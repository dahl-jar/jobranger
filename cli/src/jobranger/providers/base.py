from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import Literal, Protocol

type ProviderName = Literal["anthropic", "openai", "openrouter", "ollama"]


@dataclass(frozen=True)
class Usage:
    calls: int = 0
    input_tokens: int = 0
    cache_write_tokens: int = 0
    cache_read_tokens: int = 0
    output_tokens: int = 0
    cost_usd: float = 0.0

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


class ProviderError(Exception):
    def __init__(self, kind: ProviderErrorKind, message: str) -> None:
        super().__init__(f"{kind}: {message}")
        self.kind = kind
        self.message = message


class Provider(Protocol):
    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]: ...
