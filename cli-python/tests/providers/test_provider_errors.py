from __future__ import annotations

from typing import TYPE_CHECKING

import httpx2
import pytest
from factories import (
    ANTHROPIC_TEST_KEY,
    OLLAMA_URL,
    OPENAI_TEST_KEY,
    PROMPT,
    ROUTER_MODEL,
    SHORT_RETRY_AFTER,
    Recorder,
    anthropic_client,
    anthropic_message,
    credential_headers,
    make_assessment,
    openai_client,
    openai_completion,
    refuse_connection,
    route_sdk_network,
    set_sdk_header_env,
    traceback_text,
    unreadable_replies,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.anthropic import AnthropicProvider
from jobranger.providers.base import (
    PROVIDER_LABELS,
    ProviderError,
    ProviderErrorKind,
)
from jobranger.providers.openai_compat import OpenAICompatProvider, base_url_for

if TYPE_CHECKING:
    from collections.abc import Callable

    from jobranger.providers.base import Provider, ProviderName

    type Handler = Callable[[httpx2.Request], httpx2.Response]

SONNET = "claude-sonnet-5-5"
GPT_MINI = "gpt-5-mini"
SDK_KINDS: tuple[ProviderName, ...] = ("anthropic", "openai")
MODEL_FOR: dict[ProviderName, str] = {
    "anthropic": SONNET,
    "openai": GPT_MINI,
    "openrouter": ROUTER_MODEL,
}
KEY_FOR: dict[ProviderName, str] = {
    "anthropic": ANTHROPIC_TEST_KEY,
    "openai": OPENAI_TEST_KEY,
    "openrouter": OPENAI_TEST_KEY,
}
SENT_KEY_HEADER: dict[ProviderName, dict[str, str]] = {
    "anthropic": {"x-api-key": ANTHROPIC_TEST_KEY},
    "openai": {"authorization": f"Bearer {OPENAI_TEST_KEY}"},
}
ERROR_BODY = {"error": {"message": "failed"}}
CALLS: dict[str, Callable[[Provider], object]] = {
    "classify": lambda provider: provider.classify(PROMPT, AdAssessment),
    "check key": lambda provider: provider.check_key(),
}


def _reply(kind: ProviderName) -> dict[str, object]:
    text = make_assessment().model_dump_json()
    if kind == "anthropic":
        return anthropic_message(text)
    return openai_completion("openai_completion.json", text)


def _wrong_usage_reply(kind: ProviderName) -> dict[str, object]:
    body = _reply(kind)
    body["usage"]["input_tokens" if kind == "anthropic" else "prompt_tokens"] = "many"
    return body


def _no_choices(request: httpx2.Request) -> httpx2.Response:
    return httpx2.Response(200, json={**_reply("openai"), "choices": []})


CLASSIFY_REPLIES = [
    pytest.param(kind, handler, id=f"{kind} {name}")
    for kind in SDK_KINDS
    for name, handler in unreadable_replies(_wrong_usage_reply(kind)).items()
] + [pytest.param("openai", _no_choices, id="openai no choices")]
KEY_CHECK_REPLIES = [
    pytest.param(kind, handler, id=f"{kind} {name}")
    for kind, wrong_types in (
        ("anthropic", {"id": 5, "type": "model"}),
        ("openai", {"id": 5, "object": "model"}),
        ("openrouter", {"data": "x"}),
    )
    for name, handler in unreadable_replies(wrong_types).items()
]


def _provider(kind: ProviderName, handler: Handler) -> Provider:
    if kind == "anthropic":
        return AnthropicProvider(
            api_key=ANTHROPIC_TEST_KEY,
            model=SONNET,
            spec=None,
            client=anthropic_client(handler),
        )
    base_url = base_url_for(kind, OLLAMA_URL)
    return OpenAICompatProvider(
        kind=kind,
        api_key=OPENAI_TEST_KEY,
        model=MODEL_FOR[kind],
        base_url=base_url,
        spec=None,
        client=openai_client(handler, base_url),
    )


def _live_provider(kind: ProviderName) -> Provider:
    if kind == "anthropic":
        return AnthropicProvider(api_key=ANTHROPIC_TEST_KEY, model=SONNET, spec=None)
    return OpenAICompatProvider(
        kind=kind,
        api_key=OPENAI_TEST_KEY,
        model=MODEL_FOR[kind],
        base_url=base_url_for(kind, OLLAMA_URL),
        spec=None,
    )


def _key_in_server_error(kind: ProviderName) -> Recorder:
    return Recorder(500, {"error": {"message": f"down for {KEY_FOR[kind]}"}})


def _unreadable(kind: ProviderName) -> tuple[ProviderErrorKind, str]:
    label = PROVIDER_LABELS[kind]
    return ProviderErrorKind.server, f"{label} sent a reply jobranger could not read"


@pytest.mark.parametrize("kind", SDK_KINDS)
def test_should_send_only_saved_key_when_sdk_header_env_is_set(
    kind: ProviderName, monkeypatch: pytest.MonkeyPatch
) -> None:
    recorder = Recorder(200, _reply(kind))
    route_sdk_network(monkeypatch, recorder)
    set_sdk_header_env(monkeypatch)
    provider = _live_provider(kind)

    provider.classify(PROMPT, AdAssessment)

    assert credential_headers(recorder.requests[-1]) == SENT_KEY_HEADER[kind]


@pytest.mark.parametrize("kind", SDK_KINDS)
def test_should_raise_server_error_without_key_when_key_check_fails(
    kind: ProviderName,
) -> None:
    provider = _provider(kind, _key_in_server_error(kind))

    with pytest.raises(ProviderError) as raised:
        provider.check_key()

    assert (raised.value.kind, raised.value.message) == (
        ProviderErrorKind.server,
        "HTTP 500",
    )


@pytest.mark.parametrize("call", CALLS.values(), ids=CALLS.keys())
@pytest.mark.parametrize("kind", SDK_KINDS)
def test_should_leave_sdk_error_out_of_traceback(
    kind: ProviderName, call: Callable[[Provider], object]
) -> None:
    provider = _provider(kind, _key_in_server_error(kind))

    with pytest.raises(ProviderError) as raised:
        call(provider)

    assert KEY_FOR[kind] not in traceback_text(raised.value)


@pytest.mark.parametrize("kind", SDK_KINDS)
def test_should_raise_connection_error_when_api_is_unreachable(
    kind: ProviderName,
) -> None:
    provider = _provider(kind, refuse_connection)

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert (raised.value.kind, raised.value.message) == (
        ProviderErrorKind.connection,
        "Connection error.",
    )


@pytest.mark.parametrize(("kind", "handler"), CLASSIFY_REPLIES)
def test_should_raise_server_error_when_reply_is_unreadable(
    kind: ProviderName, handler: Handler
) -> None:
    provider = _provider(kind, handler)

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert (raised.value.kind, raised.value.message) == _unreadable(kind)


@pytest.mark.parametrize(("kind", "handler"), KEY_CHECK_REPLIES)
def test_should_raise_server_error_when_key_check_reply_is_unreadable(
    kind: ProviderName, handler: Handler
) -> None:
    provider = _provider(kind, handler)

    with pytest.raises(ProviderError) as raised:
        provider.check_key()

    assert (raised.value.kind, raised.value.message) == _unreadable(kind)


@pytest.mark.parametrize("kind", SDK_KINDS)
def test_should_retry_failed_call_four_times(
    kind: ProviderName, monkeypatch: pytest.MonkeyPatch
) -> None:
    recorder = Recorder(500, ERROR_BODY, **SHORT_RETRY_AFTER)
    route_sdk_network(monkeypatch, recorder)
    provider = _live_provider(kind)

    with pytest.raises(ProviderError):
        provider.check_key()

    assert len(recorder.requests) == 5
