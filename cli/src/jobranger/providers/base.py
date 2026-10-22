from __future__ import annotations

import html
import unicodedata
from dataclasses import dataclass
from enum import StrEnum
from typing import Final, Literal, Protocol
from urllib.parse import unquote

from pydantic import BaseModel, TypeAdapter, ValidationError

from jobranger.net.rate_limit import parse_retry_after

type ProviderName = Literal["anthropic", "openai", "openrouter", "ollama"]
type BatchState = Literal["running", "ended", "failed"]

EFFORT: Final = "low"
MAX_OUTPUT_TOKENS = 4096
SDK_MAX_RETRIES = 4
REQUEST_TIMEOUT_SECONDS = 120
RETRY_AFTER_HEADER = "retry-after"
KEY_WORKS = "Key works"
UNREADABLE_REPLY = "{provider} sent a reply jobranger could not read"
DETAIL_MAX_CHARS = 300
KEY_FRAGMENT_CHARS = 12
MIN_KEY_CHARS = KEY_FRAGMENT_CHARS
MAX_DECODE_ROUNDS = 3
KEY_CHAR_MIN = 0x21
KEY_CHAR_MAX = 0x7E
PROVIDER_LABELS: dict[ProviderName, str] = {
    "anthropic": "Anthropic",
    "openai": "OpenAI",
    "openrouter": "OpenRouter",
    "ollama": "Ollama",
}


@dataclass(frozen=True)
class Usage:
    calls: int = 0
    input_tokens: int = 0
    cache_write_tokens: int = 0
    cache_read_tokens: int = 0
    output_tokens: int = 0
    cost_usd: float = 0.0
    priced: bool = True

    @property
    def prompt_tokens(self) -> int:
        return self.input_tokens + self.cache_write_tokens + self.cache_read_tokens

    def __add__(self, other: Usage) -> Usage:
        return Usage(
            calls=self.calls + other.calls,
            input_tokens=self.input_tokens + other.input_tokens,
            cache_write_tokens=self.cache_write_tokens + other.cache_write_tokens,
            cache_read_tokens=self.cache_read_tokens + other.cache_read_tokens,
            output_tokens=self.output_tokens + other.output_tokens,
            cost_usd=self.cost_usd + other.cost_usd,
            priced=self.priced and other.priced,
        )


@dataclass(frozen=True)
class ClassifyPrompt:
    system: str
    user: str


class StopKind(StrEnum):
    done = "done"
    refusal = "refusal"
    truncated = "truncated"
    invalid = "invalid"


@dataclass(frozen=True)
class ProviderResult[SchemaT]:
    text: str
    parsed: SchemaT | None
    usage: Usage
    stop: StopKind


class ProviderErrorKind(StrEnum):
    auth = "auth"
    not_found = "not_found"
    bad_request = "bad_request"
    rate_limit = "rate_limit"
    overloaded = "overloaded"
    server = "server"
    connection = "connection"


PAUSING_KINDS = frozenset({ProviderErrorKind.rate_limit, ProviderErrorKind.overloaded})
KEY_CHECK_MESSAGES = {
    ProviderErrorKind.auth: "{provider} rejected the key",
    ProviderErrorKind.not_found: "{provider} has no model named {model}",
    ProviderErrorKind.bad_request: (
        "{provider} refused the key check. "
        "Check the key and its workspace or project settings"
    ),
}


def saved_key(key: str | None) -> str | None:
    stripped = key.strip() if key else ""
    return stripped or None


def require_key(provider: ProviderName, api_key: str) -> str:
    key = saved_key(api_key)
    label = PROVIDER_LABELS[provider]
    if key is None:
        msg = f"{label} needs an API key"
        raise ValueError(msg)
    if not all(KEY_CHAR_MIN <= ord(char) <= KEY_CHAR_MAX for char in key):
        msg = f"{label} API key must be printable ASCII"
        raise ValueError(msg)
    if len(key) < MIN_KEY_CHARS:
        msg = f"{label} API key is too short"
        raise ValueError(msg)
    return key


def retry_after_seconds(kind: ProviderErrorKind, header: str | None) -> float | None:
    return parse_retry_after(header) if kind in PAUSING_KINDS else None


