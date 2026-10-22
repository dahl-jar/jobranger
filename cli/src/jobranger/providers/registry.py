from __future__ import annotations

from typing import TYPE_CHECKING

from jobranger.providers.anthropic import AnthropicProvider
from jobranger.providers.base import PROVIDER_LABELS, saved_key
from jobranger.providers.models import check_ollama_url
from jobranger.providers.openai_compat import (
    OLLAMA_API_KEY,
    OpenAICompatProvider,
    base_url_for,
)
from jobranger.providers.prices import spec_for

if TYPE_CHECKING:
    from collections.abc import Callable

    from jobranger.providers.base import Provider, ProviderName
    from jobranger.settings import ModelConfig

SECRET_FOR: dict[ProviderName, str] = {
    "anthropic": "anthropic_api_key",
    "openai": "openai_api_key",
    "openrouter": "openrouter_api_key",
}


class ModelNotConfiguredError(Exception):
    def __init__(self) -> None:
        super().__init__("no model chosen, run init to pick a provider and model")


class MissingKeyError(Exception):
    def __init__(self, provider: ProviderName) -> None:
        super().__init__(f"no API key saved for {PROVIDER_LABELS[provider]}")
        self.provider = provider


def provider_from_config(
    model_cfg: ModelConfig, secrets: Callable[[str], str | None]
) -> Provider:
    if model_cfg.provider is None or model_cfg.model is None:
        raise ModelNotConfiguredError
    return provider_for(model_cfg.provider, model_cfg.model, model_cfg, secrets)


def provider_for(
    provider: ProviderName,
    model_id: str,
    model_cfg: ModelConfig,
    secrets: Callable[[str], str | None],
) -> Provider:
    spec = spec_for(provider, model_id, model_cfg.price)
    key = _key(provider, secrets)
    if provider == "ollama":
        check_ollama_url(model_cfg.ollama_url)
    if provider == "anthropic":
        return AnthropicProvider(api_key=key, model=model_id, spec=spec)
    return OpenAICompatProvider(
        kind=provider,
        api_key=key,
        model=model_id,
        base_url=base_url_for(provider, model_cfg.ollama_url),
        spec=spec,
    )


def _key(provider: ProviderName, secrets: Callable[[str], str | None]) -> str:
    secret_name = SECRET_FOR.get(provider)
    if secret_name is None:
        return OLLAMA_API_KEY
    key = saved_key(secrets(secret_name))
    if key is None:
        raise MissingKeyError(provider)
    return key
