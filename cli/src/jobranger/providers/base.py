from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import TYPE_CHECKING, Final, Literal, Protocol

from pydantic import TypeAdapter, ValidationError

from jobranger.net.rate_limit import parse_retry_after

if TYPE_CHECKING:
    from pydantic import BaseModel

type ProviderName = Literal["anthropic", "openai", "openrouter", "ollama"]
type BatchState = Literal["running", "ended", "failed"]

EFFORT: Final = "low"
MAX_OUTPUT_TOKENS = 4096
SDK_MAX_RETRIES = 4
REQUEST_TIMEOUT_SECONDS = 120
RETRY_AFTER_HEADER = "retry-after"
KEY_WORKS = "Key works"
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
}


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


def error_kind(
    error: Exception, table: tuple[tuple[type[Exception], ProviderErrorKind], ...]
) -> ProviderErrorKind:
    return next(
        (kind for error_type, kind in table if isinstance(error, error_type)),
        ProviderErrorKind.server,
    )


def provider_error(
    kind: ProviderErrorKind, message: str, header: str | None
) -> ProviderError:
    return ProviderError(kind, message, retry_after_seconds(kind, header))


def rejected_key(kind: ProviderErrorKind, provider: str, model: str) -> KeyCheck | None:
    template = KEY_CHECK_MESSAGES.get(kind)
    if template is None:
        return None
    return KeyCheck(ok=False, message=template.format(provider=provider, model=model))


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
