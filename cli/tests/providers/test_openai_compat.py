from __future__ import annotations

from typing import TYPE_CHECKING

import httpx2
import pytest
from factories import (
    BLANK_KEY_IDS,
    BLANK_KEYS,
    CREDENTIAL_OLLAMA_URL,
    ELSEWHERE_URL,
    MASKED_KEY_MESSAGE,
    NON_ASCII_KEY_IDS,
    NON_ASCII_KEYS,
    OLLAMA_URL,
    OPENAI_TEST_KEY,
    PROMPT,
    ROUTER_MODEL,
    Recorder,
    make_assessment,
    openai_client,
    openai_completion,
    refuse_connection,
    route_sdk_network,
    unreadable_replies,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.base import (
    KEY_CHECK_MESSAGES,
    KeyCheck,
    ProviderError,
    ProviderErrorKind,
    StopKind,
)
from jobranger.providers.openai_compat import OpenAICompatProvider, base_url_for
from jobranger.providers.prices import spec_for

if TYPE_CHECKING:
    from collections.abc import Callable

    from jobranger.providers.base import ProviderName

GPT_MINI = "gpt-5-mini"
UNLISTED_MODEL = "gpt-unlisted"
OLLAMA_MODEL = "llama3.2"
OPENAI_ONLY_FIELDS = ("prompt_cache_key", "reasoning_effort", "temperature")
FIXTURE_FOR: dict[ProviderName, str] = {
    "openai": "openai_completion.json",
    "openrouter": "openrouter_completion.json",
    "ollama": "ollama_completion.json",
}
MODEL_FOR: dict[ProviderName, str] = {
    "openai": GPT_MINI,
    "openrouter": ROUTER_MODEL,
    "ollama": OLLAMA_MODEL,
}
ERROR_BODY = {"error": {"message": "failed"}}
KEY_REPLIES: dict[ProviderName, object] = {
    "openai": {"id": GPT_MINI, "object": "model", "created": 0, "owned_by": "openai"},
    "openrouter": {"data": {"label": "fake-key"}},
}
PROMPT_CACHE_KEY = "jobranger-5c988adf05509a74"
BAD_REQUEST_CHECK = KEY_CHECK_MESSAGES[ProviderErrorKind.bad_request].format(
    provider="OpenAI", model=GPT_MINI
)
OLLAMA_HAS_MODEL = KeyCheck(True, f"Ollama has {OLLAMA_MODEL}")
OLLAMA_REFUSED = (
    f"Ollama at {OLLAMA_URL} refused the request (HTTP {{status}}). "
    "Check its access settings"
)
OLLAMA_NOT_RUNNING = f"No Ollama answered at {OLLAMA_URL}. Start it with: ollama serve"
OLLAMA_MISSING_MODEL = KeyCheck(
    False,
    f"Ollama has no model named {OLLAMA_MODEL}. "
    f"Pull it with: ollama pull {OLLAMA_MODEL}",
)


def _reply(kind: ProviderName = "openai", **choice: object) -> dict[str, object]:
    body = make_assessment().model_dump_json()
    return openai_completion(FIXTURE_FOR[kind], body, **choice)


OLLAMA_TAGS_REPLIES = unreadable_replies({"models": "x"})


def _provider(
    handler: Callable[[httpx2.Request], httpx2.Response],
    kind: ProviderName = "openai",
    model: str | None = None,
    ollama_url: str = OLLAMA_URL,
) -> OpenAICompatProvider:
    base_url = base_url_for(kind, ollama_url)
    model = model or MODEL_FOR[kind]
    return OpenAICompatProvider(
        kind=kind,
        api_key=OPENAI_TEST_KEY,
        model=model,
        base_url=base_url,
        spec=spec_for(kind, model, None),
        client=openai_client(handler, base_url),
    )


def _live_provider(kind: ProviderName = "openai") -> OpenAICompatProvider:
    return OpenAICompatProvider(
        kind=kind,
        api_key=OPENAI_TEST_KEY,
        model=MODEL_FOR[kind],
        base_url=base_url_for(kind, OLLAMA_URL),
        spec=None,
    )


def _sent(kind: ProviderName = "openai") -> dict[str, object]:
    recorder = Recorder(200, _reply(kind))
    provider = _provider(recorder, kind)

    provider.classify(PROMPT, AdAssessment)

    return recorder.sent()


def _classify(kind: ProviderName, body: dict[str, object]) -> object:
    provider = _provider(Recorder(200, body), kind)
    return provider.classify(PROMPT, AdAssessment)


def test_should_send_strict_json_schema_response_format() -> None:
    response_format = _sent()["response_format"]

    assert (response_format["type"], response_format["json_schema"]["strict"]) == (
        "json_schema",
        True,
    )


def test_should_send_prompt_as_system_and_user_messages() -> None:
    sent = _sent()

    assert sent["messages"] == [
        {"role": "system", "content": PROMPT.system},
        {"role": "user", "content": PROMPT.user},
    ]


def test_should_send_chosen_model_with_output_limit() -> None:
    sent = _sent()

    assert (sent["model"], sent["max_completion_tokens"]) == (GPT_MINI, 4096)


def test_should_send_prompt_cache_key_from_system_prompt_hash() -> None:
    sent = _sent()

    assert sent["prompt_cache_key"] == PROMPT_CACHE_KEY


def test_should_send_low_reasoning_effort_to_openai() -> None:
    sent = _sent()

    assert sent["reasoning_effort"] == "low"


def test_should_omit_reasoning_effort_for_unlisted_openai_model() -> None:
    recorder = Recorder(200, _reply())
    provider = _provider(recorder, model=UNLISTED_MODEL)

    provider.classify(PROMPT, AdAssessment)

    assert "reasoning_effort" not in recorder.sent()


def test_should_send_zero_temperature_to_ollama() -> None:
    sent = _sent("ollama")

    assert sent["temperature"] == 0


def test_should_send_openrouter_provider_preferences() -> None:
    sent = _sent("openrouter")

    assert sent["provider"] == {
        "require_parameters": True,
        "data_collection": "deny",
    }


def test_should_omit_openai_only_parameters_for_openrouter() -> None:
    sent = _sent("openrouter")

    assert [field for field in OPENAI_ONLY_FIELDS if field in sent] == []


def test_should_drop_trailing_slash_from_ollama_url() -> None:
    assert base_url_for("ollama", f"{OLLAMA_URL}/") == f"{OLLAMA_URL}/v1"


def test_should_send_ollama_requests_to_v1_under_configured_url() -> None:
    recorder = Recorder(200, _reply("ollama"))
    provider = _provider(recorder, "ollama")

    provider.classify(PROMPT, AdAssessment)

    assert str(recorder.requests[-1].url) == f"{OLLAMA_URL}/v1/chat/completions"


def test_should_return_parsed_assessment() -> None:
    result = _classify("openai", _reply())

    assert (result.stop, result.parsed) == (StopKind.done, make_assessment())


def test_should_use_openrouter_reported_cost() -> None:
    result = _classify("openrouter", _reply("openrouter"))

    assert result.usage.cost_usd == pytest.approx(0.0042)


def test_should_subtract_cached_tokens_from_openai_prompt_tokens() -> None:
    result = _classify("openai", _reply())

    assert result.usage.cost_usd == pytest.approx(0.0009875)


def test_should_ignore_reported_cost_from_openai() -> None:
    body = _reply()
    body["usage"]["cost"] = 1.0

    result = _classify("openai", body)

    assert result.usage.cost_usd == pytest.approx(0.0009875)


def test_should_record_ollama_tokens_at_zero_cost() -> None:
    result = _classify("ollama", _reply("ollama"))

    usage = result.usage
    assert (usage.input_tokens, usage.output_tokens, usage.cost_usd, usage.priced) == (
        3000,
        400,
        0.0,
        True,
    )


@pytest.mark.parametrize(
    ("cached", "expected"),
    [(2500, (1, 500, 2500, 400)), (None, (1, 3000, 0, 400))],
    ids=["cached tokens", "no cached count"],
)
def test_should_count_tokens_from_reply(
    cached: int | None, expected: tuple[int, ...]
) -> None:
    body = _reply()
    body["usage"]["prompt_tokens_details"]["cached_tokens"] = cached

    result = _classify("openai", body)

    usage = result.usage
    assert (
        usage.calls,
        usage.input_tokens,
        usage.cache_read_tokens,
        usage.output_tokens,
    ) == expected


def test_should_mark_usage_unpriced_for_unlisted_model() -> None:
    provider = _provider(Recorder(200, _reply()), model=UNLISTED_MODEL)

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.usage.priced, result.usage.cost_usd) == (False, 0.0)


