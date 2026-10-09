from __future__ import annotations

from typing import TYPE_CHECKING

import httpx2
import pytest
from factories import (
    PROMPT,
    Recorder,
    make_assessment,
    openai_client,
    openai_completion,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.base import (
    ClassifyPrompt,
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
ROUTER_MODEL = "vendor/model-one"
OLLAMA_MODEL = "llama3.2"
OLLAMA_URL = "http://box:11434"
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


def _reply(kind: ProviderName = "openai", **choice: object) -> dict[str, object]:
    body = make_assessment().model_dump_json()
    return openai_completion(FIXTURE_FOR[kind], body, **choice)


def _provider(
    handler: Callable[[httpx2.Request], httpx2.Response],
    kind: ProviderName = "openai",
) -> OpenAICompatProvider:
    base_url = base_url_for(kind, OLLAMA_URL)
    model = MODEL_FOR[kind]
    return OpenAICompatProvider(
        kind=kind,
        api_key="sk-test",
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


def test_should_send_same_prompt_cache_key_for_same_system_prompt() -> None:
    other_ad = ClassifyPrompt(system=PROMPT.system, user="other ad text")

    keys = (_sent()["prompt_cache_key"], _sent(prompt=other_ad)["prompt_cache_key"])

    assert keys[0] == keys[1]


def test_should_send_low_reasoning_effort_to_openai() -> None:
    assert _sent()["reasoning_effort"] == "low"


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
    ("kind", "status", "path", "ok"),
    [
        ("openai", 200, f"/v1/models/{GPT_MINI}", True),
        ("openai", 401, f"/v1/models/{GPT_MINI}", False),
        ("openrouter", 200, "/api/v1/key", True),
        ("openrouter", 401, "/api/v1/key", False),
    ],
    ids=["openai key works", "openai rejected", "router key works", "router rejected"],
)
def test_should_check_key_with_provider_endpoint(
    kind: ProviderName, status: int, path: str, ok: bool
) -> None:
    body = {"id": GPT_MINI, "object": "model", "created": 0, "owned_by": "openai"}
    recorder = Recorder(status, body if ok else ERROR_BODY)
    provider = _provider(recorder, kind)

    check = provider.check_key()

    assert (check.ok, recorder.requests[-1].url.path) == (ok, path)


def test_should_raise_provider_error_when_key_check_hits_server_error() -> None:
    provider = _provider(Recorder(500, ERROR_BODY))

    with pytest.raises(ProviderError) as raised:
        provider.check_key()

    assert raised.value.kind == ProviderErrorKind.server


@pytest.mark.parametrize(
    ("installed", "ok"),
    [("llama3.2", True), ("llama3.2:latest", True), ("qwen3:8b", False)],
    ids=["exact name", "latest tag", "other model"],
)
def test_should_check_ollama_tags_for_configured_model(
    installed: str, ok: bool
) -> None:
    recorder = Recorder(200, {"models": [{"name": installed, "model": installed}]})
    provider = _provider(recorder, "ollama")

    check = provider.check_key()

    assert (check.ok, str(recorder.requests[-1].url)) == (
        ok,
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
