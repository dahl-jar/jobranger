from __future__ import annotations

import hashlib
from dataclasses import dataclass
from typing import TYPE_CHECKING, Literal

import openai
from openai.lib._parsing._completions import type_to_response_format_param
from pydantic import BaseModel, ConfigDict, ValidationError

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
)
from jobranger.providers.prices import cost_usd

if TYPE_CHECKING:
    from openai.types.chat import ChatCompletion
    from openai.types.completion_usage import CompletionUsage

    from jobranger.providers.prices import ModelSpec

BASE_URLS: dict[ProviderName, str] = {
    "openai": "https://api.openai.com/v1",
    "openrouter": "https://openrouter.ai/api/v1",
}
OLLAMA_API_PATH = "/v1"
OLLAMA_TAGS_PATH = "/api/tags"
OLLAMA_API_KEY = "ollama"
OLLAMA_DEFAULT_TAG = ":latest"
OPENROUTER_KEY_PATH = "/key"
CACHE_KEY_PREFIX = "jobranger-"
CACHE_KEY_HEX_LENGTH = 16
OPENROUTER_BODY: dict[str, object] = {
    "provider": {"require_parameters": True, "data_collection": "deny"}
}
STOP_KINDS = {
    "length": StopKind.truncated,
    "content_filter": StopKind.refusal,
}
ERROR_KINDS: tuple[tuple[type[openai.APIError], ProviderErrorKind], ...] = (
    (openai.AuthenticationError, ProviderErrorKind.auth),
    (openai.PermissionDeniedError, ProviderErrorKind.auth),
    (openai.NotFoundError, ProviderErrorKind.not_found),
    (openai.BadRequestError, ProviderErrorKind.bad_request),
    (openai.UnprocessableEntityError, ProviderErrorKind.bad_request),
    (openai.RateLimitError, ProviderErrorKind.rate_limit),
    (openai.APIConnectionError, ProviderErrorKind.connection),
)

type KeyCheckKind = Literal["model", "openrouter_key", "ollama_tags"]


@dataclass(frozen=True)
class KindRules:
    cache_key: bool
    effort: bool
    zero_temperature: bool
    extra_body: dict[str, object] | None
    reported_cost: bool
    key_check: KeyCheckKind
    batches: bool


KIND_RULES: dict[ProviderName, KindRules] = {
    "openai": KindRules(
        cache_key=True,
        effort=True,
        zero_temperature=False,
        extra_body=None,
        reported_cost=False,
        key_check="model",
        batches=True,
    ),
    "openrouter": KindRules(
        cache_key=False,
        effort=False,
        zero_temperature=False,
        extra_body=OPENROUTER_BODY,
        reported_cost=True,
        key_check="openrouter_key",
        batches=False,
    ),
    "ollama": KindRules(
        cache_key=False,
        effort=False,
        zero_temperature=True,
        extra_body=None,
        reported_cost=False,
        key_check="ollama_tags",
        batches=False,
    ),
}


class OllamaTag(BaseModel):
    model_config = ConfigDict(extra="ignore")

    name: str


class OllamaTags(BaseModel):
    model_config = ConfigDict(extra="ignore")

    models: list[OllamaTag]


def base_url_for(kind: ProviderName, ollama_url: str) -> str:
    if kind == "ollama":
        return f"{ollama_url.rstrip('/')}{OLLAMA_API_PATH}"
    return BASE_URLS[kind]