def test_should_mark_usage_unpriced_when_reply_has_no_usage() -> None:
    body = _reply()
    del body["usage"]

    result = _classify("openai", body)

    assert (result.usage.calls, result.usage.priced) == (1, False)


def test_should_return_refusal_stop_when_model_refuses() -> None:
    body = openai_completion(FIXTURE_FOR["openai"], "")
    body["choices"][0]["message"].update(content=None, refusal="I can't help.")

    result = _classify("openai", body)

    assert (result.stop, result.parsed) == (StopKind.refusal, None)


def test_should_return_truncated_stop_when_finish_reason_is_length() -> None:
    body = openai_completion(FIXTURE_FOR["openai"], '{"is_programming_job": tr')
    body["choices"][0]["finish_reason"] = "length"

    result = _classify("openai", body)

    assert result.stop == StopKind.truncated


def test_should_return_empty_text_when_reply_has_no_content() -> None:
    body = openai_completion(FIXTURE_FOR["openai"], "")
    body["choices"][0]["message"].update(content=None, refusal=None)

    result = _classify("openai", body)

    assert (result.text, result.stop) == ("", StopKind.invalid)


def test_should_keep_usage_when_reply_is_not_valid_json() -> None:
    body = openai_completion(FIXTURE_FOR["openai"], "not json")

    result = _classify("openai", body)

    assert (result.stop, result.usage.output_tokens) == (StopKind.invalid, 400)


