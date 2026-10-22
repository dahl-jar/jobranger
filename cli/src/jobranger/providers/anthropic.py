from __future__ import annotations

import json
from typing import TYPE_CHECKING

import anthropic
from pydantic import BaseModel, TypeAdapter

from jobranger.providers.base import (
    EFFORT,
    KEY_WORKS,
    MAX_OUTPUT_TOKENS,
    PROVIDER_LABELS,
    REQUEST_TIMEOUT_SECONDS,
    RETRY_AFTER_HEADER,
    SDK_MAX_RETRIES,
    BatchClient,
    ClassifyPrompt,
    KeyCheck,
    ModelReply,
    ProviderError,
    ProviderErrorKind,
    ProviderName,
    ProviderResult,
    StopKind,
    Usage,
    finish_result,
    provider_error,
    read_reply,
    rejected_key,
    require_key,
    status_message,
    unreadable_reply,
)
from jobranger.providers.prices import cost_usd

if TYPE_CHECKING:
    from anthropic.types import OutputConfigParam

    from jobranger.providers.prices import ModelSpec

ANTHROPIC_API_URL = "https://api.anthropic.com"
STOP_KINDS = {
    "refusal": StopKind.refusal,
    "max_tokens": StopKind.truncated,
    "model_context_window_exceeded": StopKind.truncated,
}
ERROR_KINDS: tuple[tuple[type[anthropic.APIError], ProviderErrorKind], ...] = (
    (anthropic.AuthenticationError, ProviderErrorKind.auth),
    (anthropic.PermissionDeniedError, ProviderErrorKind.auth),
    (anthropic.NotFoundError, ProviderErrorKind.not_found),
    (anthropic.BadRequestError, ProviderErrorKind.bad_request),
    (anthropic.UnprocessableEntityError, ProviderErrorKind.bad_request),
    (anthropic.RequestTooLargeError, ProviderErrorKind.bad_request),
    (anthropic.RateLimitError, ProviderErrorKind.rate_limit),
    (anthropic.OverloadedError, ProviderErrorKind.overloaded),
    (anthropic.APIConnectionError, ProviderErrorKind.connection),
)


class ContentBlock(BaseModel):
    type: str
    text: str = ""


class CacheWrites(BaseModel):
    ephemeral_5m_input_tokens: int
    ephemeral_1h_input_tokens: int


class MessageUsage(BaseModel):
    input_tokens: int
    output_tokens: int
    cache_creation: CacheWrites | None = None
    cache_creation_input_tokens: int | None = None
    cache_read_input_tokens: int | None = None


class MessageReply(BaseModel):
    content: list[ContentBlock]
    usage: MessageUsage
    stop_reason: str | None = None


class AnthropicProvider:
    def __init__(
        self,
        api_key: str,
        model: str,
        spec: ModelSpec | None,
        client: anthropic.Anthropic | None = None,
    ) -> None:
        key = require_key("anthropic", api_key)
        self._model = model
        self._spec = spec
        self._client = client or new_client(key)

    @property
    def name(self) -> ProviderName:
        return "anthropic"

    @property
    def model(self) -> str:
        return self._model

    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]:
        try:
            message = self._client.messages.create(
                model=self._model,
                max_tokens=MAX_OUTPUT_TOKENS,
                system=[
                    {
                        "type": "text",
                        "text": prompt.system,
                        "cache_control": {"type": "ephemeral"},
                    }
                ],
                messages=[{"role": "user", "content": prompt.user}],
                output_config=_output_config(schema, self._spec),
            )
        except anthropic.APIError as error:
            raise as_provider_error(error, self._client) from None
        except json.JSONDecodeError:
            raise unreadable_reply("anthropic") from None
        reply = read_reply(MessageReply, message, "anthropic")
        return _result(reply, schema, self._spec)

    def check_key(self) -> KeyCheck:
        try:
            model = self._client.models.retrieve(self._model)
        except json.JSONDecodeError:
            raise unreadable_reply("anthropic") from None
        except anthropic.APIError as error:
            converted = as_provider_error(error, self._client)
            rejected = rejected_key(
                converted.kind,
                PROVIDER_LABELS["anthropic"],
                self._model,
                _visible_body(error, self._client),
                self._client.api_key,
            )
            if rejected is None:
                raise converted from None
            return rejected
        read_reply(ModelReply, model, "anthropic")
        return KeyCheck(ok=True, message=KEY_WORKS)

    def batch_client(self) -> BatchClient | None:
        return None


def new_client(key: str) -> anthropic.Anthropic:
    client = anthropic.Anthropic(
        api_key=key,
        base_url=ANTHROPIC_API_URL,
        max_retries=SDK_MAX_RETRIES,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )
    client._custom_headers = {}
    return client


def as_provider_error(
    error: anthropic.APIError, client: anthropic.Anthropic
) -> ProviderError:
    kind = _error_kind(error)
    if not isinstance(error, anthropic.APIStatusError):
        return provider_error(kind, error.message, None)
    body = _visible_body(error, client)
    message = status_message(kind, error.status_code, body, client.api_key)
    return provider_error(kind, message, error.response.headers.get(RETRY_AFTER_HEADER))


def _error_kind(error: anthropic.APIError) -> ProviderErrorKind:
    return next(
        (kind for error_type, kind in ERROR_KINDS if isinstance(error, error_type)),
        ProviderErrorKind.server,
    )


def _visible_body(error: anthropic.APIError, client: anthropic.Anthropic) -> object:
    return error.body if str(client.base_url) == ANTHROPIC_API_URL else None


def usage_from_anthropic(
    raw: MessageUsage, spec: ModelSpec | None, batch: bool
) -> Usage:
    if raw.cache_creation is not None:
        write_5m = raw.cache_creation.ephemeral_5m_input_tokens
        write_1h = raw.cache_creation.ephemeral_1h_input_tokens
    else:
        write_5m = raw.cache_creation_input_tokens or 0
        write_1h = 0
    cache_read = raw.cache_read_input_tokens or 0
    cost = (
        cost_usd(
            spec,
            input_tokens=raw.input_tokens,
            cache_write_5m=write_5m,
            cache_write_1h=write_1h,
            cache_read=cache_read,
            output_tokens=raw.output_tokens,
            batch=batch,
        )
        if spec is not None
        else 0.0
    )
    return Usage(
        calls=1,
        input_tokens=raw.input_tokens,
        cache_write_tokens=write_5m + write_1h,
        cache_read_tokens=cache_read,
        output_tokens=raw.output_tokens,
        cost_usd=cost,
        priced=spec is not None,
    )


def _output_config(schema: type[object], spec: ModelSpec | None) -> OutputConfigParam:
    config: OutputConfigParam = {
        "format": {
            "type": "json_schema",
            "schema": anthropic.transform_schema(TypeAdapter(schema).json_schema()),
        }
    }
    if spec is not None and spec.supports_effort:
        config["effort"] = EFFORT
    return config


def _result[SchemaT](
    message: MessageReply, schema: type[SchemaT], spec: ModelSpec | None
) -> ProviderResult[SchemaT]:
    text = "".join(block.text for block in message.content if block.type == "text")
    usage = usage_from_anthropic(message.usage, spec, batch=False)
    stop = STOP_KINDS.get(message.stop_reason or "")
    return finish_result(text, usage, stop, schema)
