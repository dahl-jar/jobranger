from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from http import HTTPStatus
from typing import TYPE_CHECKING, Annotated, Literal

import openai
from openai.lib._parsing._completions import type_to_response_format_param
from pydantic import BaseModel, ConfigDict, Field, ValidationError

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
from jobranger.providers.urls import OLLAMA_API_PATH, ollama_root, without_userinfo

if TYPE_CHECKING:
    from jobranger.providers.prices import ModelSpec

BASE_URLS: dict[ProviderName, str] = {
    "openai": "https://api.openai.com/v1",
    "openrouter": "https://openrouter.ai/api/v1",
}
OFFICIAL_URLS = frozenset(f"{url}/" for url in BASE_URLS.values())
OLLAMA_TAGS_PATH = "/api/tags"
OLLAMA_API_KEY = "ollama"
OLLAMA_DEFAULT_TAG = ":latest"
OPENROUTER_KEY_PATH = "/key"
CACHE_KEY_PREFIX = "jobranger-"
CACHE_KEY_HEX_LENGTH = 16
OPENROUTER_BODY: dict[str, object] = {
    "provider": {"require_parameters": True, "data_collection": "deny"}
}
STOP_KINDS: dict[str | None, StopKind] = {
    "length": StopKind.truncated,
    "content_filter": StopKind.refusal,
}
STATUS_KINDS: dict[int, ProviderErrorKind] = {
    HTTPStatus.BAD_REQUEST: ProviderErrorKind.bad_request,
    HTTPStatus.UNAUTHORIZED: ProviderErrorKind.auth,
    HTTPStatus.FORBIDDEN: ProviderErrorKind.auth,
    HTTPStatus.NOT_FOUND: ProviderErrorKind.not_found,
    HTTPStatus.REQUEST_ENTITY_TOO_LARGE: ProviderErrorKind.bad_request,
    HTTPStatus.UNPROCESSABLE_ENTITY: ProviderErrorKind.bad_request,
    HTTPStatus.TOO_MANY_REQUESTS: ProviderErrorKind.rate_limit,
}

OLLAMA_STATUS_MESSAGES = {
    ProviderErrorKind.auth: (
        "Ollama at {root} refused the request (HTTP {status}). "
        "Check its access settings"
    ),
    ProviderErrorKind.rate_limit: (
        "Ollama at {root} is limiting requests (HTTP {status}). Try again later"
    ),
    ProviderErrorKind.server: (
        "Ollama at {root} failed (HTTP {status}). Check the Ollama logs"
    ),
}
OLLAMA_OTHER_STATUS = "Ollama at {root} answered HTTP {status}. Check ollama_url"
OLLAMA_NOT_RUNNING = "No Ollama answered at {root}. Start it with: ollama serve"

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


class ChoiceMessage(BaseModel):
    content: str | None = None
    refusal: str | None = None


class CompletionChoice(BaseModel):
    message: ChoiceMessage
    finish_reason: str | None = None


class PromptDetails(BaseModel):
    cached_tokens: int | None = None


class TokenUsage(BaseModel):
    prompt_tokens: int
    completion_tokens: int
    prompt_tokens_details: PromptDetails | None = None
    cost: object = None


class CompletionReply(BaseModel):
    choices: Annotated[list[CompletionChoice], Field(min_length=1)]
    usage: TokenUsage | None = None


class OpenRouterKey(BaseModel):
    data: dict[str, object]


def new_client(key: str, base_url: str) -> openai.OpenAI:
    client = openai.OpenAI(
        api_key=key,
        base_url=base_url,
        max_retries=SDK_MAX_RETRIES,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )
    client.organization = None
    client.project = None
    client._custom_headers = {}
    return client


def status_kind(status: int) -> ProviderErrorKind:
    return STATUS_KINDS.get(status, ProviderErrorKind.server)


def as_provider_error(error: openai.APIError, client: openai.OpenAI) -> ProviderError:
    if isinstance(error, openai.APIStatusError):
        kind = status_kind(error.status_code)
        body = _visible_body(error, client)
        message = status_message(kind, error.status_code, body, client.api_key)
        header = error.response.headers.get(RETRY_AFTER_HEADER)
        return provider_error(kind, message, header)
    kind = (
        ProviderErrorKind.connection
        if isinstance(error, openai.APIConnectionError)
        else ProviderErrorKind.server
    )
    return provider_error(kind, error.message, None)