def test_should_carry_retry_after_on_rate_limit() -> None:
    body = {"error": {"message": "slow down", "type": "rate_limit_exceeded"}}
    provider = _provider(Recorder(429, body, **{"retry-after": "30"}))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert (raised.value.kind, raised.value.retry_after) == (
        ProviderErrorKind.rate_limit,
        30.0,
    )


@pytest.mark.parametrize(
    ("kind", "status", "path", "expected"),
    [
        ("openai", 200, f"/v1/models/{GPT_MINI}", KeyCheck(True, "Key works")),
        (
            "openai",
            401,
            f"/v1/models/{GPT_MINI}",
            KeyCheck(False, "OpenAI rejected the key"),
        ),
        (
            "openai",
            404,
            f"/v1/models/{GPT_MINI}",
            KeyCheck(False, f"OpenAI has no model named {GPT_MINI}"),
        ),
        ("openrouter", 200, "/api/v1/key", KeyCheck(True, "Key works")),
        (
            "openrouter",
            401,
            "/api/v1/key",
            KeyCheck(False, "OpenRouter rejected the key"),
        ),
    ],
    ids=[
        "openai key works",
        "openai rejected",
        "openai unknown model",
        "router key works",
        "router rejected",
    ],
)
def test_should_check_key_with_provider_endpoint(
    kind: ProviderName, status: int, path: str, expected: KeyCheck
) -> None:
    recorder = Recorder(status, KEY_REPLIES[kind] if expected.ok else ERROR_BODY)
    provider = _provider(recorder, kind)

    check = provider.check_key()

    assert (check, recorder.requests[-1].url.path) == (expected, path)


def test_should_send_saved_key_to_openrouter_with_request_timeout(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    recorder = Recorder(200, {"data": {}})
    route_sdk_network(monkeypatch, recorder)
    provider = _live_provider("openrouter")

    provider.check_key()

    request = recorder.requests[-1]
    assert (
        str(request.url),
        request.headers.get("authorization"),
        request.extensions["timeout"],
    ) == (
        "https://openrouter.ai/api/v1/key",
        f"Bearer {OPENAI_TEST_KEY}",
        {"connect": 120, "read": 120, "write": 120, "pool": 120},
    )


def test_should_leave_provider_detail_out_of_error_when_it_contains_the_key() -> None:
    body = {"error": {"message": f"invalid key {OPENAI_TEST_KEY}"}}
    provider = _provider(Recorder(400, body))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400"


@pytest.mark.parametrize("kind", ["openai", "openrouter"])
def test_should_keep_clean_provider_detail_in_error(kind: ProviderName) -> None:
    body = {"error": {"message": "invalid\nmodel id"}}
    provider = _provider(Recorder(400, body), kind)

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400: invalid model id"


def test_should_leave_provider_detail_out_of_auth_error() -> None:
    body = {"error": {"message": MASKED_KEY_MESSAGE, "code": "invalid_api_key"}}
    provider = _provider(Recorder(401, body))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 401"


def test_should_report_bad_request_key_check_as_rejected() -> None:
    body = {"error": {"message": "invalid\nmodel id", "type": "invalid_request_error"}}
    provider = _provider(Recorder(400, body))

    check = provider.check_key()

    assert check == KeyCheck(
        ok=False,
        message=f"{BAD_REQUEST_CHECK}. OpenAI said: invalid model id",
    )


def test_should_leave_provider_detail_out_when_it_contains_the_key() -> None:
    body = {"error": {"message": f"bad key {OPENAI_TEST_KEY}"}}
    provider = _provider(Recorder(400, body))

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


def test_should_report_too_large_key_check_as_rejected() -> None:
    provider = _provider(Recorder(413, ERROR_BODY))

    check = provider.check_key()

    assert check == KeyCheck(
        ok=False, message=f"{BAD_REQUEST_CHECK}. OpenAI said: failed"
    )


@pytest.mark.parametrize(
    ("installed", "expected"),
    [
        ("llama3.2", OLLAMA_HAS_MODEL),
        ("llama3.2:latest", OLLAMA_HAS_MODEL),
        ("qwen3:8b", OLLAMA_MISSING_MODEL),
    ],
    ids=["exact name", "latest tag", "other model"],
)
def test_should_check_ollama_tags_for_configured_model(
    installed: str, expected: KeyCheck
) -> None:
    recorder = Recorder(200, {"models": [{"name": installed, "model": installed}]})
    provider = _provider(recorder, "ollama")

    check = provider.check_key()

    assert (check, str(recorder.requests[-1].url)) == (
        expected,
        f"{OLLAMA_URL}/api/tags",
    )


def test_should_name_ollama_serve_when_ollama_is_unreachable() -> None:
    provider = _provider(refuse_connection, "ollama")

    check = provider.check_key()

    assert "ollama serve" in check.message


@pytest.mark.parametrize(
    ("status", "message"),
    [
        (401, OLLAMA_REFUSED.format(status=401)),
        (403, OLLAMA_REFUSED.format(status=403)),
        (
            429,
            f"Ollama at {OLLAMA_URL} is limiting requests (HTTP 429). Try again later",
        ),
        (500, f"Ollama at {OLLAMA_URL} failed (HTTP 500). Check the Ollama logs"),
        (404, f"Ollama at {OLLAMA_URL} answered HTTP 404. Check ollama_url"),
    ],
)
def test_should_name_ollama_status_when_tags_request_fails(
    status: int, message: str
) -> None:
    provider = _provider(Recorder(status, ERROR_BODY), "ollama")

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=message)


