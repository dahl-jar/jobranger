from __future__ import annotations

from typing import TYPE_CHECKING

import anthropic
from pydantic import TypeAdapter

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
    ProviderError,
    ProviderErrorKind,
    ProviderName,
    ProviderResult,
    StopKind,
    Usage,
    error_kind,
    finish_result,
    provider_error,
    rejected_key,
    require_key,
)
from jobranger.providers.prices import cost_usd

if TYPE_CHECKING:
    from anthropic.types import Message, OutputConfigParam

    from jobranger.providers.prices import ModelSpec

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
        self._client = client or anthropic.Anthropic(
            api_key=key,
            max_retries=SDK_MAX_RETRIES,
            timeout=REQUEST_TIMEOUT_SECONDS,
        )

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
            raise _provider_error(error) from error
        return _result(message, schema, self._spec)

    def check_key(self) -> KeyCheck:
        try:
            self._client.models.retrieve(self._model)
        except anthropic.APIError as error:
            rejected = rejected_key(
                error_kind(error, ERROR_KINDS),
                PROVIDER_LABELS["anthropic"],
                self._model,
                error.body,
                self._client.api_key,
            )
            if rejected is None:
                raise _provider_error(error) from error
            return rejected
        return KeyCheck(ok=True, message=KEY_WORKS)

    def batch_client(self) -> BatchClient | None:
        return None


def usage_from_anthropic(
    raw: anthropic.types.Usage, spec: ModelSpec | None, batch: bool
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
    message: Message, schema: type[SchemaT], spec: ModelSpec | None
) -> ProviderResult[SchemaT]:
    text = "".join(block.text for block in message.content if block.type == "text")
    usage = usage_from_anthropic(message.usage, spec, batch=False)
    stop = STOP_KINDS.get(message.stop_reason or "")
    return finish_result(text, usage, stop, schema)


def _provider_error(error: anthropic.APIError) -> ProviderError:
    header = (
        error.response.headers.get(RETRY_AFTER_HEADER)
        if isinstance(error, anthropic.APIStatusError)
        else None
    )
    return provider_error(error_kind(error, ERROR_KINDS), error.message, header)
