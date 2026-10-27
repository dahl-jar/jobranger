from __future__ import annotations

import contextlib
import ipaddress
import smtplib
import socket
import ssl
import threading
import time
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from email.utils import parsedate_to_datetime
from typing import TYPE_CHECKING

import httpx
import pytest
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.x509.oid import NameOID
from mail_fakes import (
    ADDRESS,
    RESEND_EMAIL,
    RESEND_KEY,
    SMTP_EMAIL,
    SMTP_PASSWORD,
    SMTP_URL,
    SMTP_USER,
    FakeSMTP,
    ResendRecorder,
)

from jobranger.notify import send
from jobranger.notify.email import RenderedDigest
from jobranger.notify.send import (
    DEFAULT_RESEND_SENDER,
    REPLY_BYTE_LIMIT,
    REPLY_LINE_LIMIT,
    RESEND_TIMEOUT_S,
    RESEND_URL,
    SMTP_TIMEOUT_S,
    TEST_SUBJECT,
    Addresses,
    EmailError,
    build_message,
    send_email,
    send_resend,
    send_smtp,
    send_test_email,
    sender_for,
)
from jobranger.settings import EmailConfig

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

IDN_ADDRESS = "user@b\u00f8.example"
ASCII_IDN_ADDRESS = "user@xn--b-5ga.example"
SHORT_DEADLINE_SECONDS = 0.3
DRIP_INTERVAL_SECONDS = 0.05
DEADLINE_SLACK_SECONDS = 2
DRIP_LIMIT_SECONDS = 4
SHORT_CONNECT_TIMEOUT_SECONDS = 0.2
MANY_ADDRESSES = 20
FULL_REPLY_LINES = 8
FULL_LINE_WIDTH = (REPLY_BYTE_LIMIT - len(b"220 ok\r\n")) // FULL_REPLY_LINES - len(
    b"220-\r\n"
)
LOOPBACK = "127.0.0.1"
SMTP_PORT = 587
SENDER = "jobranger <sender@example.test>"
SMTPS_URL = "smtps://smtp.example.test"
RUN_KEY = "run-1"
ADDRESSES = Addresses(SENDER, ADDRESS)
RENDERED = RenderedDigest(
    subject="jobranger: 1 match, Thursday 8 October",
    html="<p>One strong fit</p>",
    text="One strong fit\n",
)


def test_should_put_text_part_before_html_part() -> None:
    msg = build_message(RENDERED, SENDER, ADDRESS)

    assert [part.get_content_type() for part in msg.iter_parts()] == [
        "text/plain",
        "text/html",
    ]


def test_should_set_message_id_on_sender_domain() -> None:
    msg = build_message(RENDERED, SENDER, ADDRESS)

    assert str(msg["Message-ID"]).endswith("@example.test>")


def test_should_require_starttls_for_smtp_scheme(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(
        SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
    )

    assert server.calls == [
        "connect",
        "ehlo",
        "starttls",
        "ehlo",
        "login",
        "send_message",
        "quit",
    ]


def test_should_refuse_smtp_when_server_lacks_starttls(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP(offers_starttls=False).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert (raised.value.reason, "login" in server.calls) == (
        "The SMTP server at smtp.example.test:587 does not offer STARTTLS.",
        False,
    )


def test_should_use_smtp_ssl_for_smtps_scheme(monkeypatch: pytest.MonkeyPatch) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(
        SMTPS_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
    )

    assert (server.connections, "starttls" in server.calls) == (
        [("SMTP_SSL", "smtp.example.test", 465)],
        False,
    )


def test_should_skip_login_without_smtp_user(monkeypatch: pytest.MonkeyPatch) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))

    assert server.logins == []


@pytest.mark.parametrize(
    ("fail_on", "error", "reason"),
    [
        (
            "login",
            smtplib.SMTPAuthenticationError(535, b"bad credentials"),
            "SMTP login to smtp.example.test:587 failed.",
        ),
        (
            "send_message",
            smtplib.SMTPRecipientsRefused({ADDRESS: (550, b"no such user")}),
            f"The server refused {ADDRESS}.",
        ),
        (
            "send_message",
            smtplib.SMTPSenderRefused(553, b"not yours", "sender@example.test"),
            "The server refused the sender sender@example.test.",
        ),
        (
            "send_message",
            smtplib.SMTPDataError(554, b"spam"),
            "The SMTP server answered 554.",
        ),
        (
            "login",
            smtplib.SMTPException("No suitable authentication method found."),
            "The SMTP server at smtp.example.test:587 does not support login or"
            " this address.",
        ),
        (
            "starttls",
            ssl.SSLCertVerificationError("certificate verify failed"),
            "Could not verify the TLS certificate of smtp.example.test.",
        ),
        (
            "starttls",
            ssl.SSLError("wrong version number"),
            "TLS with smtp.example.test:587 failed.",
        ),
        (
            "send_message",
            smtplib.SMTPServerDisconnected("Connection unexpectedly closed"),
            "The SMTP server at smtp.example.test:587 closed the connection.",
        ),
        (
            "connect",
            ConnectionRefusedError(61, "Connection refused"),
            "Could not reach smtp.example.test:587.",
        ),
    ],
    ids=[
        "login",
        "recipient",
        "sender",
        "response code",
        "no login method",
        "certificate",
        "tls",
        "disconnected",
        "unreachable",
    ],
)
def test_should_explain_smtp_failure(
    monkeypatch: pytest.MonkeyPatch, fail_on: str, error: Exception, reason: str
) -> None:
    FakeSMTP(fail_on=fail_on, error=error).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert raised.value.reason == reason


