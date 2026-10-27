from __future__ import annotations

import contextlib
import smtplib
import socket
import ssl
import threading
import time
import uuid
from dataclasses import dataclass
from email.errors import HeaderParseError
from email.message import EmailMessage
from email.utils import formataddr, formatdate, make_msgid
from smtplib import SMTP, SMTP_SSL
from typing import TYPE_CHECKING, BinaryIO, Literal, Self, cast

import httpcore
import httpx

from jobranger.notify.email import RenderedDigest
from jobranger.settings import ascii_address, ascii_mailbox, smtp_server

if TYPE_CHECKING:
    from collections.abc import Mapping

    from jobranger.settings import EmailConfig, EmailVia, SmtpServer

type AddressInfo = tuple[
    socket.AddressFamily,
    socket.SocketKind,
    int,
    str,
    tuple[str, int] | tuple[str, int, int, int] | tuple[int, bytes],
]
type FailureKind = Literal[
    "missing_address",
    "missing_smtp_url",
    "missing_smtp_password",
    "missing_resend_key",
    "unusable_address",
    "invalid_smtp_url",
    "non_ascii_login",
    "no_starttls",
    "not_smtp",
    "smtp_login",
    "smtp_recipient",
    "smtp_sender",
    "smtp_status",
    "smtp_unsupported",
    "tls_certificate",
    "tls",
    "smtp_disconnected",
    "smtp_reply_too_long",
    "invalid_host",
    "smtp_unreachable",
    "smtp_failed",
    "stalled",
    "smtp_too_slow",
    "resend_key",
    "unusable_resend_key",
    "resend_redirect",
    "resend_rejected",
    "resend_status",
    "resend_timeout",
    "resend_unreachable",
    "resend_too_slow",
]

SMTP_TIMEOUT_S = 30
LOCAL_HELLO = "[127.0.0.1]"
REPLY_LINE_LIMIT = 100
REPLY_BYTE_LIMIT = 64 * 1024
SEND_DEADLINE_SECONDS = 120
NOT_SMTP_CODE = -1
RESEND_URL = "https://api.resend.com/emails"
RESEND_TIMEOUT_S = 30
SENDER_NAME = "jobranger"
DEFAULT_RESEND_SENDER = f"{SENDER_NAME} <onboarding@resend.dev>"
SECRET_NAMES: dict[EmailVia, str] = {
    "smtp": "smtp_password",
    "resend": "resend_api_key",
}
STARTTLS_EXTENSION = "starttls"
TCP_CONNECTED_EVENT = "connection.connect_tcp.complete"
RESEND_KEY_STATUSES = frozenset({401, 403})
RESEND_VALIDATION_STATUS = 422
REASON_CHAR_LIMIT = 200
SETUP_HINT = "Add one with: jobranger init"
FAILURES: dict[FailureKind, str] = {
    "missing_address": f"No email address is set. {SETUP_HINT}",
    "missing_smtp_url": f"No SMTP server is set. {SETUP_HINT}",
    "missing_smtp_password": f"No SMTP password is stored. {SETUP_HINT}",
    "missing_resend_key": f"No Resend API key is stored. {SETUP_HINT}",
    "unusable_address": "The email address or sender in the config cannot be used.",
    "invalid_smtp_url": (
        "The SMTP server must look like smtps://host:465 or smtp://host:587."
    ),
    "non_ascii_login": "The SMTP username and password must be plain ASCII.",
    "no_starttls": "The SMTP server at {host}:{port} does not offer STARTTLS.",
    "not_smtp": "The server at {host}:{port} is not an SMTP server.",
    "smtp_login": "SMTP login to {host}:{port} failed.",
    "smtp_recipient": "The server refused {to}.",
    "smtp_sender": "The server refused the sender {sender}.",
    "smtp_status": "The SMTP server answered {status}.",
    "smtp_unsupported": (
        "The SMTP server at {host}:{port} does not support login or this address."
    ),
    "tls_certificate": "Could not verify the TLS certificate of {host}.",
    "tls": "TLS with {host}:{port} failed.",
    "smtp_disconnected": "The SMTP server at {host}:{port} closed the connection.",
    "smtp_reply_too_long": (
        "The SMTP server at {host}:{port} sent a reply that is too long."
    ),
    "invalid_host": "The SMTP server name {host} is not a valid host name.",
    "smtp_unreachable": "Could not reach {host}:{port}.",
    "smtp_failed": "The email could not be sent through {host}:{port}.",
    "stalled": "The SMTP server at {host}:{port} stopped answering.",
    "smtp_too_slow": (
        "The SMTP server at {host}:{port} took longer than {seconds} s,"
        " so the send was stopped."
    ),
    "resend_key": "Resend refused the key.",
    "unusable_resend_key": (
        f"The stored Resend API key has characters an API key cannot have. {SETUP_HINT}"
    ),
    "resend_redirect": (
        "Resend answered with a redirect ({status}), so the email was not sent."
    ),
    "resend_rejected": "{message}",
    "resend_status": "Resend answered {status}.",
    "resend_timeout": "Resend did not answer in time.",
    "resend_unreachable": "Could not reach Resend.",
    "resend_too_slow": "Resend took longer than {seconds} s, so the send was stopped.",
}


