from __future__ import annotations

from typing import TYPE_CHECKING, Literal

import httpx
import httpx2
import pytest
from factories import (
    ANTHROPIC_TEST_KEY,
    CREDENTIAL_OLLAMA_URL,
    INVALID_URL,
    MASKED_KEY_MESSAGE,
    OLLAMA_PASSWORD,
    OLLAMA_URL,
    OPENAI_TEST_KEY,
    ROUTER_MODEL,
    Recorder,
    anthropic_client,
    openai_client,
    redirect_sdk_env,
    traceback_text,
    unreadable_replies,
)

from jobranger.providers import models as model_listing
from jobranger.providers.base import (
    REQUEST_TIMEOUT_SECONDS,
    SDK_MAX_RETRIES,
    ProviderError,
    ProviderErrorKind,
)
from jobranger.providers.models import ModelInfo, list_models, sdk_client

if TYPE_CHECKING:
    from collections.abc import Callable

    import anthropic
    import openai

    from jobranger.providers.base import ProviderName

ANTHROPIC_MODELS = {
    "data": [
        {
            "type": "model",
            "id": "claude-future-9",
            "display_name": "Claude Future 9",
            "created_at": "2026-09-01T00:00:00Z",
            "lifecycle": "active",
        },
        {
            "type": "model",
            "id": "claude-sonnet-5-5",
            "display_name": "Claude Sonnet 5.5",
            "created_at": "2026-06-01T00:00:00Z",
            "lifecycle": "active",
        },
        {
            "type": "model",
            "id": "claude-haiku-5-5",
            "display_name": "Claude Haiku 5.5",
            "created_at": "2026-06-01T00:00:00Z",
            "lifecycle": "active",
        },
    ],
    "has_more": False,
    "first_id": "claude-future-9",
    "last_id": "claude-haiku-5-5",
}
OPENAI_MODEL_IDS = (
    "gpt-4.1",
    "gpt-4o-audio-preview",
    "gpt-realtime",
    "gpt-4o-transcribe",
    "gpt-4o-mini-tts",
    "gpt-image-1",
    "gpt-4o-search-preview",
    "dall-e-3",
    "text-embedding-3-small",
    "gpt-5-mini",
)
OPENAI_MODELS = {
    "object": "list",
    "data": [
        {"id": model_id, "object": "model", "created": 1, "owned_by": "openai"}
        for model_id in OPENAI_MODEL_IDS
    ],
}
OPENROUTER_MODELS = {
    "data": [
        {
            "id": ROUTER_MODEL,
            "name": "Vendor: Model One",
            "pricing": {"prompt": "0.0000005", "completion": "0.0000015"},
        }
    ]
}
OLLAMA_TAGS = {"models": [{"name": "llama3.2:latest", "size": 1}]}
AUTH_ERROR_BODY = {
    "error": {"type": "authentication_error", "message": MASKED_KEY_MESSAGE}
}
KEY_FOR = {"anthropic": ANTHROPIC_TEST_KEY, "openai": OPENAI_TEST_KEY}
SDK_LISTING_REPLIES = [
    pytest.param(provider, client_for, label, handler, id=f"{provider} {name}")
    for provider, client_for, label, wrong_types in (
        (
            "anthropic",
            anthropic_client,
            "Anthropic",
            {"data": [{"id": 5, "display_name": "Five"}], "has_more": False},
        ),
        ("openai", openai_client, "OpenAI", {"object": "list", "data": [{"id": 5}]}),
    )
    for name, handler in unreadable_replies(wrong_types).items()
]
UNREADABLE_REPLY = "<html>busy</html>"
RATE_LIMIT_BODY = {"error": {"type": "rate_limit_error", "message": "slow down"}}


@pytest.fixture(autouse=True)
def offline_sdk(monkeypatch: pytest.MonkeyPatch) -> None:
    def fail(provider: str, key: str) -> None:
        raise AssertionError(provider)

    monkeypatch.setattr(model_listing, "sdk_client", fail)


def _http(handler: Callable[[httpx.Request], httpx.Response]) -> httpx.Client:
    return httpx.Client(transport=httpx.MockTransport(handler))