def test_should_send_digest_to_resend_with_key() -> None:
    recorder = ResendRecorder()

    send_resend(
        RESEND_KEY,
        RENDERED,
        ADDRESSES,
        recorder.client(),
        RUN_KEY,
    )

    request = recorder.requests[0]
    assert (
        str(request.url),
        request.headers["Authorization"],
        request.headers["Idempotency-Key"],
        recorder.sent(),
    ) == (
        RESEND_URL,
        f"Bearer {RESEND_KEY}",
        RUN_KEY,
        {
            "from": SENDER,
            "to": [ADDRESS],
            "subject": RENDERED.subject,
            "html": RENDERED.html,
            "text": RENDERED.text,
        },
    )


@pytest.mark.parametrize(
    ("status", "body", "reason"),
    [
        (401, {"message": "API key is invalid"}, "Resend refused the key."),
        (403, {"message": "not allowed"}, "Resend refused the key."),
        (
            422,
            {"message": "Invalid `from` field.\n\x1b[31m"},
            "Invalid `from` field. [31m",
        ),
        (422, {"name": "validation_error"}, "Resend answered 422."),
        (500, {"message": "internal"}, "Resend answered 500."),
    ],
    ids=["401", "403", "422 message", "422 without message", "500"],
)
def test_should_explain_resend_failure(status: int, body: object, reason: str) -> None:
    recorder = ResendRecorder(status, body)

    with pytest.raises(EmailError) as raised:
        send_resend(
            RESEND_KEY,
            RENDERED,
            ADDRESSES,
            recorder.client(),
            RUN_KEY,
        )

    assert raised.value.reason == reason


def test_should_explain_unreachable_resend() -> None:
    def time_out(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectTimeout("timed out", request=request)

    client = httpx.Client(transport=httpx.MockTransport(time_out))

    with pytest.raises(EmailError, match=r"^Could not reach Resend\.$"):
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, client, RUN_KEY)


@pytest.mark.parametrize(
    "key", ["re_fak\u00eb", "re_fake\x7fkey"], ids=["non-ascii", "control character"]
)
def test_should_refuse_resend_key_before_sending(key: str) -> None:
    recorder = ResendRecorder()

    with pytest.raises(EmailError) as raised:
        send_resend(key, RENDERED, ADDRESSES, recorder.client(), RUN_KEY)

    assert (raised.value.reason, recorder.requests) == (
        "The stored Resend API key has characters an API key cannot have."
        " Add one with: jobranger init",
        [],
    )


@pytest.mark.parametrize(
    ("email", "expected"),
    [
        (SMTP_EMAIL, f"jobranger <{ADDRESS}>"),
        (RESEND_EMAIL, DEFAULT_RESEND_SENDER),
        (SMTP_EMAIL.model_copy(update={"sender": SENDER}), SENDER),
    ],
    ids=["smtp default", "resend default", "explicit"],
)
def test_should_pick_sender(email: EmailConfig, expected: str) -> None:
    assert sender_for(email, ADDRESS) == expected


@pytest.mark.parametrize(
    ("email", "secret", "reason"),
    [
        (
            RESEND_EMAIL,
            None,
            "No Resend API key is stored. Add one with: jobranger init",
        ),
        (
            SMTP_EMAIL,
            None,
            "No SMTP password is stored. Add one with: jobranger init",
        ),
        (
            EmailConfig(via="resend"),
            RESEND_KEY,
            "No email address is set. Add one with: jobranger init",
        ),
        (
            EmailConfig(address=ADDRESS, via="smtp"),
            SMTP_PASSWORD,
            "No SMTP server is set. Add one with: jobranger init",
        ),
    ],
    ids=["resend key", "smtp password", "address", "smtp server"],
)
def test_should_refuse_send_when_setting_is_missing(
    monkeypatch: pytest.MonkeyPatch, email: EmailConfig, secret: str | None, reason: str
) -> None:
    server = FakeSMTP().install(monkeypatch)
    recorder = ResendRecorder()

    with pytest.raises(EmailError) as raised:
        send_email(email, secret, RENDERED, recorder.client(), RUN_KEY)

    assert (raised.value.reason, server.calls, recorder.requests) == (reason, [], [])


