from __future__ import annotations

import base64
import ipaddress
import json
import socket
import threading
import traceback
from pathlib import Path
from typing import TYPE_CHECKING

import anthropic
import httpx
import httpx2
import openai
from keyring.backend import KeyringBackend
from keyring.errors import PasswordDeleteError

from jobranger.ads.models import Ad
from jobranger.catalogue import EmployerFeed, source_plan
from jobranger.events import CancelToken, RunEvent
from jobranger.matching.assessment import AdAssessment
from jobranger.matching.classify import Classification
from jobranger.matching.rules import Match
from jobranger.providers.base import (
    ClassifyPrompt,
    ProviderError,
    ProviderResult,
    StopKind,
    Usage,
)
from jobranger.settings import Limits, Targets
from jobranger.sources.base import FetchContext

if TYPE_CHECKING:
    from collections.abc import Callable

    import pytest

FIXTURES = Path(__file__).parent / "fixtures"
OLLAMA_URL = "http://box:11434"
OLLAMA_PASSWORD = "pass-1234"
CREDENTIAL_OLLAMA_URL = f"http://user:{OLLAMA_PASSWORD}@box:11434"
INVALID_URL = "http://[::1"
ROUTER_MODEL = "vendor/model-one"
FAKE_VERSION = "test-version"
GUIDANCE = "Judge roles by the actual work.\n\nPrefer backend roles."
OUTDATED_VERSION = "old"
ANTHROPIC_TEST_KEY = "Fake-Anthropic-Key-For-Unit-Tests"
OPENAI_TEST_KEY = "fake-openai-key"
BLANK_KEYS = ("", "   ")
BLANK_KEY_IDS = ("empty", "spaces")
NON_ASCII_KEYS = ("fake\u00a0key", "fake\u200bkey", "fak\u00eb-key")
NON_ASCII_KEY_IDS = ("nbsp", "zero-width space", "accented letter")
SDK_ENV_VARIABLES = ("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL")
SHORT_RETRY_AFTER = {"retry-after-ms": "1"}
ELSEWHERE_URL = "http://elsewhere.example.test"
JSON_CONTENT_TYPE = {"content-type": "application/json"}
MASKED_KEY_MESSAGE = "Incorrect API key provided: sk-proj-****abcd"
ENV_HEADERS = "x-env-header: leaked\nAuthorization: Bearer env-token"
SDK_HEADER_ENV = {
    "ANTHROPIC_CUSTOM_HEADERS": ENV_HEADERS,
    "OPENAI_CUSTOM_HEADERS": ENV_HEADERS,
    "OPENAI_ORG_ID": "org-env",
    "OPENAI_PROJECT_ID": "proj-env",
}
CREDENTIAL_HEADERS = (
    "authorization",
    "x-api-key",
    "x-env-header",
    "openai-organization",
    "openai-project",
)
ALL_ROLES = (
    "backend",
    "frontend",
    "fullstack",
    "mobile",
    "data_engineering",
    "devops_platform",
    "ml_ai",
    "embedded",
    "game",
)
FAKE_LIMITS = Limits(
    levels=frozenset({"junior", "mid", "none"}),
    years=frozenset({"0-1"}),
    hide_masters=True,
)
CALL_USAGE = Usage(calls=1, input_tokens=100, output_tokens=20, cost_usd=0.01)
ASHBY_BOARD = EmployerFeed(
    kind="ashby", key="example-one", company="Org One AS", board="Org One AS"
)
TEAMTAILOR_FEED = EmployerFeed(
    kind="teamtailor",
    key="example",
    company="Example AS",
    board="https://careers.example.test/jobs.rss?per_page=200",
)
OTHER_TEAMTAILOR_FEED = EmployerFeed(
    kind="teamtailor",
    key="org-two",
    company="Org Two AS",
    board="https://careers.example.test/rss",
)


def targets(**overrides: object) -> Targets:
    base: dict[str, object] = {"subfields": ALL_ROLES, "limits": FAKE_LIMITS}
    base.update(overrides)
    return Targets.model_validate(base)


def classification_payload(**overrides: object) -> dict[str, object]:
    payload = json.loads((FIXTURES / "classification.json").read_text())
    payload.update(overrides)
    return payload


def make_assessment(**overrides: object) -> AdAssessment:
    return AdAssessment.model_validate(classification_payload(**overrides))


def make_match(**overrides: object) -> Match:
    base = dict(classification_payload()["match"], version=FAKE_VERSION)
    for name in ("evidence", "gaps", "blockers"):
        base[name] = tuple(base[name])
    base.update(overrides)
    return Match(**base)


def make_classification(**overrides: object) -> Classification:
    base = classification_payload()
    for name in ("technologies", "requirements"):
        base[name] = tuple(base[name])
    base["match"] = make_match()
    base["checks"] = ()
    base.update(overrides)
    return Classification(**base)