def _unused_http() -> httpx.Client:
    def fail(request: httpx.Request) -> httpx.Response:
        raise AssertionError(request.url)

    return _http(fail)


def _json_reply(body: object) -> Callable[[httpx.Request], httpx.Response]:
    return lambda request: httpx.Response(200, json=body)


def test_should_convert_openrouter_per_token_prices_to_per_million() -> None:
    http = _http(_json_reply(OPENROUTER_MODELS))

    models = list_models("openrouter", None, OLLAMA_URL, http)

    assert models == [
        ModelInfo(ROUTER_MODEL, "Vendor: Model One", 0.5, 1.5, batch=False)
    ]


def test_should_treat_negative_openrouter_price_as_unknown() -> None:
    router = {
        "id": "openrouter/auto",
        "name": "Auto Router",
        "pricing": {"prompt": "-1", "completion": "-1"},
    }
    body = {"data": [router, *OPENROUTER_MODELS["data"]]}

    models = list_models("openrouter", None, OLLAMA_URL, _http(_json_reply(body)))

    assert models == [
        ModelInfo(ROUTER_MODEL, "Vendor: Model One", 0.5, 1.5, batch=False),
        ModelInfo("openrouter/auto", "Auto Router", None, None, batch=False),
    ]


@pytest.mark.parametrize(
    ("provider", "ollama_url", "body", "url"),
    [
        ("ollama", "http://box:11434/v1/", OLLAMA_TAGS, "http://box:11434/api/tags"),
        (
            "openrouter",
            OLLAMA_URL,
            OPENROUTER_MODELS,
            "https://openrouter.ai/api/v1/models?supported_parameters=structured_outputs",
        ),
    ],
)
def test_should_request_model_list_from_provider_url(
    provider: ProviderName, ollama_url: str, body: object, url: str
) -> None:
    requests: list[httpx.Request] = []

    def reply(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        return httpx.Response(200, json=body)

    list_models(provider, None, ollama_url, _http(reply))

    assert [str(request.url) for request in requests] == [url]


def test_should_list_ollama_models_from_tags_at_zero_price() -> None:
    http = _http(_json_reply(OLLAMA_TAGS))

    models = list_models("ollama", None, OLLAMA_URL, http)

    assert models == [
        ModelInfo("llama3.2:latest", "llama3.2:latest", 0.0, 0.0, batch=False)
    ]


def _refuse(request: httpx.Request) -> httpx.Response:
    raise httpx.ConnectError("refused", request=request)


@pytest.mark.parametrize(
    ("provider", "handler", "kind", "message"),
    [
        (
            "ollama",
            _refuse,
            ProviderErrorKind.connection,
            "No Ollama answered at http://box:11434. Start it with: ollama serve",
        ),
        (
            "ollama",
            lambda request: httpx.Response(503),
            ProviderErrorKind.server,
            "Ollama at http://box:11434 answered HTTP 503",
        ),
        (
            "openrouter",
            _refuse,
            ProviderErrorKind.connection,
            "Could not reach OpenRouter at https://openrouter.ai/api/v1",
        ),
    ],
)
def test_should_name_provider_and_target_when_listing_fails(
    provider: ProviderName,
    handler: Callable[[httpx.Request], httpx.Response],
    kind: ProviderErrorKind,
    message: str,
) -> None:
    with pytest.raises(ProviderError) as caught:
        list_models(provider, None, OLLAMA_URL, _http(handler))

    assert (caught.value.kind, caught.value.message) == (kind, message)


@pytest.mark.parametrize(
    ("ollama_url", "handler"),
    [
        (CREDENTIAL_OLLAMA_URL, _refuse),
        (CREDENTIAL_OLLAMA_URL, lambda request: httpx.Response(503)),
        (CREDENTIAL_OLLAMA_URL, lambda request: httpx.Response(401)),
        (CREDENTIAL_OLLAMA_URL, lambda request: httpx.Response(200, text="{}")),
        (f"http://user:{OLLAMA_PASSWORD}@[::1", _refuse),
    ],
    ids=["unreachable", "server error", "refused", "not tags", "invalid url"],
)
def test_should_hide_ollama_password_when_listing_fails(
    ollama_url: str, handler: Callable[[httpx.Request], httpx.Response]
) -> None:
    with pytest.raises(ProviderError) as caught:
        list_models("ollama", None, ollama_url, _http(handler))

    assert OLLAMA_PASSWORD not in traceback_text(caught.value)


@pytest.mark.parametrize(
    ("provider", "client_for", "label", "handler"), SDK_LISTING_REPLIES
)
def test_should_raise_server_error_when_listing_reply_is_unreadable(
    provider: ProviderName,
    client_for: Callable[..., anthropic.Anthropic | openai.OpenAI],
    label: str,
    handler: Callable[[httpx2.Request], httpx2.Response],
) -> None:
    client = client_for(handler)

    with pytest.raises(ProviderError) as caught:
        list_models(provider, KEY_FOR[provider], OLLAMA_URL, _unused_http(), client)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.server,
        f"{label} sent a reply jobranger could not read",
    )