def test_should_send_test_email_to_configured_address(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_test_email(SMTP_EMAIL, SMTP_PASSWORD, ResendRecorder().client())

    sent = server.messages[0]
    assert (sent["To"], sent["Subject"], server.logins) == (
        ADDRESS,
        TEST_SUBJECT,
        [(SMTP_USER, SMTP_PASSWORD)],
    )


def test_should_date_message_with_send_time() -> None:
    msg = build_message(RENDERED, SENDER, ADDRESS)

    sent_at = parsedate_to_datetime(str(msg["Date"]))
    assert abs(datetime.now(tz=UTC) - sent_at) < timedelta(minutes=1)


def test_should_connect_to_smtp_server_from_url(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(
        SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
    )

    assert server.connections == [("SMTP", "smtp.example.test", 587)]


@pytest.mark.parametrize("url", [SMTP_URL, SMTPS_URL], ids=["starttls", "smtps"])
def test_should_verify_server_certificate(
    monkeypatch: pytest.MonkeyPatch, url: str
) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(url, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS))

    assert [
        (context.verify_mode, context.check_hostname) for context in server.contexts
    ] == [(ssl.CERT_REQUIRED, True)]


@pytest.mark.parametrize("url", [SMTP_URL, SMTPS_URL], ids=["starttls", "smtps"])
def test_should_bound_smtp_wait(monkeypatch: pytest.MonkeyPatch, url: str) -> None:
    server = FakeSMTP().install(monkeypatch)

    send_smtp(url, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS))

    assert server.timeouts == [SMTP_TIMEOUT_S]


def test_should_explain_invalid_smtp_url(monkeypatch: pytest.MonkeyPatch) -> None:
    server = FakeSMTP().install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            "http://smtp.example.test",
            SMTP_USER,
            SMTP_PASSWORD,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    assert (raised.value.reason, server.calls) == (
        "The SMTP server must look like smtps://host:465 or smtp://host:587.",
        [],
    )


def test_should_bound_resend_wait() -> None:
    recorder = ResendRecorder()

    send_resend(RESEND_KEY, RENDERED, ADDRESSES, recorder.client(), RUN_KEY)

    assert set(recorder.requests[0].extensions["timeout"].values()) == {
        RESEND_TIMEOUT_S
    }


def test_should_name_status_when_resend_error_is_not_json() -> None:
    client = httpx.Client(
        transport=httpx.MockTransport(lambda request: httpx.Response(422, text="no"))
    )

    with pytest.raises(EmailError, match=r"^Resend answered 422\.$"):
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, client, RUN_KEY)


def test_should_send_test_email_through_resend_with_idempotency_key() -> None:
    recorder = ResendRecorder()

    send_test_email(RESEND_EMAIL, RESEND_KEY, recorder.client())

    request = recorder.requests[0]
    assert (recorder.sent()["subject"], len(request.headers["Idempotency-Key"])) == (
        TEST_SUBJECT,
        32,
    )


def _timed_out_disconnect() -> smtplib.SMTPServerDisconnected:
    error = smtplib.SMTPServerDisconnected("Connection unexpectedly closed: timed out")
    error.__context__ = TimeoutError("timed out")
    return error


class _DripServer:
    def __init__(self, first: bytes) -> None:
        self.first = first
        self.listener = socket.create_server(("127.0.0.1", 0))
        self.port = self.listener.getsockname()[1]
        self.stop = threading.Event()
        self.thread = threading.Thread(target=self._serve, daemon=True)

    def _serve(self) -> None:
        self.listener.settimeout(DEADLINE_SLACK_SECONDS)
        try:
            connection, _ = self.listener.accept()
        except OSError:
            return
        with connection:
            connection.settimeout(DRIP_INTERVAL_SECONDS)
            with contextlib.suppress(OSError):
                connection.recv(4096)
            try:
                connection.sendall(self.first)
                ends = time.monotonic() + DRIP_LIMIT_SECONDS
                while time.monotonic() < ends and not self.stop.wait(
                    DRIP_INTERVAL_SECONDS
                ):
                    connection.sendall(b"x")
            except OSError:
                return


@pytest.fixture
def drip_server(request: pytest.FixtureRequest) -> Iterator[_DripServer]:
    server = _DripServer(request.param)
    server.thread.start()
    yield server
    server.stop.set()
    server.thread.join()
    server.listener.close()