class ReplyTooLongError(ValueError):
    pass


SMTP_FAILURES: tuple[tuple[type[Exception], FailureKind], ...] = (
    (ReplyTooLongError, "smtp_reply_too_long"),
    (smtplib.SMTPAuthenticationError, "smtp_login"),
    (smtplib.SMTPRecipientsRefused, "smtp_recipient"),
    (smtplib.SMTPSenderRefused, "smtp_sender"),
    (smtplib.SMTPResponseException, "smtp_status"),
    (ssl.SSLCertVerificationError, "tls_certificate"),
    (ssl.SSLError, "tls"),
    (smtplib.SMTPServerDisconnected, "smtp_disconnected"),
    (smtplib.SMTPException, "smtp_unsupported"),
    (UnicodeError, "invalid_host"),
    (OSError, "smtp_unreachable"),
    (ValueError, "smtp_failed"),
)
TEST_SUBJECT = "jobranger test email"
TEST_TEXT = "Email from jobranger works. Your matches will arrive at this address."
TEST_DIGEST = RenderedDigest(TEST_SUBJECT, f"<p>{TEST_TEXT}</p>", f"{TEST_TEXT}\n")


@dataclass(frozen=True)
class Addresses:
    sender: str
    to: str


class EmailError(Exception):
    def __init__(
        self, kind: FailureKind, *, status: int | None = None, **details: object
    ) -> None:
        reason = FAILURES[kind].format(status=status, **details)
        super().__init__(reason)
        self.reason = reason
        self.kind = kind
        self.status = status


class Deadline:
    _ends: float

    def __init__(self, seconds: float) -> None:
        self.seconds = seconds
        self._lock = threading.Lock()
        self._guards: list[socket.socket] = []
        self._timer = threading.Timer(seconds, self._expire)
        self._timer.daemon = True

    def __enter__(self) -> Self:
        self._ends = time.monotonic() + self.seconds
        self._timer.start()
        return self

    def __exit__(self, *exc_info: object) -> None:
        self._timer.cancel()
        with self._lock:
            for guard in self._guards:
                guard.close()
            self._guards.clear()

    @property
    def expired(self) -> bool:
        return self.left() <= 0

    def left(self) -> float:
        return self._ends - time.monotonic()

    def watch(self, sock: socket.socket) -> None:
        with self._lock:
            guard = sock.dup()
            self._guards.append(guard)
            if self.expired:
                _shut(guard)

    def trace(self, event: str, info: Mapping[str, object]) -> None:
        if event != TCP_CONNECTED_EVENT:
            return
        stream = info.get("return_value")
        if isinstance(stream, httpcore.NetworkStream):
            self.watch(stream.get_extra_info("socket"))

    def _expire(self) -> None:
        with self._lock:
            for guard in self._guards:
                _shut(guard)


class _ReplyReader:
    def __init__(self, file: BinaryIO) -> None:
        self._file = file
        self.start()

    def start(self) -> None:
        self._lines = 0
        self._size = 0

    def readline(self, limit: int) -> bytes:
        line = self._file.readline(limit)
        self._lines += 1
        self._size += len(line)
        if (
            len(line) >= limit
            or self._lines > REPLY_LINE_LIMIT
            or self._size > REPLY_BYTE_LIMIT
        ):
            raise ReplyTooLongError
        return line

    def close(self) -> None:
        self._file.close()


def _shut(guard: socket.socket) -> None:
    with contextlib.suppress(OSError):
        guard.shutdown(socket.SHUT_RDWR)


def _resolve(host: str, port: int, deadline: Deadline) -> list[AddressInfo]:
    found: list[AddressInfo] = []
    failed: list[Exception] = []

    def lookup() -> None:
        try:
            found.extend(socket.getaddrinfo(host, port, 0, socket.SOCK_STREAM))
        except Exception as error:
            failed.append(error)

    thread = threading.Thread(target=lookup, daemon=True)
    thread.start()
    thread.join(deadline.left())
    if failed:
        raise failed[0]
    if thread.is_alive():
        raise TimeoutError
    return found