@pytest.mark.parametrize(
    ("handler", "message"),
    [
        (lambda request: httpx2.Response(401, json=ERROR_BODY), OLLAMA_REFUSED),
        (refuse_connection, OLLAMA_NOT_RUNNING),
    ],
    ids=["refused", "unreachable"],
)
def test_should_hide_ollama_password_in_key_check(
    handler: Callable[[httpx2.Request], httpx2.Response], message: str
) -> None:
    provider = _provider(handler, "ollama", ollama_url=CREDENTIAL_OLLAMA_URL)

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=message.format(status=401))


def test_should_leave_provider_detail_out_for_custom_url() -> None:
    body = {"error": {"message": "invalid model id"}}
    provider = _provider(Recorder(400, body), "ollama")

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400"


def test_should_leave_provider_detail_out_of_key_check_for_custom_url() -> None:
    body = {"error": {"message": "invalid model id"}}
    provider = OpenAICompatProvider(
        kind="openai",
        api_key=OPENAI_TEST_KEY,
        model=GPT_MINI,
        base_url=ELSEWHERE_URL,
        spec=None,
        client=openai_client(Recorder(400, body), ELSEWHERE_URL),
    )

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


@pytest.mark.parametrize(
    "handler", OLLAMA_TAGS_REPLIES.values(), ids=OLLAMA_TAGS_REPLIES.keys()
)
def test_should_name_ollama_serve_when_another_service_answers(
    handler: Callable[[httpx2.Request], httpx2.Response],
) -> None:
    provider = _provider(handler, "ollama")

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=OLLAMA_NOT_RUNNING)


@pytest.mark.parametrize("api_key", BLANK_KEYS, ids=BLANK_KEY_IDS)
@pytest.mark.parametrize(
    ("kind", "label"), [("openai", "OpenAI"), ("openrouter", "OpenRouter")]
)
def test_should_refuse_empty_api_key(
    kind: ProviderName, label: str, api_key: str
) -> None:
    with pytest.raises(ValueError, match=f"^{label} needs an API key$"):
        OpenAICompatProvider(
            kind=kind,
            api_key=api_key,
            model=MODEL_FOR[kind],
            base_url=base_url_for(kind, OLLAMA_URL),
            spec=None,
            client=openai_client(Recorder(200, {})),
        )


@pytest.mark.parametrize("api_key", NON_ASCII_KEYS, ids=NON_ASCII_KEY_IDS)
@pytest.mark.parametrize(
    ("kind", "label"), [("openai", "OpenAI"), ("openrouter", "OpenRouter")]
)
def test_should_refuse_non_ascii_api_key(
    kind: ProviderName, label: str, api_key: str
) -> None:
    with pytest.raises(ValueError, match=f"^{label} API key must be printable ASCII$"):
        OpenAICompatProvider(
            kind=kind,
            api_key=api_key,
            model=MODEL_FOR[kind],
            base_url=base_url_for(kind, OLLAMA_URL),
            spec=None,
            client=openai_client(Recorder(200, {})),
        )