class ProviderError(Exception):
    def __init__(
        self,
        kind: ProviderErrorKind,
        message: str,
        retry_after: float | None = None,
    ) -> None:
        super().__init__(f"{kind}: {message}")
        self.kind = kind
        self.message = message
        self.retry_after = retry_after


@dataclass(frozen=True)
class KeyCheck:
    ok: bool
    message: str


@dataclass(frozen=True)
class BatchRequest:
    custom_id: str
    prompt: ClassifyPrompt


@dataclass(frozen=True)
class BatchStatus:
    state: BatchState
    done: int
    total: int


class BatchClient(Protocol):
    def submit(self, requests: list[BatchRequest], schema: type[BaseModel]) -> str: ...

    def poll(self, batch_id: str) -> BatchStatus: ...

    def results[SchemaT](
        self, batch_id: str, schema: type[SchemaT]
    ) -> dict[str, ProviderResult[SchemaT]]: ...


class Provider(Protocol):
    @property
    def name(self) -> ProviderName: ...

    @property
    def model(self) -> str: ...

    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]: ...

    def check_key(self) -> KeyCheck: ...

    def batch_client(self) -> BatchClient | None: ...


class ModelReply(BaseModel):
    id: str


def unreadable_reply(provider: ProviderName) -> ProviderError:
    message = UNREADABLE_REPLY.format(provider=PROVIDER_LABELS[provider])
    return ProviderError(ProviderErrorKind.server, message)


def read_reply[ReplyT: BaseModel](
    shape: type[ReplyT], reply: object, provider: ProviderName
) -> ReplyT:
    try:
        return shape.model_validate(reply, from_attributes=True)
    except ValidationError:
        raise unreadable_reply(provider) from None


def provider_error(
    kind: ProviderErrorKind, message: str, header: str | None
) -> ProviderError:
    return ProviderError(kind, message, retry_after_seconds(kind, header))


def rejected_key(
    kind: ProviderErrorKind,
    provider: str,
    model: str,
    body: object,
    api_key: str | None,
) -> KeyCheck | None:
    template = KEY_CHECK_MESSAGES.get(kind)
    if template is None:
        return None
    message = template.format(provider=provider, model=model)
    detail = (
        _error_detail(body, api_key) if kind is ProviderErrorKind.bad_request else None
    )
    if detail is not None:
        message = f"{message}. {provider} said: {detail}"
    return KeyCheck(ok=False, message=message)


def status_line(status: int) -> str:
    return f"HTTP {status}"


def status_message(
    kind: ProviderErrorKind, status: int, body: object, api_key: str | None
) -> str:
    detail = None if kind is ProviderErrorKind.auth else _error_detail(body, api_key)
    line = status_line(status)
    return line if detail is None else f"{line}: {detail}"


def _error_detail(body: object, api_key: str | None) -> str | None:
    error = body.get("error", body) if isinstance(body, dict) else None
    text = error.get("message") if isinstance(error, dict) else None
    if not isinstance(text, str):
        return None
    printable = "".join(char for char in text if char.isprintable() or char.isspace())
    if api_key and _shows_key(printable, api_key):
        return None
    text = " ".join(printable.split())
    if not text:
        return None
    return text[:DETAIL_MAX_CHARS]


def _shows_key(text: str, api_key: str) -> bool:
    plain = _normalize(text)
    key = api_key.lower()
    starts = range(max(len(key) - KEY_FRAGMENT_CHARS + 1, 1))
    return any(key[start : start + KEY_FRAGMENT_CHARS] in plain for start in starts)


def _normalize(text: str) -> str:
    decoded = text
    for _ in range(MAX_DECODE_ROUNDS):
        decoded = unicodedata.normalize("NFKC", html.unescape(unquote(decoded)))
    return "".join(
        char for char in decoded.lower() if char.isprintable() and not char.isspace()
    )


def finish_result[SchemaT](
    text: str, usage: Usage, stop: StopKind | None, schema: type[SchemaT]
) -> ProviderResult[SchemaT]:
    if stop is not None:
        return ProviderResult(text, None, usage, stop)
    try:
        parsed = TypeAdapter(schema).validate_json(text)
    except ValidationError:
        return ProviderResult(text, None, usage, StopKind.invalid)
    return ProviderResult(text, parsed, usage, StopKind.done)
