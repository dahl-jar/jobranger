from __future__ import annotations

import json
import smtplib
from typing import TYPE_CHECKING, Self

import httpx

from jobranger.notify import send
from jobranger.settings import EmailConfig

if TYPE_CHECKING:
    import ssl
    from email.message import EmailMessage

    import pytest

SMTP_READY = 220
RESEND_KEY = "re_fake_key_1234"
SMTP_PASSWORD = "app-password"
ADDRESS = "user@example.com"
SMTP_URL = "smtp://smtp.example.test"
SMTP_USER = ADDRESS
SMTP_EMAIL = EmailConfig(
    on=True, address=ADDRESS, via="smtp", smtp_url=SMTP_URL, smtp_user=SMTP_USER
)
RESEND_EMAIL = EmailConfig(on=True, address=ADDRESS, via="resend")


class FakeSMTP:
    def __init__(
        self,
        offers_starttls: bool = True,
        fail_on: str | None = None,
        error: Exception | None = None,
        greeting: int = 220,
    ) -> None:
        self.offers_starttls = offers_starttls
        self.greeting = greeting
        self.fail_on = fail_on
        self.error = error
        self.calls: list[str] = []
        self.connections: list[tuple[str, str, int]] = []
        self.logins: list[tuple[str, str]] = []
        self.messages: list[EmailMessage] = []
        self.timeouts: list[float] = []
        self.contexts: list[ssl.SSLContext] = []
        self.closed = False

    def install(self, monkeypatch: pytest.MonkeyPatch) -> Self:
        monkeypatch.setattr(send, "GuardedSMTP", self._plain)
        monkeypatch.setattr(send, "GuardedSMTPSSL", self._implicit_tls)
        return self

    def _plain(self, deadline: object, host: str, port: int, timeout: float) -> _Client:
        self.timeouts.append(timeout)
        return self._connect("SMTP", host, port)

    def _implicit_tls(
        self,
        deadline: object,
        host: str,
        port: int,
        timeout: float,
        context: ssl.SSLContext,
    ) -> _Client:
        self.timeouts.append(timeout)
        self.contexts.append(context)
        return self._connect("SMTP_SSL", host, port)

    def _connect(self, kind: str, host: str, port: int) -> _Client:
        self.connections.append((kind, host, port))
        self.step("connect")
        if self.greeting != SMTP_READY:
            raise smtplib.SMTPConnectError(self.greeting, b"greeting")
        return _Client(self)

    def step(self, name: str) -> None:
        self.calls.append(name)
        if name == self.fail_on and self.error is not None:
            raise self.error


class _Client:
    def __init__(self, server: FakeSMTP) -> None:
        self.server = server

    def quit(self) -> None:
        self.server.step("quit")

    def close(self) -> None:
        self.server.closed = True

    def ehlo(self) -> None:
        self.server.step("ehlo")

    def has_extn(self, name: str) -> bool:
        return name.lower() == "starttls" and self.server.offers_starttls

    def starttls(self, context: ssl.SSLContext) -> None:
        self.server.contexts.append(context)
        self.server.step("starttls")

    def login(self, user: str, password: str) -> None:
        self.server.step("login")
        self.server.logins.append((user, password))

    def send_message(self, msg: EmailMessage) -> None:
        self.server.step("send_message")
        self.server.messages.append(msg)


class ResendRecorder:
    def __init__(self, status: int = 200, body: object = None) -> None:
        self.status = status
        self.body = {"id": "email-1"} if body is None else body
        self.requests: list[httpx.Request] = []

    def __call__(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        return httpx.Response(self.status, json=self.body)

    def client(self) -> httpx.Client:
        return httpx.Client(transport=httpx.MockTransport(self))

    def sent(self) -> dict[str, object]:
        return json.loads(self.requests[-1].content)