def make_ad(**overrides: object) -> Ad:
    base: dict[str, object] = {
        "id": "finn:1234",
        "source": "finn",
        "source_id": "1234",
        "title": "Utvikler",
        "company": "Org One AS",
        "location": "Bergen",
        "published": "2026-08-24T00:00:00+00:00",
        "deadline": None,
        "url": "https://www.finn.no/job/ad/1234",
        "description": None,
        "label": None,
        "is_programming_job": True,
        "display_title": "Software Developer",
        "role_type": "general_software",
        "technologies": (),
        "requirements": (),
        "match": make_match(),
    }
    base.update(overrides)
    return Ad(**base)


def make_search_html(
    docs: list[dict[str, object]], match_count: int, last_page: int = 1
) -> str:
    state = {
        "queries": [
            {
                "state": {
                    "data": {
                        "docs": docs,
                        "metadata": {
                            "result_size": {"match_count": match_count},
                            "paging": {"current": 1, "last": last_page},
                        },
                    }
                }
            }
        ]
    }
    encoded = base64.b64encode(json.dumps(state).encode()).decode()
    return f'<html><script data-react-query-state="1">{encoded}</script></html>'


def make_search_doc(source_id: int, **overrides: object) -> dict[str, object]:
    doc: dict[str, object] = {
        "id": source_id,
        "heading": f"Utvikler {source_id}",
        "company_name": "Org One AS",
        "location": "Bergen",
        "published": 1789394781000,
        "deadline": None,
        "canonical_url": f"https://www.finn.no/job/ad/{source_id}",
    }
    doc.update(overrides)
    return doc


class FakeClock:
    def __init__(self) -> None:
        self.now = 0.0
        self.sleeps: list[float] = []

    def __call__(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds


type Reply = (
    AdAssessment | ProviderError | StopKind | Exception | tuple[StopKind, AdAssessment]
)


class FakeProvider:
    def __init__(self, replies: list[Reply] | None = None) -> None:
        self.replies = list(replies or [])
        self.prompts: list[ClassifyPrompt] = []
        self.schemas: list[type] = []

    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]:
        self.prompts.append(prompt)
        self.schemas.append(schema)
        reply = self.replies.pop(0) if self.replies else make_assessment()
        if isinstance(reply, tuple):
            stop, body = reply
            return ProviderResult(body.model_dump_json(), body, CALL_USAGE, stop)
        if isinstance(reply, Exception):
            raise reply
        if isinstance(reply, StopKind):
            return ProviderResult("", None, CALL_USAGE, reply)
        return ProviderResult(reply.model_dump_json(), reply, CALL_USAGE, StopKind.done)


class MemoryKeyring(KeyringBackend):
    priority = 0

    def __init__(self) -> None:
        super().__init__()
        self.passwords: dict[tuple[str, str], str] = {}

    def get_password(self, service: str, username: str) -> str | None:
        return self.passwords.get((service, username))

    def set_password(self, service: str, username: str, password: str) -> None:
        self.passwords[(service, username)] = password

    def delete_password(self, service: str, username: str) -> None:
        if self.passwords.pop((service, username), None) is None:
            raise PasswordDeleteError(username)


def anthropic_client(
    handler: Callable[[httpx2.Request], httpx2.Response],
    base_url: str = "https://api.anthropic.com",
) -> anthropic.Anthropic:
    transport = httpx2.MockTransport(handler)
    return anthropic.Anthropic(
        api_key=ANTHROPIC_TEST_KEY,
        base_url=base_url,
        max_retries=0,
        http_client=httpx2.Client(transport=transport),
    )


def anthropic_message(
    text: str, stop_reason: str = "end_turn", **usage: object
) -> dict[str, object]:
    message = json.loads((FIXTURES / "anthropic_message.json").read_text())
    message["content"][0]["text"] = text
    message["stop_reason"] = stop_reason
    message["usage"].update(usage)
    return message


def anthropic_model(model_id: str) -> dict[str, object]:
    return {"id": model_id, "type": "model"}


def openai_client(
    handler: Callable[[httpx2.Request], httpx2.Response],
    base_url: str = "https://api.openai.com/v1",
) -> openai.OpenAI:
    return openai.OpenAI(
        api_key=OPENAI_TEST_KEY,
        base_url=base_url,
        max_retries=0,
        http_client=httpx2.Client(transport=httpx2.MockTransport(handler)),
    )


def openai_completion(
    fixture: str, content: str, **choice: object
) -> dict[str, object]:
    body = json.loads((FIXTURES / fixture).read_text())
    body["choices"][0]["message"]["content"] = content
    body["choices"][0].update(choice)
    return body


PROMPT = ClassifyPrompt(system="rules and profile", user="ad text")