def test_should_filter_openai_models_to_chat_models() -> None:
    client = openai_client(Recorder(200, OPENAI_MODELS))

    models = list_models("openai", OPENAI_TEST_KEY, OLLAMA_URL, _unused_http(), client)

    assert [(model.id, model.name) for model in models] == [
        ("gpt-5-mini", "gpt-5-mini"),
        ("gpt-4.1", "gpt-4.1"),
    ]


def test_should_list_unknown_anthropic_model_with_no_price_last() -> None:
    client = anthropic_client(Recorder(200, ANTHROPIC_MODELS))

    models = list_models(
        "anthropic", ANTHROPIC_TEST_KEY, OLLAMA_URL, _unused_http(), client
    )

    assert models == [
        ModelInfo("claude-haiku-5-5", "Claude Haiku 5.5", 0.1, 0.5, batch=True),
        ModelInfo("claude-sonnet-5-5", "Claude Sonnet 5.5", 2.0, 10.0, batch=True),
        ModelInfo("claude-future-9", "Claude Future 9", None, None, batch=True),
    ]


@pytest.mark.parametrize(
    ("provider", "client_for"),
    [("anthropic", anthropic_client), ("openai", openai_client)],
)
def test_should_raise_auth_error_when_key_is_rejected_while_listing(
    provider: ProviderName,
    client_for: Callable[[Recorder], anthropic.Anthropic | openai.OpenAI],
) -> None:
    client = client_for(Recorder(401, AUTH_ERROR_BODY))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, KEY_FOR[provider], OLLAMA_URL, _unused_http(), client)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.auth,
        "HTTP 401",
    )


@pytest.mark.parametrize(
    ("provider", "client_for"),
    [("anthropic", anthropic_client), ("openai", openai_client)],
)
def test_should_carry_retry_after_when_listing_is_rate_limited(
    provider: ProviderName,
    client_for: Callable[[Recorder], anthropic.Anthropic | openai.OpenAI],
) -> None:
    client = client_for(Recorder(429, RATE_LIMIT_BODY, **{"retry-after": "7"}))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, KEY_FOR[provider], OLLAMA_URL, _unused_http(), client)

    assert (caught.value.kind, caught.value.retry_after) == (
        ProviderErrorKind.rate_limit,
        7.0,
    )


@pytest.mark.parametrize(
    ("provider", "client_for", "body", "label"),
    [
        ("anthropic", anthropic_client, ANTHROPIC_MODELS, "Anthropic"),
        ("openai", openai_client, OPENAI_MODELS, "OpenAI"),
    ],
)
def test_should_refuse_non_ascii_key_without_listing(
    provider: ProviderName,
    client_for: Callable[[Recorder], anthropic.Anthropic | openai.OpenAI],
    body: object,
    label: str,
) -> None:
    recorder = Recorder(200, body)

    with pytest.raises(ProviderError) as caught:
        list_models(
            provider, "fake\u200bkey", OLLAMA_URL, _unused_http(), client_for(recorder)
        )

    assert (caught.value.kind, caught.value.message, recorder.requests) == (
        ProviderErrorKind.auth,
        f"{label} API key must be printable ASCII",
        [],
    )


