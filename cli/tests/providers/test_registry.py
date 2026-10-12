from __future__ import annotations

from typing import TYPE_CHECKING

import pytest
from factories import INVALID_URL, OLLAMA_URL, ROUTER_MODEL

from jobranger.providers.base import ProviderError, ProviderErrorKind
from jobranger.providers.registry import (
    MissingKeyError,
    ModelNotConfiguredError,
    provider_for,
    provider_from_config,
)
from jobranger.settings import ModelConfig, PriceOverride

if TYPE_CHECKING:
    from jobranger.providers.base import ProviderName

HAIKU = "claude-haiku-5-5"
HAIKU_CONFIG = ModelConfig(provider="anthropic", model=HAIKU)
PRICE = PriceOverride(input_per_mtok=4.0, output_per_mtok=20.0)
ELSEWHERE = "http://elsewhere.example.test"
SAVED_KEYS = {
    "anthropic_api_key": "sk-anthropic",
    "openai_api_key": "sk-openai",
    "openrouter_api_key": "sk-openrouter",
}


@pytest.fixture(autouse=True)
def redirecting_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("NO_PROXY", "*")
    monkeypatch.setenv("ANTHROPIC_BASE_URL", ELSEWHERE)
    monkeypatch.setenv("OPENAI_BASE_URL", ELSEWHERE)


@pytest.mark.parametrize(("provider", "model"), [(None, HAIKU), ("anthropic", None)])
def test_should_ask_to_run_init_when_no_model_is_chosen(
    provider: ProviderName | None, model: str | None
) -> None:
    config = HAIKU_CONFIG.model_copy(update={"provider": provider, "model": model})

    with pytest.raises(
        ModelNotConfiguredError,
        match="^no model chosen, run init to pick a provider and model$",
    ):
        provider_from_config(config, SAVED_KEYS.get)


@pytest.mark.parametrize("secret", [None, "   "])
def test_should_raise_missing_key_when_no_key_is_saved(secret: str | None) -> None:
    config = HAIKU_CONFIG.model_copy(
        update={"provider": "openrouter", "model": ROUTER_MODEL}
    )

    with pytest.raises(
        MissingKeyError, match="^no API key saved for OpenRouter$"
    ) as caught:
        provider_from_config(config, {"openrouter_api_key": secret}.get)

    assert caught.value.provider == "openrouter"


@pytest.mark.parametrize(
    ("provider", "model", "api_key", "base_url", "price_in"),
    [
        ("anthropic", HAIKU, "sk-anthropic", "https://api.anthropic.com", 4.0),
        ("openai", "gpt-5-mini", "sk-openai", "https://api.openai.com/v1/", 4.0),
        (
            "openrouter",
            ROUTER_MODEL,
            "sk-openrouter",
            "https://openrouter.ai/api/v1/",
            4.0,
        ),
        ("ollama", "llama3.2", "ollama", "http://box:11434/v1/", 0.0),
    ],
)
def test_should_build_provider_with_its_own_key(
    provider: ProviderName,
    model: str,
    api_key: str,
    base_url: str,
    price_in: float,
) -> None:
    config = HAIKU_CONFIG.model_copy(
        update={
            "provider": provider,
            "model": model,
            "price": PRICE,
            "ollama_url": f"{OLLAMA_URL}/v1/",
        }
    )

    built = provider_from_config(config, SAVED_KEYS.get)

    assert (
        built.name,
        built.model,
        built._client.api_key,
        str(built._client.base_url),
        built._spec.input,
    ) == (provider, model, api_key, base_url, price_in)


def test_should_build_client_with_trimmed_saved_key() -> None:
    config = HAIKU_CONFIG.model_copy(
        update={"provider": "openrouter", "model": ROUTER_MODEL}
    )

    built = provider_from_config(config, {"openrouter_api_key": "sk-openrouter\n"}.get)

    assert built._client.api_key == "sk-openrouter"


def test_should_build_provider_for_earlier_model_choice() -> None:
    built = provider_for("openai", "gpt-5", HAIKU_CONFIG, SAVED_KEYS.get)

    assert (built.name, built.model) == ("openai", "gpt-5")


def test_should_raise_connection_error_for_invalid_ollama_url() -> None:
    config = HAIKU_CONFIG.model_copy(
        update={"provider": "ollama", "model": "llama3.2", "ollama_url": INVALID_URL}
    )

    with pytest.raises(ProviderError) as caught:
        provider_from_config(config, {}.get)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.connection,
        "ollama_url 'http://[::1' is not an http or https URL with a host",
    )
