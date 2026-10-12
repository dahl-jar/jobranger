from __future__ import annotations

import functools
import math
from dataclasses import dataclass
from typing import TYPE_CHECKING, Annotated, Literal, overload

import anthropic
import httpx
import openai
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from jobranger.providers import anthropic as anthropic_provider
from jobranger.providers import openai_compat
from jobranger.providers.base import (
    PROVIDER_LABELS,
    REQUEST_TIMEOUT_SECONDS,
    RETRY_AFTER_HEADER,
    SDK_MAX_RETRIES,
    ProviderError,
    ProviderErrorKind,
    error_kind,
    provider_error,
    saved_key,
)
from jobranger.providers.openai_compat import (
    BASE_URLS,
    OLLAMA_TAGS_PATH,
    OllamaTags,
)
from jobranger.providers.prices import TOKENS_PER_MTOK, spec_for
from jobranger.providers.urls import ollama_root
from jobranger.settings import INVALID_OLLAMA_URL, is_ollama_url

if TYPE_CHECKING:
    from collections.abc import Callable

    from jobranger.providers.base import ProviderName

ANTHROPIC_API_URL = "https://api.anthropic.com"
OPENAI_MODEL_PREFIX = "gpt-"
OPENAI_EXCLUDED_PARTS = ("audio", "realtime", "transcribe", "tts", "image", "search")
OPENROUTER_MODELS_PATH = "/models"
OPENROUTER_FILTER = {"supported_parameters": "structured_outputs"}
SDK_ERROR_KINDS = anthropic_provider.ERROR_KINDS + openai_compat.ERROR_KINDS
SDK_LISTING_PROVIDERS: frozenset[ProviderName] = frozenset({"anthropic", "openai"})
PRICE_DECIMALS = 6
UNREACHABLE_MESSAGES: dict[ProviderName, str] = {
    "openrouter": "Could not reach OpenRouter at {target}",
    "ollama": "No Ollama answered at {target}. Start it with: ollama serve",
}
HTTP_STATUS_MESSAGE = "{label} at {target} answered HTTP {status}"

type SdkProvider = Literal["anthropic", "openai"]
type SdkClient = anthropic.Anthropic | openai.OpenAI


@dataclass(frozen=True)
class ModelInfo:
    id: str
    name: str
    price_in: float | None
    price_out: float | None
    batch: bool


@dataclass(frozen=True)
class ListRequest:
    key: str | None
    ollama_url: str
    http: httpx.Client
    sdk: SdkClient | None


class OpenRouterPricing(BaseModel):
    model_config = ConfigDict(extra="ignore")

    prompt: float
    completion: float


class OpenRouterModel(BaseModel):
    model_config = ConfigDict(extra="ignore")

    id: Annotated[str, Field(min_length=1)]
    name: str | None = None
    pricing: object = None


class OpenRouterModels(BaseModel):
    model_config = ConfigDict(extra="ignore")

    data: list[object]


def list_models(
    provider: ProviderName,
    key: str | None,
    ollama_url: str,
    http: httpx.Client,
    sdk: SdkClient | None = None,
) -> list[ModelInfo]:
    trimmed = saved_key(key)
    if provider in SDK_LISTING_PROVIDERS and trimmed is None:
        message = f"no API key saved for {PROVIDER_LABELS[provider]}"
        raise ProviderError(ProviderErrorKind.auth, message)
    try:
        models = LISTERS[provider](ListRequest(trimmed, ollama_url, http, sdk))
    except (anthropic.APIError, openai.APIError, httpx.HTTPError) as error:
        raise _listing_error(provider, ollama_url, error) from error
    return sorted(models, key=_price_order)


@overload
def sdk_client(
    provider: Literal["anthropic"], key: str | None
) -> anthropic.Anthropic: ...


@overload
def sdk_client(provider: Literal["openai"], key: str | None) -> openai.OpenAI: ...


def sdk_client(provider: SdkProvider, key: str | None) -> SdkClient:
    if provider == "anthropic":
        return anthropic.Anthropic(
            api_key=key,
            base_url=ANTHROPIC_API_URL,
            max_retries=SDK_MAX_RETRIES,
            timeout=REQUEST_TIMEOUT_SECONDS,
        )
    return openai.OpenAI(
        api_key=key,
        base_url=BASE_URLS["openai"],
        max_retries=SDK_MAX_RETRIES,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )


def check_ollama_url(url: str) -> str:
    root = ollama_root(url)
    if not is_ollama_url(root):
        message = INVALID_OLLAMA_URL.format(url=url)
        raise ProviderError(ProviderErrorKind.connection, message)
    return root