def _connect(host: str, port: int, timeout: float, deadline: Deadline) -> socket.socket:
    for family, kind, proto, _, address in _resolve(host, port, deadline):
        wait = min(SMTP_TIMEOUT_S, deadline.left())
        if wait <= 0:
            break
        sock = socket.socket(family, kind, proto)
        try:
            sock.settimeout(wait)
            sock.connect(address)
        except OSError:
            sock.close()
            continue
        sock.settimeout(timeout)
        deadline.watch(sock)
        return sock
    raise ConnectionError


class _Guarded(SMTP):
    deadline: Deadline

    def getreply(self) -> tuple[int, bytes]:
        if self.file is None:
            self.file = _ReplyReader(cast("socket.socket", self.sock).makefile("rb"))
        self.file.start()
        try:
            return super().getreply()
        except ReplyTooLongError:
            self.close()
            raise


class GuardedSMTP(_Guarded):
    def __init__(
        self, deadline: Deadline, host: str, port: int, timeout: float
    ) -> None:
        self.deadline = deadline
        super().__init__(host, port, LOCAL_HELLO, timeout=timeout)

    def _get_socket(self, host: str, port: int, timeout: float) -> socket.socket:
        return _connect(host, port, timeout, self.deadline)


class GuardedSMTPSSL(_Guarded, SMTP_SSL):
    def __init__(
        self,
        deadline: Deadline,
        host: str,
        port: int,
        timeout: float,
        context: ssl.SSLContext,
    ) -> None:
        self.deadline = deadline
        super().__init__(host, port, LOCAL_HELLO, timeout=timeout, context=context)

    def _get_socket(self, host: str, port: int, timeout: float) -> socket.socket:
        sock = _connect(host, port, timeout, self.deadline)
        try:
            return self.context.wrap_socket(sock, server_hostname=host)
        except BaseException:
            sock.close()
            raise


def send_email(
    email: EmailConfig,
    secret: str | None,
    rendered: RenderedDigest,
    client: httpx.Client,
    idempotency_key: str,
) -> None:
    addresses = _addresses(email)
    if email.via == "resend":
        if not secret:
            raise EmailError("missing_resend_key")
        send_resend(secret, rendered, addresses, client, idempotency_key)
        return
    if email.smtp_url is None:
        raise EmailError("missing_smtp_url")
    msg = build_message(rendered, addresses.sender, addresses.to)
    send_smtp(email.smtp_url, email.smtp_user, secret, msg)


def send_test_email(email: EmailConfig, secret: str, client: httpx.Client) -> None:
    send_email(email, secret, TEST_DIGEST, client, uuid.uuid4().hex)


def _addresses(email: EmailConfig) -> Addresses:
    if email.address is None:
        raise EmailError("missing_address")
    try:
        to = ascii_address(email.address)
        sender = ascii_mailbox(sender_for(email, to))
    except (ValueError, HeaderParseError) as error:
        raise EmailError("unusable_address") from error
    return Addresses(sender, to)


def sender_for(email: EmailConfig, to: str) -> str:
    if email.sender:
        return email.sender
    if email.via == "resend":
        return DEFAULT_RESEND_SENDER
    return formataddr((SENDER_NAME, to))


def build_message(rendered: RenderedDigest, sender: str, to: str) -> EmailMessage:
    msg = EmailMessage()
    msg["Subject"] = rendered.subject
    msg["From"] = sender
    msg["To"] = to
    msg["Date"] = formatdate(localtime=True)
    msg["Message-ID"] = make_msgid(domain=msg["From"].addresses[0].domain)
    msg.set_content(rendered.text)
    msg.add_alternative(rendered.html, subtype="html")
    return msg


def send_smtp(
    url: str, user: str | None, password: str | None, msg: EmailMessage
) -> None:
    try:
        server = smtp_server(url)
    except ValueError as error:
        raise EmailError("invalid_smtp_url") from error
    login = _login(user, password)
    with Deadline(SEND_DEADLINE_SECONDS) as deadline:
        try:
            _send_smtp(server, login, msg, deadline)
        except (smtplib.SMTPException, OSError, ValueError) as error:
            raise _smtp_failure(error, server, str(msg["To"]), deadline) from error