@pytest.mark.parametrize(
    ("provider", "client_for", "key"),
    [
        ("anthropic", anthropic_client, ANTHROPIC_TEST_KEY),
        ("openai", openai_client, OPENAI_TEST_KEY),
    ],
)
def test_should_leave_provider_detail_out_of_listing_error_with_the_key(
    provider: ProviderName,
    client_for: Callable[[Recorder], anthropic.Anthropic | openai.OpenAI],
    key: str,
) -> None:
    body = {"error": {"type": "invalid_request_error", "message": f"bad {key}"}}
    client = client_for(Recorder(400, body))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, key, OLLAMA_URL, _unused_http(), client)

    assert key not in traceback_text(caught.value)


def test_should_build_listing_client_from_trimmed_key(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    built: list[tuple[str, str]] = []

    def build(provider: str, key: str) -> anthropic.Anthropic:
        built.append((provider, key))
        return anthropic_client(Recorder(200, ANTHROPIC_MODELS))

    monkeypatch.setattr(model_listing, "sdk_client", build)

    list_models("anthropic", f" {ANTHROPIC_TEST_KEY}\n", OLLAMA_URL, _unused_http())

    assert built == [("anthropic", ANTHROPIC_TEST_KEY)]


def _openrouter_entry(**overrides: object) -> dict[str, object]:
    return {**OPENROUTER_MODELS["data"][0], **overrides}


def _list_openrouter(*entries: dict[str, object]) -> list[ModelInfo]:
    http = _http(_json_reply({"data": list(entries)}))
    return list_models("openrouter", None, OLLAMA_URL, http)


@pytest.mark.parametrize(
    ("provider", "key", "client_for", "body", "label"),
    [
        ("anthropic", "   ", anthropic_client, ANTHROPIC_MODELS, "Anthropic"),
        ("openai", None, openai_client, OPENAI_MODELS, "OpenAI"),
    ],
)
def test_should_raise_auth_error_when_sdk_provider_has_no_key(
    provider: ProviderName,
    key: str | None,
    client_for: Callable[[Recorder], anthropic.Anthropic | openai.OpenAI],
    body: object,
    label: str,
) -> None:
    sdk = client_for(Recorder(200, body))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, key, OLLAMA_URL, _unused_http(), sdk)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.auth,
        f"no API key saved for {label}",
    )


def test_should_raise_server_error_when_openrouter_listing_is_malformed() -> None:
    http = _http(lambda request: httpx.Response(200, text=UNREADABLE_REPLY))

    with pytest.raises(ProviderError) as caught:
        list_models("openrouter", None, OLLAMA_URL, http)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.server,
        "OpenRouter sent a model list jobranger could not read",
    )


def test_should_raise_server_error_naming_url_when_ollama_reply_is_not_tags() -> None:
    http = _http(lambda request: httpx.Response(200, text="<html>other service</html>"))

    with pytest.raises(ProviderError) as caught:
        list_models("ollama", None, OLLAMA_URL, http)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.server,
        "http://box:11434 answered, but not with an Ollama model list. "
        "Check ollama_url",
    )


@pytest.mark.parametrize("provider", ["openrouter", "ollama"])
def test_should_leave_unreadable_reply_out_of_listing_traceback(
    provider: ProviderName,
) -> None:
    http = _http(lambda request: httpx.Response(200, text=UNREADABLE_REPLY))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, None, OLLAMA_URL, http)

    assert UNREADABLE_REPLY not in traceback_text(caught.value)


def test_should_list_openrouter_model_with_bad_pricing_without_price() -> None:
    pricing = {"prompt": "n/a", "completion": "0.0000015"}

    models = _list_openrouter(_openrouter_entry(id="vendor/bad", pricing=pricing))

    assert models == [ModelInfo("vendor/bad", "Vendor: Model One", None, None, False)]


@pytest.mark.parametrize(
    "without_id",
    [
        {"name": "No Id", "pricing": {"prompt": "0", "completion": "0"}},
        {"id": "", "name": "Empty Id", "pricing": {"prompt": "0", "completion": "0"}},
    ],
)
def test_should_skip_openrouter_entry_without_id(without_id: dict[str, object]) -> None:
    models = _list_openrouter(without_id, _openrouter_entry())

    assert [model.id for model in models] == [ROUTER_MODEL]