def _listing_error(
    provider: ProviderName,
    ollama_url: str,
    error: anthropic.APIError | openai.APIError | httpx.HTTPError,
) -> ProviderError:
    if isinstance(error, anthropic.APIError | openai.APIError):
        return ProviderError(error_kind(error, SDK_ERROR_KINDS), error.message)
    target = ollama_root(ollama_url) if provider == "ollama" else BASE_URLS[provider]
    if isinstance(error, httpx.HTTPStatusError):
        status = error.response.status_code
        kind = (
            ProviderErrorKind.rate_limit
            if status == httpx.codes.TOO_MANY_REQUESTS
            else ProviderErrorKind.server
        )
        message = HTTP_STATUS_MESSAGE.format(
            label=PROVIDER_LABELS[provider], target=target, status=status
        )
        header = error.response.headers.get(RETRY_AFTER_HEADER)
        return provider_error(kind, message, header)
    message = UNREACHABLE_MESSAGES[provider].format(target=target)
    return ProviderError(ProviderErrorKind.connection, message)


def _sdk_models(provider: SdkProvider, request: ListRequest) -> list[ModelInfo]:
    client = request.sdk or sdk_client(provider, request.key)
    if isinstance(client, anthropic.Anthropic):
        return _anthropic_models(client)
    return _openai_models(client)


def _anthropic_models(client: anthropic.Anthropic) -> list[ModelInfo]:
    return [
        _sdk_info("anthropic", model.id, model.display_name)
        for model in client.models.list()
    ]


def _openai_models(client: openai.OpenAI) -> list[ModelInfo]:
    return [
        _sdk_info("openai", model.id, model.id)
        for model in client.models.list()
        if _is_chat_model(model.id)
    ]


def _openrouter_models(request: ListRequest) -> list[ModelInfo]:
    response = request.http.get(
        f"{BASE_URLS['openrouter']}{OPENROUTER_MODELS_PATH}", params=OPENROUTER_FILTER
    )
    response.raise_for_status()
    try:
        listing = OpenRouterModels.model_validate_json(response.content)
    except ValidationError as error:
        message = "OpenRouter sent a model list jobranger could not read"
        raise ProviderError(ProviderErrorKind.server, message) from error
    return [info for raw in listing.data if (info := _openrouter_info(raw))]


def _openrouter_info(raw: object) -> ModelInfo | None:
    try:
        model = OpenRouterModel.model_validate(raw)
    except ValidationError:
        return None
    name = model.name or model.id
    try:
        pricing = OpenRouterPricing.model_validate(model.pricing)
    except ValidationError:
        return ModelInfo(model.id, name, None, None, batch=False)
    price_in = _per_mtok(pricing.prompt)
    price_out = _per_mtok(pricing.completion)
    if price_in is None or price_out is None:
        price_in = price_out = None
    return ModelInfo(model.id, name, price_in, price_out, batch=False)


def _per_mtok(per_token: float) -> float | None:
    if not math.isfinite(per_token) or per_token < 0:
        return None
    return round(per_token * TOKENS_PER_MTOK, PRICE_DECIMALS)


def _ollama_models(request: ListRequest) -> list[ModelInfo]:
    root = check_ollama_url(request.ollama_url)
    response = request.http.get(f"{root}{OLLAMA_TAGS_PATH}")
    response.raise_for_status()
    try:
        tags = OllamaTags.model_validate_json(response.content)
    except ValidationError as error:
        message = (
            f"{root} answered, but not with an Ollama model list. Check ollama_url"
        )
        raise ProviderError(ProviderErrorKind.server, message) from error
    return [
        ModelInfo(id=tag.name, name=tag.name, price_in=0.0, price_out=0.0, batch=False)
        for tag in tags.models
    ]


def _sdk_info(provider: ProviderName, model_id: str, name: str) -> ModelInfo:
    spec = spec_for(provider, model_id, None)
    return ModelInfo(
        id=model_id,
        name=name,
        price_in=spec.input if spec is not None else None,
        price_out=spec.output if spec is not None else None,
        batch=True,
    )


def _is_chat_model(model_id: str) -> bool:
    return model_id.startswith(OPENAI_MODEL_PREFIX) and not any(
        part in model_id for part in OPENAI_EXCLUDED_PARTS
    )


def _price_order(model: ModelInfo) -> tuple[bool, float]:
    return (model.price_in is None, model.price_in or 0.0)


LISTERS: dict[ProviderName, Callable[[ListRequest], list[ModelInfo]]] = {
    "anthropic": functools.partial(_sdk_models, "anthropic"),
    "openai": functools.partial(_sdk_models, "openai"),
    "openrouter": _openrouter_models,
    "ollama": _ollama_models,
}