def _login(user: str | None, password: str | None) -> tuple[str, str] | None:
    if not user:
        return None
    if not password:
        raise EmailError("missing_smtp_password")
    if not (user + password).isascii():
        raise EmailError("non_ascii_login")
    return user, password


def _send_smtp(
    server: SmtpServer,
    login: tuple[str, str] | None,
    msg: EmailMessage,
    deadline: Deadline,
) -> None:
    context = ssl.create_default_context()
    client: SMTP
    if server.implicit_tls:
        client = GuardedSMTPSSL(
            deadline, server.host, server.port, SMTP_TIMEOUT_S, context
        )
    else:
        client = GuardedSMTP(deadline, server.host, server.port, SMTP_TIMEOUT_S)
    try:
        if not server.implicit_tls:
            _start_tls(client, context, server)
        if login:
            client.login(*login)
        client.send_message(msg)
        with contextlib.suppress(OSError):
            client.quit()
    finally:
        client.close()


def _start_tls(client: SMTP, context: ssl.SSLContext, server: SmtpServer) -> None:
    client.ehlo()
    if not client.has_extn(STARTTLS_EXTENSION):
        raise EmailError("no_starttls", host=server.host, port=server.port)
    client.starttls(context=context)
    client.ehlo()


def _smtp_failure(
    error: Exception, server: SmtpServer, to: str, deadline: Deadline
) -> EmailError:
    host, port = server.host, server.port
    if deadline.expired:
        return EmailError(
            "smtp_too_slow", host=host, port=port, seconds=deadline.seconds
        )
    if _stalled(error):
        return EmailError("stalled", host=host, port=port)
    code = _smtp_code(error)
    if code == NOT_SMTP_CODE:
        return EmailError("not_smtp", host=host, port=port)
    kind = next(kind for type_, kind in SMTP_FAILURES if isinstance(error, type_))
    sender = getattr(error, "sender", "")
    return EmailError(kind, status=code, host=host, port=port, to=to, sender=sender)


def _smtp_code(error: Exception) -> int | None:
    if isinstance(error, smtplib.SMTPResponseException):
        return error.smtp_code
    if isinstance(error, smtplib.SMTPRecipientsRefused):
        return next((code for code, _ in error.recipients.values()), None)
    return None


def _stalled(error: Exception) -> bool:
    lost = isinstance(error, smtplib.SMTPServerDisconnected)
    return isinstance(error, TimeoutError) or (
        lost and isinstance(error.__context__, TimeoutError)
    )


def send_resend(
    key: str,
    rendered: RenderedDigest,
    addresses: Addresses,
    client: httpx.Client,
    idempotency_key: str,
) -> None:
    if not (key.isascii() and key.isprintable()):
        raise EmailError("unusable_resend_key")
    payload = {
        "from": addresses.sender,
        "to": [addresses.to],
        "subject": rendered.subject,
        "html": rendered.html,
        "text": rendered.text,
    }
    headers = {
        "Authorization": f"Bearer {key}",
        "Idempotency-Key": idempotency_key,
        "Connection": "close",
    }
    with Deadline(SEND_DEADLINE_SECONDS) as deadline:
        try:
            response = client.post(
                RESEND_URL,
                json=payload,
                headers=headers,
                timeout=RESEND_TIMEOUT_S,
                follow_redirects=False,
                extensions={"trace": deadline.trace},
            )
        except httpx.HTTPError as error:
            raise _resend_failure(error, deadline) from error
    if response.is_success:
        return
    status = response.status_code
    if response.is_redirect:
        raise EmailError("resend_redirect", status=status)
    if status in RESEND_KEY_STATUSES:
        raise EmailError("resend_key", status=status)
    message = _resend_message(response) if status == RESEND_VALIDATION_STATUS else ""
    if message:
        raise EmailError("resend_rejected", status=status, message=message)
    raise EmailError("resend_status", status=status)


def _resend_failure(error: httpx.HTTPError, deadline: Deadline) -> EmailError:
    if deadline.expired:
        return EmailError("resend_too_slow", seconds=deadline.seconds)
    if isinstance(error, httpx.ReadTimeout | httpx.WriteTimeout):
        return EmailError("resend_timeout")
    return EmailError("resend_unreachable")


def _resend_message(response: httpx.Response) -> str:
    try:
        body = response.json()
    except ValueError:
        return ""
    message = body.get("message") if isinstance(body, dict) else None
    if not isinstance(message, str):
        return ""
    shown = "".join(char for char in message if char.isprintable() or char.isspace())
    return " ".join(shown.split())[:REASON_CHAR_LIMIT]