def base_url_for(kind: ProviderName, ollama_url: str) -> str:
    if kind == "ollama":
        return f"{ollama_root(ollama_url)}{OLLAMA_API_PATH}"
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
        key = OLLAMA_API_KEY if kind == "ollama" else require_key(kind, api_key)
        self._kind = kind
        self._rules = KIND_RULES[kind]
        self._model = model
        self._base_url = base_url
        self._spec = spec
        self._client = client or new_client(key, base_url)

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
            raise as_provider_error(error, self._client) from None
        except json.JSONDecodeError:
            raise unreadable_reply(self._kind) from None
        reply = read_reply(CompletionReply, completion, self._kind)
        return self._result(reply, schema)

    def check_key(self) -> KeyCheck:
        if self._rules.key_check == "ollama_tags":
            return self._check_ollama()
        shape: type[BaseModel] = ModelReply
        try:
            if self._rules.key_check == "openrouter_key":
                shape = OpenRouterKey
                reply = self._client.get(OPENROUTER_KEY_PATH, cast_to=object)
            else:
                reply = self._client.models.retrieve(self._model)
        except json.JSONDecodeError:
            raise unreadable_reply(self._kind) from None
        except openai.APIError as error:
            converted = as_provider_error(error, self._client)
            rejected = rejected_key(
                converted.kind,
                PROVIDER_LABELS[self._kind],
                self._model,
                _visible_body(error, self._client),
                self._client.api_key,
            )
            if rejected is None:
                raise converted from None
            return rejected
        read_reply(shape, reply, self._kind)
        return KeyCheck(ok=True, message=KEY_WORKS)

    def batch_client(self) -> BatchClient | None:
        return None

    def _sends_effort(self) -> bool:
        return (
            self._rules.effort and self._spec is not None and self._spec.supports_effort
        )

    def _check_ollama(self) -> KeyCheck:
        root = self._base_url.removesuffix(OLLAMA_API_PATH)
        shown = without_userinfo(root)
        not_running = KeyCheck(ok=False, message=OLLAMA_NOT_RUNNING.format(root=shown))
        try:
            raw = self._client.get(f"{root}{OLLAMA_TAGS_PATH}", cast_to=object)
            names = {tag.name for tag in OllamaTags.model_validate(raw).models}
        except openai.APIStatusError as error:
            return KeyCheck(ok=False, message=_ollama_status(shown, error.status_code))
        except (openai.APIConnectionError, ValidationError, json.JSONDecodeError):
            return not_running
        if self._model in names or f"{self._model}{OLLAMA_DEFAULT_TAG}" in names:
            return KeyCheck(ok=True, message=f"Ollama has {self._model}")
        return KeyCheck(
            ok=False,
            message=f"Ollama has no model named {self._model}. "
            f"Pull it with: ollama pull {self._model}",
        )

    def _result[SchemaT](
        self, completion: CompletionReply, schema: type[SchemaT]
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

    def _usage(self, raw: TokenUsage | None) -> Usage:
        if raw is None:
            return Usage(calls=1, priced=False)
        reported = None
        if self._rules.reported_cost and isinstance(raw.cost, int | float):
            reported = float(raw.cost)
        return usage_from_openai(raw, self._spec, batch=False, reported_cost=reported)


def usage_from_openai(
    raw: TokenUsage,
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


def _visible_body(error: openai.APIError, client: openai.OpenAI) -> object:
    return error.body if str(client.base_url) in OFFICIAL_URLS else None


def _ollama_status(root: str, status: int) -> str:
    template = OLLAMA_STATUS_MESSAGES.get(status_kind(status), OLLAMA_OTHER_STATUS)
    return template.format(root=root, status=status)


def _cache_key(prompt: ClassifyPrompt) -> str:
    digest = hashlib.sha256(prompt.system.encode()).hexdigest()
    return f"{CACHE_KEY_PREFIX}{digest[:CACHE_KEY_HEX_LENGTH]}"