class Recorder:
    def __init__(self, status: int, body: object, **headers: str) -> None:
        self.status = status
        self.body = body
        self.headers = headers
        self.requests: list[httpx2.Request] = []

    def __call__(self, request: httpx2.Request) -> httpx2.Response:
        self.requests.append(request)
        return httpx2.Response(self.status, json=self.body, headers=self.headers)

    def sent(self) -> dict[str, object]:
        return json.loads(self.requests[-1].content)


def unreadable_replies(
    wrong_types: object,
) -> dict[str, Callable[[httpx2.Request], httpx2.Response]]:
    return {
        "junk json": lambda request: httpx2.Response(200, json={"unexpected": True}),
        "wrong types": lambda request: httpx2.Response(200, json=wrong_types),
        "invalid json": lambda request: httpx2.Response(
            200, content=b"not json", headers=JSON_CONTENT_TYPE
        ),
        "plain text": lambda request: httpx2.Response(200, text="hello"),
    }


def refuse_connection(request: httpx2.Request) -> httpx2.Response:
    raise httpx2.ConnectError("connection refused", request=request)


def route_sdk_network(monkeypatch: pytest.MonkeyPatch, recorder: Recorder) -> None:
    for name in SDK_ENV_VARIABLES:
        monkeypatch.delenv(name, raising=False)
    monkeypatch.setattr(
        httpx2.HTTPTransport,
        "handle_request",
        lambda _transport, request: recorder(request),
    )


LOOPBACK_NAMES = frozenset({"localhost"})
INET_FAMILIES = frozenset({socket.AF_INET, socket.AF_INET6})
SCOPE_SEPARATOR = "%"
HOST_CODEC = "idna"
PROXY_VARIABLES = (
    "HTTP_PROXY",
    "HTTPS_PROXY",
    "ALL_PROXY",
    "http_proxy",
    "https_proxy",
    "all_proxy",
)
NO_PROXY_HOSTS = "*"


class NetworkBlockedError(RuntimeError):
    def __init__(self, host: object) -> None:
        super().__init__(f"tests may only reach loopback, blocked {host!r}")


def is_loopback(host: object) -> bool:
    if host is None:
        return True
    name = host.decode() if isinstance(host, bytes) else str(host)
    if name in LOOPBACK_NAMES:
        return True
    try:
        return ipaddress.ip_address(name.split(SCOPE_SEPARATOR)[0]).is_loopback
    except ValueError:
        return False


def guard_network(patch: pytest.MonkeyPatch) -> None:
    real_getaddrinfo = socket.getaddrinfo
    real_connect = socket.socket.connect
    real_connect_ex = socket.socket.connect_ex

    def require_loopback(sock: socket.socket, address: object) -> None:
        if sock.family in INET_FAMILIES and isinstance(address, tuple):
            host = address[0]
            if not is_loopback(host):
                raise NetworkBlockedError(host)

    def getaddrinfo(host: object, *args: object, **kwargs: object) -> object:
        encoded = host.encode(HOST_CODEC) if isinstance(host, str) else host
        if not is_loopback(encoded):
            raise NetworkBlockedError(host)
        return real_getaddrinfo(host, *args, **kwargs)

    def connect(sock: socket.socket, address: object) -> None:
        require_loopback(sock, address)
        real_connect(sock, address)

    def connect_ex(sock: socket.socket, address: object) -> int:
        require_loopback(sock, address)
        return real_connect_ex(sock, address)

    for name in PROXY_VARIABLES:
        patch.delenv(name, raising=False)
    patch.setenv("NO_PROXY", NO_PROXY_HOSTS)
    patch.setattr(socket, "getaddrinfo", getaddrinfo)
    patch.setattr(socket.socket, "connect", connect)
    patch.setattr(socket.socket, "connect_ex", connect_ex)


def traceback_text(error: BaseException) -> str:
    return "".join(traceback.format_exception(error))


def redirect_sdk_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("ANTHROPIC_BASE_URL", ELSEWHERE_URL)
    monkeypatch.setenv("OPENAI_BASE_URL", ELSEWHERE_URL)


def set_sdk_header_env(monkeypatch: pytest.MonkeyPatch) -> None:
    for name, value in SDK_HEADER_ENV.items():
        monkeypatch.setenv(name, value)


def credential_headers(request: httpx2.Request) -> dict[str, str]:
    return {
        name: request.headers[name]
        for name in CREDENTIAL_HEADERS
        if name in request.headers
    }


class ListSink:
    def __init__(self) -> None:
        self.events: list[RunEvent] = []
        self._lock = threading.Lock()

    def emit(self, event: RunEvent) -> None:
        with self._lock:
            self.events.append(event)


RUN_STARTED = "2026-10-07T03:00:00+00:00"


def fetch_context(http: httpx.Client, **overrides: object) -> FetchContext:
    base: dict[str, object] = {
        "http": http,
        "plan": source_plan(targets()),
        "run_started": RUN_STARTED,
        "known_ids": frozenset(),
        "cursors": {},
        "cancel": CancelToken(),
    }
    base.update(overrides)
    return FetchContext(**base)
