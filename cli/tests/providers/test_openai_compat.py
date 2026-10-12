from __future__ import annotations

from typing import TYPE_CHECKING

import httpx2
import pytest
from factories import (
    BLANK_KEY_IDS,
    BLANK_KEYS,
    NON_ASCII_KEY_IDS,
    NON_ASCII_KEYS,
    OLLAMA_URL,
    OPENAI_TEST_KEY,
    PROMPT,
    ROUTER_MODEL,
    SHORT_RETRY_AFTER,
    Recorder,
    make_assessment,
    openai_client,
    openai_completion,
    route_sdk_network,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.base import (
    KEY_CHECK_MESSAGES,
    ClassifyPrompt,
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
BAD_REQUEST_CHECK = KEY_CHECK_MESSAGES[ProviderErrorKind.bad_request].format(
    provider="OpenAI", model=GPT_MINI
)
OLLAMA_HAS_MODEL = KeyCheck(True, f"Ollama has {OLLAMA_MODEL}")
OLLAMA_MISSING_MODEL = KeyCheck(
    False,
    f"Ollama has no model named {OLLAMA_MODEL}. "
    f"Pull it with: ollama pull {OLLAMA_MODEL}",
)


def _reply(kind: ProviderName = "openai", **choice: object) -> dict[str, object]:
    body = make_assessment().model_dump_json()
    return openai_completion(FIXTURE_FOR[kind], body, **choice)


def _provider(
    handler: Callable[[httpx2.Request], httpx2.Response],
    kind: ProviderName = "openai",
    model: str | None = None,
) -> OpenAICompatProvider:
    base_url = base_url_for(kind, OLLAMA_URL)
    model = model or MODEL_FOR[kind]
    return OpenAICompatProvider(
        kind=kind,
        api_key=OPENAI_TEST_KEY,
        model=model,
        base_url=base_url,
        spec=spec_for(kind, model, None),
        client=openai_client(handler, base_url),
    )


def _sent(
    kind: ProviderName = "openai", prompt: ClassifyPrompt = PROMPT
) -> dict[str, object]:
    recorder = Recorder(200, _reply(kind))
    provider = _provider(recorder, kind)

    provider.classify(prompt, AdAssessment)

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
    assert _sent()["messages"] == [
        {"role": "system", "content": PROMPT.system},
        {"role": "user", "content": PROMPT.user},
    ]


def test_should_send_chosen_model_with_output_limit() -> None:
    sent = _sent()

    assert (sent["model"], sent["max_completion_tokens"]) == (GPT_MINI, 4096)


def test_should_send_same_prompt_cache_key_for_same_system_prompt() -> None:
    other_ad = ClassifyPrompt(system=PROMPT.system, user="other ad text")

    keys = (_sent()["prompt_cache_key"], _sent(prompt=other_ad)["prompt_cache_key"])

    assert keys[0] == keys[1]


def test_should_send_low_reasoning_effort_to_openai() -> None:
    assert _sent()["reasoning_effort"] == "low"


def test_should_omit_reasoning_effort_for_unlisted_openai_model() -> None:
    recorder = Recorder(200, _reply())
    provider = _provider(recorder, model=UNLISTED_MODEL)

    provider.classify(PROMPT, AdAssessment)

    assert "reasoning_effort" not in recorder.sent()


def test_should_send_zero_temperature_to_ollama() -> None:
    assert _sent("ollama")["temperature"] == 0


def test_should_require_parameters_and_deny_data_collection_on_openrouter() -> None:
    assert _sent("openrouter")["provider"] == {
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


def test_should_record_ollama_tokens_at_zero_cost() -> None:
    usage = _classify("ollama", _reply("ollama")).usage

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

    usage = _classify("openai", body).usage

    assert (
        usage.calls,
        usage.input_tokens,
        usage.cache_read_tokens,
        usage.output_tokens,
    ) == expected


def test_should_mark_usage_unpriced_for_unlisted_model() -> None:
    provider = _provider(Recorder(200, _reply()), model=UNLISTED_MODEL)

    usage = provider.classify(PROMPT, AdAssessment).usage

    assert (usage.priced, usage.cost_usd) == (False, 0.0)


def test_should_mark_usage_unpriced_when_reply_has_no_usage() -> None:
    body = _reply()
    del body["usage"]

    usage = _classify("openai", body).usage

    assert (usage.calls, usage.priced) == (1, False)


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
    body = {"id": GPT_MINI, "object": "model", "created": 0, "owned_by": "openai"}
    recorder = Recorder(status, body if expected.ok else ERROR_BODY)
    provider = _provider(recorder, kind)

    check = provider.check_key()

    assert (check, recorder.requests[-1].url.path) == (expected, path)


def test_should_send_saved_key_to_openrouter_with_request_timeout(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    recorder = Recorder(200, {"data": {}})
    route_sdk_network(monkeypatch, recorder)
    provider = OpenAICompatProvider(
        kind="openrouter",
        api_key=OPENAI_TEST_KEY,
        model=ROUTER_MODEL,
        base_url=base_url_for("openrouter", OLLAMA_URL),
        spec=None,
    )

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


def test_should_retry_failed_call_four_times(monkeypatch: pytest.MonkeyPatch) -> None:
    recorder = Recorder(500, ERROR_BODY, **SHORT_RETRY_AFTER)
    route_sdk_network(monkeypatch, recorder)
    provider = OpenAICompatProvider(
        kind="openai",
        api_key=OPENAI_TEST_KEY,
        model=GPT_MINI,
        base_url=base_url_for("openai", OLLAMA_URL),
        spec=None,
    )

    with pytest.raises(ProviderError):
        provider.check_key()

    assert len(recorder.requests) == 5


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


def test_should_raise_provider_error_when_key_check_hits_server_error() -> None:
    provider = _provider(Recorder(500, ERROR_BODY))

    with pytest.raises(ProviderError) as raised:
        provider.check_key()

    assert raised.value.kind == ProviderErrorKind.server


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
    def refuse(request: httpx2.Request) -> httpx2.Response:
        raise httpx2.ConnectError("connection refused", request=request)

    check = _provider(refuse, "ollama").check_key()

    assert "ollama serve" in check.message


def test_should_name_ollama_serve_when_another_service_answers() -> None:
    provider = _provider(Recorder(200, {}), "ollama")

    check = provider.check_key()

    assert (check.ok, "ollama serve" in check.message) == (False, True)


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