class OpenAICompatProvider:
    def __init__(
        self,
        kind: ProviderName,
        api_key: str,
        model: str,
        base_url: str,
        spec: ModelSpec | None,
        client: openai.OpenAI | None = None,
    ) -> None:
        self._kind = kind
        self._rules = KIND_RULES[kind]
        self._model = model
        self._base_url = base_url
        self._spec = spec
        self._client = client or openai.OpenAI(
            api_key=api_key,
            base_url=base_url,
            max_retries=SDK_MAX_RETRIES,
            timeout=REQUEST_TIMEOUT_SECONDS,
        )

    @property
    def name(self) -> ProviderName:
        return self._kind

    @property
    def model(self) -> str:
        return self._model

    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]:
        rules = self._rules
        try:
            completion = self._client.chat.completions.create(
                model=self._model,
                messages=[
                    {"role": "system", "content": prompt.system},
                    {"role": "user", "content": prompt.user},
                ],
                response_format=type_to_response_format_param(schema),
                max_completion_tokens=MAX_OUTPUT_TOKENS,
                prompt_cache_key=_cache_key(prompt) if rules.cache_key else openai.omit,
                reasoning_effort=EFFORT if self._sends_effort() else openai.omit,
                temperature=0 if rules.zero_temperature else openai.omit,
                extra_body=rules.extra_body,
            )
        except openai.APIError as error:
            raise _provider_error(error) from error
        return self._result(completion, schema)

    def check_key(self) -> KeyCheck:
        if self._rules.key_check == "ollama_tags":
            return self._check_ollama()
        try:
            if self._rules.key_check == "openrouter_key":
                self._client.get(OPENROUTER_KEY_PATH, cast_to=object)
            else:
                self._client.models.retrieve(self._model)
        except openai.APIError as error:
            rejected = rejected_key(
                error_kind(error, ERROR_KINDS), PROVIDER_LABELS[self._kind], self._model
            )
            if rejected is None:
                raise _provider_error(error) from error
            return rejected
        return KeyCheck(ok=True, message=KEY_WORKS)

    def batch_client(self) -> BatchClient | None:
        return None

    def _sends_effort(self) -> bool:
        return (
            self._rules.effort and self._spec is not None and self._spec.supports_effort
        )

    def _check_ollama(self) -> KeyCheck:
        root = self._base_url.removesuffix(OLLAMA_API_PATH)
        not_running = KeyCheck(
            ok=False,
            message=f"No Ollama answered at {root}. Start it with: ollama serve",
        )
        try:
            raw = self._client.get(f"{root}{OLLAMA_TAGS_PATH}", cast_to=object)
            names = {tag.name for tag in OllamaTags.model_validate(raw).models}
        except (openai.APIConnectionError, openai.APIStatusError, ValidationError):
            return not_running
        if self._model in names or f"{self._model}{OLLAMA_DEFAULT_TAG}" in names:
            return KeyCheck(ok=True, message=f"Ollama has {self._model}")
        return KeyCheck(
            ok=False,
            message=f"Ollama has no model named {self._model}. "
            f"Pull it with: ollama pull {self._model}",
        )

    def _result[SchemaT](
        self, completion: ChatCompletion, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]:
        choice = completion.choices[0]
        message = choice.message
        text = message.content or message.refusal or ""
        usage = self._usage(completion.usage)
        stop = (
            StopKind.refusal
            if message.refusal is not None
            else STOP_KINDS.get(choice.finish_reason)
        )
        return finish_result(text, usage, stop, schema)

    def _usage(self, raw: CompletionUsage | None) -> Usage:
        if raw is None:
            return Usage(calls=1, priced=False)
        reported = None
        if self._rules.reported_cost and raw.model_extra:
            cost = raw.model_extra.get("cost")
            reported = float(cost) if isinstance(cost, int | float) else None
        return usage_from_openai(raw, self._spec, batch=False, reported_cost=reported)


def usage_from_openai(
    raw: CompletionUsage,
    spec: ModelSpec | None,
    batch: bool,
    reported_cost: float | None,
) -> Usage:
    details = raw.prompt_tokens_details
    cached = (details.cached_tokens or 0) if details is not None else 0
    uncached = raw.prompt_tokens - cached
    if reported_cost is not None:
        cost = reported_cost
    elif spec is not None:
        cost = cost_usd(
            spec,
            input_tokens=uncached,
            cache_write_5m=0,
            cache_write_1h=0,
            cache_read=cached,
            output_tokens=raw.completion_tokens,
            batch=batch,
        )
    else:
        cost = 0.0
    return Usage(
        calls=1,
        input_tokens=uncached,
        cache_read_tokens=cached,
        output_tokens=raw.completion_tokens,
        cost_usd=cost,
        priced=reported_cost is not None or spec is not None,
    )


def _cache_key(prompt: ClassifyPrompt) -> str:
    digest = hashlib.sha256(prompt.system.encode()).hexdigest()
    return f"{CACHE_KEY_PREFIX}{digest[:CACHE_KEY_HEX_LENGTH]}"


def _provider_error(error: openai.APIError) -> ProviderError:
    header = (
        error.response.headers.get(RETRY_AFTER_HEADER)
        if isinstance(error, openai.APIStatusError)
        else None
    )
    return provider_error(error_kind(error, ERROR_KINDS), error.message, header)