def test_should_list_free_openrouter_model_at_zero_price() -> None:
    free = _openrouter_entry(
        id="vendor/free",
        name="Vendor: Free",
        pricing={"prompt": "0", "completion": "0"},
    )

    models = _list_openrouter(_openrouter_entry(), free)

    assert models == [
        ModelInfo("vendor/free", "Vendor: Free", 0.0, 0.0, batch=False),
        ModelInfo(ROUTER_MODEL, "Vendor: Model One", 0.5, 1.5, batch=False),
    ]


def test_should_name_openrouter_model_by_id_when_name_is_missing() -> None:
    entry = {"id": "vendor/unnamed", "pricing": {"prompt": "0", "completion": "0"}}

    models = _list_openrouter(entry)

    assert models[0].name == "vendor/unnamed"


@pytest.mark.parametrize(
    "pricing",
    [
        {"prompt": "NaN", "completion": "0.000001"},
        {"prompt": "0.000001", "completion": "inf"},
    ],
)
def test_should_treat_non_finite_openrouter_price_as_unknown(
    pricing: dict[str, str],
) -> None:
    models = _list_openrouter(_openrouter_entry(pricing=pricing))

    assert (models[0].price_in, models[0].price_out) == (None, None)


def test_should_round_openrouter_prices_to_six_decimals() -> None:
    price = "0.0000000123456789"
    entry = _openrouter_entry(pricing={"prompt": price, "completion": price})

    models = _list_openrouter(entry)

    assert (models[0].price_in, models[0].price_out) == (0.012346, 0.012346)


def test_should_raise_connection_error_naming_invalid_ollama_url() -> None:
    with pytest.raises(ProviderError) as caught:
        list_models("ollama", None, INVALID_URL, _unused_http())

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.connection,
        "ollama_url 'http://[::1' is not an http or https URL with a host",
    )


@pytest.mark.parametrize(
    ("status", "kind"),
    [
        (400, ProviderErrorKind.bad_request),
        (404, ProviderErrorKind.not_found),
        (413, ProviderErrorKind.bad_request),
        (422, ProviderErrorKind.bad_request),
        (500, ProviderErrorKind.server),
    ],
)
def test_should_map_openrouter_listing_status_like_calls(
    status: int, kind: ProviderErrorKind
) -> None:
    http = _http(lambda request: httpx.Response(status))

    with pytest.raises(ProviderError) as caught:
        list_models("openrouter", None, OLLAMA_URL, http)

    assert caught.value.kind == kind


@pytest.mark.parametrize(
    ("provider", "status"),
    [("openrouter", 401), ("openrouter", 403), ("ollama", 401), ("ollama", 403)],
)
def test_should_name_only_status_when_listing_auth_fails(
    provider: ProviderName, status: int
) -> None:
    http = _http(lambda request: httpx.Response(status))

    with pytest.raises(ProviderError) as caught:
        list_models(provider, None, OLLAMA_URL, http)

    assert (caught.value.kind, caught.value.message) == (
        ProviderErrorKind.auth,
        f"HTTP {status}",
    )


def test_should_raise_rate_limit_when_openrouter_answers_429() -> None:
    http = _http(lambda request: httpx.Response(429, headers={"retry-after": "7"}))

    with pytest.raises(ProviderError) as caught:
        list_models("openrouter", None, OLLAMA_URL, http)

    assert (caught.value.kind, caught.value.retry_after) == (
        ProviderErrorKind.rate_limit,
        7.0,
    )


@pytest.mark.parametrize(
    ("provider", "base_url"),
    [
        ("anthropic", "https://api.anthropic.com"),
        ("openai", "https://api.openai.com/v1/"),
    ],
)
def test_should_build_listing_client_for_official_url_when_base_url_env_is_set(
    provider: Literal["anthropic", "openai"],
    base_url: str,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    redirect_sdk_env(monkeypatch)

    client = sdk_client(provider, OPENAI_TEST_KEY)

    assert (
        client.api_key,
        client.max_retries,
        client.timeout,
        str(client.base_url),
    ) == (OPENAI_TEST_KEY, SDK_MAX_RETRIES, REQUEST_TIMEOUT_SECONDS, base_url)
