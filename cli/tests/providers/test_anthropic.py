from __future__ import annotations

import anthropic
import pytest
from factories import (
    PROMPT,
    Recorder,
    anthropic_client,
    anthropic_message,
    make_assessment,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.anthropic import AnthropicProvider
from jobranger.providers.base import (
    ProviderError,
    ProviderErrorKind,
    StopKind,
)
from jobranger.providers.prices import spec_for

SONNET = "claude-sonnet-5-5"
HAIKU_4_5 = "claude-haiku-4-5"
UNLISTED = "claude-unlisted"
MTOK = 1_000_000
ERROR_BODY = {"type": "error", "error": {"type": "error", "message": "failed"}}


def _reply(**overrides: object) -> dict[str, object]:
    return anthropic_message(make_assessment().model_dump_json(), **overrides)


def _provider(recorder: Recorder, model: str = SONNET) -> AnthropicProvider:
    return AnthropicProvider(
        api_key="sk-ant-test",
        model=model,
        spec=spec_for("anthropic", model, None),
        client=anthropic_client(recorder),
    )


def _sent(model: str = SONNET) -> dict[str, object]:
    recorder = Recorder(200, _reply())
    provider = _provider(recorder, model)

    provider.classify(PROMPT, AdAssessment)

    return recorder.sent()


def _error(status: int, **headers: str) -> ProviderError:
    provider = _provider(Recorder(status, ERROR_BODY, **headers))
    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)
    return raised.value


def test_should_send_system_prompt_as_cached_block() -> None:
    sent = _sent()

    assert sent["system"] == [
        {"type": "text", "text": PROMPT.system, "cache_control": {"type": "ephemeral"}}
    ]


def test_should_send_ad_as_user_message() -> None:
    sent = _sent()

    assert sent["messages"] == [{"role": "user", "content": PROMPT.user}]


def test_should_send_low_effort_with_transformed_schema() -> None:
    config = _sent()["output_config"]

    assert config == {
        "effort": "low",
        "format": {
            "type": "json_schema",
            "schema": anthropic.transform_schema(AdAssessment),
        },
    }


def test_should_omit_effort_for_haiku_4_5() -> None:
    config = _sent(HAIKU_4_5)["output_config"]

    assert "effort" not in config


def test_should_return_parsed_assessment() -> None:
    provider = _provider(Recorder(200, _reply()))

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.stop, result.parsed) == (StopKind.done, make_assessment())


def test_should_compute_usage_from_cache_creation_breakdown() -> None:
    body = _reply(
        input_tokens=0,
        cache_read_input_tokens=0,
        output_tokens=0,
        cache_creation_input_tokens=2 * MTOK,
        cache_creation={
            "ephemeral_5m_input_tokens": MTOK,
            "ephemeral_1h_input_tokens": MTOK,
        },
    )
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.usage.cost_usd == pytest.approx(6.5)


def test_should_price_cache_writes_as_5m_when_breakdown_is_missing() -> None:
    body = _reply(
        input_tokens=0,
        cache_read_input_tokens=0,
        output_tokens=0,
        cache_creation_input_tokens=MTOK,
        cache_creation=None,
    )
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.usage.cost_usd == pytest.approx(2.5)


def test_should_mark_usage_unpriced_without_spec() -> None:
    provider = _provider(Recorder(200, _reply()), UNLISTED)

    result = provider.classify(PROMPT, AdAssessment)

    assert result.usage.priced is False


def test_should_return_refusal_stop_without_parsed_output() -> None:
    body = _reply(stop_reason="refusal")
    body["content"] = [{"type": "text", "text": "I can't help with that."}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.stop, result.parsed) == (StopKind.refusal, None)


def test_should_return_truncated_stop_when_output_hits_max_tokens() -> None:
    body = _reply(stop_reason="max_tokens")
    body["content"] = [{"type": "text", "text": '{"is_programming_job": tr'}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.stop == StopKind.truncated


def test_should_keep_usage_when_reply_is_not_valid_json() -> None:
    body = _reply()
    body["content"] = [{"type": "text", "text": "not json"}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.stop, result.usage.output_tokens) == (StopKind.invalid, 400)


def test_should_map_529_to_overloaded_error() -> None:
    assert _error(529).kind == ProviderErrorKind.overloaded


@pytest.mark.parametrize(
    ("header", "expected"),
    [("30", 30.0), ("Wed, 21 Oct 2015 07:28:00 GMT", 0.0), ("inf", None)],
    ids=["seconds", "past date", "not finite"],
)
def test_should_read_retry_after_on_rate_limit(
    header: str, expected: float | None
) -> None:
    error = _error(429, **{"retry-after": header})

    assert (error.kind, error.retry_after) == (ProviderErrorKind.rate_limit, expected)


@pytest.mark.parametrize(
    ("status", "ok"),
    [(200, True), (401, False), (404, False)],
    ids=["key works", "rejected key", "unknown model"],
)
def test_should_check_key_against_chosen_model(status: int, ok: bool) -> None:
    body = {"id": SONNET, "type": "model"} if ok else ERROR_BODY
    recorder = Recorder(status, body)
    provider = _provider(recorder)

    check = provider.check_key()

    assert (check.ok, recorder.requests[-1].url.path) == (ok, f"/v1/models/{SONNET}")


def test_should_raise_provider_error_when_key_check_hits_server_error() -> None:
    provider = _provider(Recorder(500, ERROR_BODY))

    with pytest.raises(ProviderError) as raised:
        provider.check_key()

    assert raised.value.kind == ProviderErrorKind.server