@pytest.fixture
def short_deadline(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(send, "SEND_DEADLINE_SECONDS", SHORT_DEADLINE_SECONDS)


def test_should_refuse_non_ascii_login_before_connecting(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL,
            SMTP_USER,
            "pass\u00f8rd",
            build_message(RENDERED, SENDER, ADDRESS),
        )

    assert (raised.value.reason, server.calls) == (
        "The SMTP username and password must be plain ASCII.",
        [],
    )


def test_should_explain_host_name_the_system_cannot_encode(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    FakeSMTP(fail_on="connect", error=UnicodeError("label empty")).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert raised.value.reason == (
        "The SMTP server name smtp.example.test is not a valid host name."
    )


@pytest.mark.parametrize(
    ("fail_on", "error"),
    [("ehlo", _timed_out_disconnect()), ("send_message", TimeoutError("timed out"))],
    ids=["reply timed out", "send timed out"],
)
def test_should_say_server_stopped_answering_when_it_stalls(
    monkeypatch: pytest.MonkeyPatch, fail_on: str, error: Exception
) -> None:
    FakeSMTP(fail_on=fail_on, error=error).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert raised.value.reason == (
        "The SMTP server at smtp.example.test:587 stopped answering."
    )


@pytest.mark.parametrize(
    ("greeting", "reason"),
    [
        (-1, "The server at smtp.example.test:587 is not an SMTP server."),
        (421, "The SMTP server answered 421."),
    ],
    ids=["not smtp", "busy"],
)
def test_should_stop_at_unwelcome_greeting(
    monkeypatch: pytest.MonkeyPatch, greeting: int, reason: str
) -> None:
    server = FakeSMTP(greeting=greeting).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert (raised.value.reason, "login" in server.calls) == (reason, False)


@pytest.mark.parametrize("drip_server", [b"220"], indirect=True)
@pytest.mark.usefixtures("short_deadline")
def test_should_stop_smtp_send_when_server_drips_past_deadline(
    drip_server: _DripServer,
) -> None:
    started = time.monotonic()

    with pytest.raises(EmailError) as raised:
        send_smtp(
            f"smtp://127.0.0.1:{drip_server.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    assert (
        raised.value.reason,
        time.monotonic() - started < DEADLINE_SLACK_SECONDS,
    ) == (
        f"The SMTP server at 127.0.0.1:{drip_server.port} took longer than"
        f" {SHORT_DEADLINE_SECONDS} s, so the send was stopped.",
        True,
    )


@pytest.mark.parametrize("drip_server", [b"HTTP/1.1 200 OK\r\n"], indirect=True)
@pytest.mark.usefixtures("short_deadline")
def test_should_stop_resend_send_when_answer_drips_past_deadline(
    monkeypatch: pytest.MonkeyPatch, drip_server: _DripServer
) -> None:
    monkeypatch.setattr(send, "RESEND_URL", f"http://127.0.0.1:{drip_server.port}/")
    started = time.monotonic()

    with pytest.raises(EmailError) as raised, httpx.Client() as client:
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, client, RUN_KEY)

    assert (
        raised.value.reason,
        time.monotonic() - started < DEADLINE_SLACK_SECONDS,
    ) == (
        f"Resend took longer than {SHORT_DEADLINE_SECONDS} s, so the send was stopped.",
        True,
    )


def test_should_say_resend_did_not_answer_in_time() -> None:
    def stall(request: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout("timed out", request=request)

    client = httpx.Client(transport=httpx.MockTransport(stall))

    with pytest.raises(EmailError, match=r"^Resend did not answer in time\.$"):
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, client, RUN_KEY)


def test_should_refuse_resend_redirect_without_following_it() -> None:
    requests: list[httpx.Request] = []

    def redirect(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        if request.url.host == "api.resend.com":
            return httpx.Response(
                307, headers={"Location": "http://sink.example.test/"}
            )
        return httpx.Response(200, json={"id": "email-1"})

    client = httpx.Client(
        transport=httpx.MockTransport(redirect), follow_redirects=True
    )

    with pytest.raises(EmailError) as raised:
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, client, RUN_KEY)

    assert (raised.value.reason, len(requests)) == (
        "Resend answered with a redirect (307), so the email was not sent.",
        1,
    )


@pytest.mark.parametrize(
    ("fail_on", "error", "kind", "status"),
    [
        ("login", smtplib.SMTPAuthenticationError(535, b"no"), "smtp_login", 535),
        ("connect", ConnectionRefusedError(61, "refused"), "smtp_unreachable", None),
    ],
    ids=["login", "unreachable"],
)
def test_should_tag_smtp_failure_with_category_and_code(
    monkeypatch: pytest.MonkeyPatch,
    fail_on: str,
    error: Exception,
    kind: str,
    status: int | None,
) -> None:
    FakeSMTP(fail_on=fail_on, error=error).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert (raised.value.kind, raised.value.status) == (kind, status)


def test_should_send_ascii_domain_in_smtp_headers(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)
    email = SMTP_EMAIL.model_copy(update={"address": IDN_ADDRESS})

    send_email(email, SMTP_PASSWORD, RENDERED, ResendRecorder().client(), RUN_KEY)

    sent = server.messages[0]
    assert (str(sent["To"]), str(sent["From"])) == (
        ASCII_IDN_ADDRESS,
        f"jobranger <{ASCII_IDN_ADDRESS}>",
    )


def test_should_send_ascii_domain_to_resend() -> None:
    recorder = ResendRecorder()
    email = RESEND_EMAIL.model_copy(update={"address": IDN_ADDRESS})

    send_email(email, RESEND_KEY, RENDERED, recorder.client(), RUN_KEY)

    assert recorder.sent()["to"] == [ASCII_IDN_ADDRESS]


def test_should_refuse_unusable_address(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    server = FakeSMTP().install(monkeypatch)
    email = EmailConfig.model_construct(
        on=True,
        address="user@smtp..example.test",
        via="smtp",
        smtp_url=SMTP_URL,
        smtp_user=None,
        sender=None,
    )

    with pytest.raises(EmailError) as raised:
        send_email(email, None, RENDERED, ResendRecorder().client(), RUN_KEY)

    assert (raised.value.reason, server.calls) == (
        "The email address or sender in the config cannot be used.",
        [],
    )


class _HelloCatcher:
    def __init__(self, implicit_tls: bool) -> None:
        self.implicit_tls = implicit_tls
        self.listener = socket.create_server(("127.0.0.1", 0))
        self.port = self.listener.getsockname()[1]
        self.hello = b""
        self.ehlo = b""
        self.thread = threading.Thread(target=self._serve, daemon=True)
        self.thread.start()

    def _serve(self) -> None:
        self.listener.settimeout(DEADLINE_SLACK_SECONDS)
        try:
            connection, _ = self.listener.accept()
        except OSError:
            return
        with connection:
            connection.settimeout(DEADLINE_SLACK_SECONDS)
            reader = connection.makefile("rb")
            if not self.implicit_tls:
                connection.sendall(b"220 ready\r\n")
                self.ehlo = reader.readline()
                connection.sendall(b"250-sink\r\n250 STARTTLS\r\n")
                reader.readline()
                connection.sendall(b"220 go ahead\r\n")
            with contextlib.suppress(OSError):
                self.hello = connection.recv(4096)


@pytest.mark.parametrize(
    ("scheme", "implicit_tls"),
    [("smtp", False), ("smtps", True)],
    ids=["starttls", "smtps"],
)
def test_should_name_server_in_tls_handshake(scheme: str, implicit_tls: bool) -> None:
    catcher = _HelloCatcher(implicit_tls)

    with pytest.raises(EmailError):
        send_smtp(
            f"{scheme}://localhost:{catcher.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    catcher.thread.join()
    catcher.listener.close()
    assert b"localhost" in catcher.hello


def test_should_close_without_quit_after_failure(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    error = smtplib.SMTPAuthenticationError(535, b"no")
    server = FakeSMTP(fail_on="login", error=error).install(monkeypatch)

    with pytest.raises(EmailError, match="SMTP login to"):
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert ("quit" in server.calls, server.closed) == (False, True)


def test_should_count_email_as_sent_when_quit_fails(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    error = smtplib.SMTPResponseException(403, b"bye")
    server = FakeSMTP(fail_on="quit", error=error).install(monkeypatch)

    send_smtp(
        SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
    )

    assert (len(server.messages), server.closed) == (1, True)


def test_should_explain_failure_when_message_cannot_be_sent(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    FakeSMTP(fail_on="send_message", error=ValueError("bad header")).install(
        monkeypatch
    )

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert raised.value.reason == (
        "The email could not be sent through smtp.example.test:587."
    )


def _address_infos(*ports: int) -> list[tuple[object, ...]]:
    return [
        (socket.AF_INET, socket.SOCK_STREAM, socket.IPPROTO_TCP, "", (LOOPBACK, port))
        for port in ports
    ]


def _free_port() -> int:
    with socket.create_server((LOOPBACK, 0)) as listener:
        return listener.getsockname()[1]


@pytest.mark.parametrize(
    ("url", "port"), [(SMTP_URL, 587), (SMTPS_URL, 465)], ids=["starttls", "smtps"]
)
def test_should_bound_tcp_connect_wait(
    monkeypatch: pytest.MonkeyPatch, url: str, port: int
) -> None:
    lookups: list[object] = []
    attempts: list[float | None] = []

    def resolve(host: str, *_: object) -> list[tuple[object, ...]]:
        lookups.append(host)
        return _address_infos(port)

    def time_out(sock: socket.socket, address: object) -> None:
        attempts.append(sock.gettimeout())
        raise TimeoutError

    monkeypatch.setattr(send.socket, "getaddrinfo", resolve)
    monkeypatch.setattr(socket.socket, "connect", time_out)

    with pytest.raises(EmailError) as raised:
        send_smtp(url, None, None, build_message(RENDERED, SENDER, ADDRESS))

    assert (raised.value.reason, lookups, attempts) == (
        f"Could not reach smtp.example.test:{port}.",
        ["smtp.example.test"],
        [SMTP_TIMEOUT_S],
    )


@pytest.mark.usefixtures("short_deadline")
def test_should_stop_connecting_when_addresses_outlast_deadline(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def hang(sock: socket.socket, address: object) -> None:
        time.sleep(sock.gettimeout() or 0)
        raise TimeoutError

    infos = _address_infos(*[SMTP_PORT] * MANY_ADDRESSES)
    monkeypatch.setattr(send, "SMTP_TIMEOUT_S", SHORT_CONNECT_TIMEOUT_SECONDS)
    monkeypatch.setattr(send.socket, "getaddrinfo", lambda *_: infos)
    monkeypatch.setattr(socket.socket, "connect", hang)
    started = time.monotonic()

    with pytest.raises(EmailError) as raised:
        send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))

    assert (raised.value.kind, time.monotonic() - started < DEADLINE_SLACK_SECONDS) == (
        "smtp_too_slow",
        True,
    )


@pytest.mark.usefixtures("short_deadline")
def test_should_stop_name_lookup_at_deadline(monkeypatch: pytest.MonkeyPatch) -> None:
    released = threading.Event()

    def stall(*_: object) -> list[tuple[object, ...]]:
        released.wait(DRIP_LIMIT_SECONDS)
        return []

    monkeypatch.setattr(send.socket, "getaddrinfo", stall)
    started = time.monotonic()

    try:
        with pytest.raises(EmailError) as raised:
            send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))
    finally:
        released.set()

    assert (raised.value.kind, time.monotonic() - started < DEADLINE_SLACK_SECONDS) == (
        "smtp_too_slow",
        True,
    )


@pytest.mark.usefixtures("short_deadline")
def test_should_not_block_exit_on_stalled_lookup(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    released = threading.Event()

    def stall(*_: object) -> list[tuple[object, ...]]:
        released.wait(DRIP_LIMIT_SECONDS)
        return []

    monkeypatch.setattr(send.socket, "getaddrinfo", stall)

    try:
        with pytest.raises(EmailError):
            send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))
        blocking = [
            thread
            for thread in threading.enumerate()
            if not thread.daemon and thread is not threading.main_thread()
        ]
    finally:
        released.set()

    assert blocking == []


def test_should_explain_host_name_lookup_cannot_encode(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def refuse(*_: object) -> list[tuple[object, ...]]:
        raise UnicodeError("label too long")

    monkeypatch.setattr(send.socket, "getaddrinfo", refuse)

    with pytest.raises(EmailError) as raised:
        send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))

    assert raised.value.kind == "invalid_host"


@pytest.mark.parametrize("scheme", ["smtp", "smtps"])
def test_should_say_server_stopped_answering_when_greeting_never_comes(
    monkeypatch: pytest.MonkeyPatch, scheme: str
) -> None:
    monkeypatch.setattr(send, "SMTP_TIMEOUT_S", SHORT_CONNECT_TIMEOUT_SECONDS)
    monkeypatch.setattr(send, "SEND_DEADLINE_SECONDS", DEADLINE_SLACK_SECONDS)

    silent = socket.create_server((LOOPBACK, 0))

    with silent, pytest.raises(EmailError) as raised:
        send_smtp(
            f"{scheme}://{LOOPBACK}:{silent.getsockname()[1]}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    assert raised.value.kind == "stalled"


def test_should_try_next_address_when_first_refuses(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    catcher = _HelloCatcher(implicit_tls=False)
    infos = _address_infos(_free_port(), catcher.port)
    monkeypatch.setattr(send.socket, "getaddrinfo", lambda *_: infos)

    with pytest.raises(EmailError):
        send_smtp(SMTP_URL, None, None, build_message(RENDERED, SENDER, ADDRESS))

    catcher.thread.join()
    catcher.listener.close()
    assert catcher.ehlo.startswith(b"ehlo ")


def test_should_greet_server_without_machine_name() -> None:
    catcher = _HelloCatcher(implicit_tls=False)

    with pytest.raises(EmailError):
        send_smtp(
            f"smtp://{LOOPBACK}:{catcher.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    catcher.thread.join()
    catcher.listener.close()
    assert catcher.ehlo == b"ehlo [127.0.0.1]\r\n"


class _ReplyServer:
    def __init__(self, greeting: bytes) -> None:
        self.greeting = greeting
        self.request: bytes | None = None
        self.listener = socket.create_server((LOOPBACK, 0))
        self.port = self.listener.getsockname()[1]
        self.thread = threading.Thread(target=self._serve, daemon=True)
        self.thread.start()

    def _serve(self) -> None:
        self.listener.settimeout(DEADLINE_SLACK_SECONDS)
        try:
            connection, _ = self.listener.accept()
        except OSError:
            return
        with connection, contextlib.suppress(OSError):
            connection.settimeout(DEADLINE_SLACK_SECONDS)
            connection.sendall(self.greeting)
            self.request = connection.makefile("rb").readline()
            connection.sendall(b"502 no\r\n")
            while connection.recv(4096):
                pass


def _reply(lines: int, width: int = 1, tail: int = 0) -> bytes:
    continuation = b"220-" + b"x" * width + b"\r\n"
    return continuation * lines + b"220 ok" + b"k" * tail + b"\r\n"


@pytest.mark.parametrize(
    ("greeting", "kind"),
    [
        (_reply(REPLY_LINE_LIMIT - 1), "no_starttls"),
        (_reply(REPLY_LINE_LIMIT), "smtp_reply_too_long"),
        (_reply(FULL_REPLY_LINES, FULL_LINE_WIDTH), "no_starttls"),
        (_reply(FULL_REPLY_LINES, FULL_LINE_WIDTH, tail=1), "smtp_reply_too_long"),
    ],
    ids=["lines at limit", "lines over limit", "bytes at limit", "bytes over limit"],
)
def test_should_refuse_reply_over_size_limit(greeting: bytes, kind: str) -> None:
    server = _ReplyServer(greeting)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            f"smtp://{LOOPBACK}:{server.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    server.thread.join()
    server.listener.close()
    assert raised.value.kind == kind


def test_should_hang_up_after_too_long_reply() -> None:
    server = _ReplyServer(_reply(REPLY_LINE_LIMIT))

    with pytest.raises(EmailError) as raised:
        send_smtp(
            f"smtp://{LOOPBACK}:{server.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    server.thread.join()
    server.listener.close()
    assert (raised.value.kind, server.request) == ("smtp_reply_too_long", b"")


def test_should_stop_reading_line_without_end(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(send, "SEND_DEADLINE_SECONDS", DEADLINE_SLACK_SECONDS)
    server = _ReplyServer(b"220-" + b"x" * 2 * REPLY_BYTE_LIMIT)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            f"smtp://{LOOPBACK}:{server.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    server.thread.join()
    server.listener.close()
    assert (raised.value.kind, raised.value.status) == ("smtp_reply_too_long", None)


def test_should_name_server_that_sends_too_long_reply() -> None:
    server = _ReplyServer(_reply(REPLY_LINE_LIMIT))

    with pytest.raises(EmailError) as raised:
        send_smtp(
            f"smtp://{LOOPBACK}:{server.port}",
            None,
            None,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    server.thread.join()
    server.listener.close()
    assert raised.value.reason == (
        f"The SMTP server at {LOOPBACK}:{server.port} sent a reply that is too long."
    )


@contextlib.contextmanager
def _expired_deadline() -> Iterator[send.Deadline]:
    with send.Deadline(0) as deadline:
        ends = time.monotonic() + DEADLINE_SLACK_SECONDS
        while not deadline.expired and time.monotonic() < ends:
            time.sleep(DRIP_INTERVAL_SECONDS)
        yield deadline


def test_should_shut_socket_watched_after_deadline() -> None:
    near, far = socket.socketpair()
    with near, far, _expired_deadline() as deadline:
        far.settimeout(DEADLINE_SLACK_SECONDS)

        deadline.watch(near)

        assert far.recv(1) == b""


def test_should_ignore_closed_peer_when_shutting_socket() -> None:
    near, far = socket.socketpair()
    far.close()
    with near, _expired_deadline() as deadline:
        deadline.watch(near)

        assert deadline.expired


def test_should_open_fresh_connection_for_each_resend_send() -> None:
    recorder = ResendRecorder()

    send_resend(RESEND_KEY, RENDERED, ADDRESSES, recorder.client(), RUN_KEY)

    assert recorder.requests[0].headers["Connection"] == "close"


@pytest.mark.parametrize(
    ("status", "kind"),
    [
        (401, "resend_key"),
        (307, "resend_redirect"),
        (422, "resend_rejected"),
        (429, "resend_status"),
    ],
    ids=["key", "redirect", "rejected", "other status"],
)
def test_should_keep_http_status_on_resend_failure(status: int, kind: str) -> None:
    recorder = ResendRecorder(status, {"message": "invalid"})

    with pytest.raises(EmailError) as raised:
        send_resend(RESEND_KEY, RENDERED, ADDRESSES, recorder.client(), RUN_KEY)

    assert (raised.value.kind, raised.value.status) == (kind, status)


def test_should_tag_refused_recipient_with_its_code(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    error = smtplib.SMTPRecipientsRefused({ADDRESS: (550, b"no such user")})
    FakeSMTP(fail_on="send_message", error=error).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            SMTP_URL, SMTP_USER, SMTP_PASSWORD, build_message(RENDERED, SENDER, ADDRESS)
        )

    assert (raised.value.kind, raised.value.status) == ("smtp_recipient", 550)


def test_should_name_host_without_trailing_dot_in_reason(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    error = ConnectionRefusedError(61, "refused")
    FakeSMTP(fail_on="connect", error=error).install(monkeypatch)

    with pytest.raises(EmailError) as raised:
        send_smtp(
            "smtp://smtp.example.test.:587",
            SMTP_USER,
            SMTP_PASSWORD,
            build_message(RENDERED, SENDER, ADDRESS),
        )

    assert raised.value.reason == "Could not reach smtp.example.test:587."


@dataclass(frozen=True)
class _Identity:
    cert: Path
    key: Path


@pytest.fixture(scope="module")
def loopback_identity(tmp_path_factory: pytest.TempPathFactory) -> _Identity:
    folder = tmp_path_factory.mktemp("tls")
    key = ec.generate_private_key(ec.SECP256R1())
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, LOOPBACK)])
    now = datetime.now(tz=UTC)
    cert = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(days=1))
        .not_valid_after(now + timedelta(days=1))
        .add_extension(
            x509.SubjectAlternativeName(
                [x509.IPAddress(ipaddress.ip_address(LOOPBACK))]
            ),
            critical=False,
        )
        .add_extension(x509.BasicConstraints(ca=True, path_length=None), critical=True)
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                content_commitment=False,
                key_encipherment=False,
                data_encipherment=False,
                key_agreement=False,
                key_cert_sign=True,
                crl_sign=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .add_extension(
            x509.SubjectKeyIdentifier.from_public_key(key.public_key()), critical=False
        )
        .sign(key, hashes.SHA256())
    )
    identity = _Identity(folder / "cert.pem", folder / "key.pem")
    identity.cert.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    identity.key.write_bytes(
        key.private_bytes(
            serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8,
            serialization.NoEncryption(),
        )
    )
    return identity


class _TlsServer:
    def __init__(self, identity: _Identity) -> None:
        self.context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        self.context.load_cert_chain(identity.cert, identity.key)
        self.ehlo = b""
        self.listener = socket.create_server((LOOPBACK, 0))
        self.port = self.listener.getsockname()[1]
        self.thread = threading.Thread(target=self._serve, daemon=True)
        self.thread.start()

    def _serve(self) -> None:
        self.listener.settimeout(DEADLINE_SLACK_SECONDS)
        try:
            connection, _ = self.listener.accept()
        except OSError:
            return
        connection.settimeout(DEADLINE_SLACK_SECONDS)
        with (
            contextlib.suppress(OSError),
            self.context.wrap_socket(connection, server_side=True) as secure,
        ):
            secure.sendall(b"220 ready\r\n")
            self.ehlo = secure.makefile("rb").readline()

    def url(self) -> str:
        return f"smtps://{LOOPBACK}:{self.port}"


def _trust(monkeypatch: pytest.MonkeyPatch, identity: _Identity) -> None:
    default_context = ssl.create_default_context

    def trusting() -> ssl.SSLContext:
        context = default_context()
        context.load_verify_locations(identity.cert)
        return context

    monkeypatch.setattr(send.ssl, "create_default_context", trusting)


def test_should_greet_tls_server_without_machine_name(
    monkeypatch: pytest.MonkeyPatch, loopback_identity: _Identity
) -> None:
    _trust(monkeypatch, loopback_identity)
    server = _TlsServer(loopback_identity)

    with pytest.raises(EmailError):
        send_smtp(server.url(), None, None, build_message(RENDERED, SENDER, ADDRESS))

    server.thread.join()
    server.listener.close()
    assert server.ehlo == b"ehlo [127.0.0.1]\r\n"


def test_should_refuse_server_with_untrusted_certificate(
    loopback_identity: _Identity,
) -> None:
    server = _TlsServer(loopback_identity)

    with pytest.raises(EmailError) as raised:
        send_smtp(server.url(), None, None, build_message(RENDERED, SENDER, ADDRESS))

    server.thread.join()
    server.listener.close()
    assert raised.value.kind == "tls_certificate"
