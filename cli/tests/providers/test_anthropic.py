from __future__ import annotations

from typing import TYPE_CHECKING

import anthropic
import httpx2
import pytest
from factories import (
    ANTHROPIC_TEST_KEY,
    BLANK_KEY_IDS,
    BLANK_KEYS,
    ELSEWHERE_URL,
    MASKED_KEY_MESSAGE,
    NON_ASCII_KEY_IDS,
    NON_ASCII_KEYS,
    PROMPT,
    Recorder,
    anthropic_client,
    anthropic_message,
    anthropic_model,
    make_assessment,
    route_sdk_network,
)

from jobranger.matching.assessment import AdAssessment
from jobranger.providers.anthropic import AnthropicProvider
from jobranger.providers.base import (
    DETAIL_MAX_CHARS,
    KEY_CHECK_MESSAGES,
    KEY_FRAGMENT_CHARS,
    KeyCheck,
    ProviderError,
    ProviderErrorKind,
    StopKind,
)
from jobranger.providers.prices import spec_for

if TYPE_CHECKING:
    from collections.abc import Callable

SONNET = "claude-sonnet-5-5"
HAIKU_4_5 = "claude-haiku-4-5"
UNLISTED = "claude-unlisted"
MTOK = 1_000_000
FULLWIDTH_OFFSET = 0xFEE0
KEY_GROUP_CHARS = 10
TEXT_SPLIT = 10
ERROR_BODY = {"type": "error", "error": {"type": "error", "message": "failed"}}
UNSCOPED_KEY_MESSAGE = (
    "This API key is not scoped to a workspace, so this request must include "
    "the anthropic-workspace-id header"
)
BAD_REQUEST_CHECK = KEY_CHECK_MESSAGES[ProviderErrorKind.bad_request].format(
    provider="Anthropic", model=SONNET
)


def _bad_request(message: str) -> dict[str, object]:
    return {
        "type": "error",
        "error": {"type": "invalid_request_error", "message": message},
    }


def _reply(**overrides: object) -> dict[str, object]:
    return anthropic_message(make_assessment().model_dump_json(), **overrides)


def _provider(
    handler: Callable[[httpx2.Request], httpx2.Response], model: str = SONNET
) -> AnthropicProvider:
    return AnthropicProvider(
        api_key=ANTHROPIC_TEST_KEY,
        model=model,
        spec=spec_for("anthropic", model, None),
        client=anthropic_client(handler),
    )


def _live_provider() -> AnthropicProvider:
    return AnthropicProvider(api_key=ANTHROPIC_TEST_KEY, model=SONNET, spec=None)


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


def test_should_send_chosen_model() -> None:
    sent = _sent()

    assert sent["model"] == SONNET


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

    assert (result.usage.priced, result.usage.cost_usd) == (False, 0.0)


@pytest.mark.parametrize(
    ("cache_fields", "expected"),
    [
        (
            {
                "cache_creation_input_tokens": 300,
                "cache_creation": {
                    "ephemeral_5m_input_tokens": 200,
                    "ephemeral_1h_input_tokens": 100,
                },
                "cache_read_input_tokens": 2500,
            },
            (1, 500, 300, 2500, 400),
        ),
        (
            {
                "cache_creation_input_tokens": None,
                "cache_creation": None,
                "cache_read_input_tokens": None,
            },
            (1, 500, 0, 0, 400),
        ),
    ],
    ids=["cache breakdown", "no cache fields"],
)
def test_should_count_tokens_from_reply(
    cache_fields: dict[str, object], expected: tuple[int, ...]
) -> None:
    provider = _provider(Recorder(200, _reply(**cache_fields)))

    result = provider.classify(PROMPT, AdAssessment)

    usage = result.usage
    assert (
        usage.calls,
        usage.input_tokens,
        usage.cache_write_tokens,
        usage.cache_read_tokens,
        usage.output_tokens,
    ) == expected


def test_should_return_refusal_stop_without_parsed_output() -> None:
    body = _reply(stop_reason="refusal")
    body["content"] = [{"type": "text", "text": "I can't help with that."}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.stop, result.parsed) == (StopKind.refusal, None)


def test_should_keep_usage_when_model_refuses() -> None:
    body = _reply(stop_reason="refusal")
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.usage.output_tokens == 400


def test_should_return_truncated_stop_when_output_hits_max_tokens() -> None:
    body = _reply(stop_reason="max_tokens")
    body["content"] = [{"type": "text", "text": '{"is_programming_job": tr'}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.stop == StopKind.truncated


def test_should_join_text_blocks_into_one_reply() -> None:
    text = make_assessment().model_dump_json()
    body = _reply()
    body["content"] = [
        {"type": "text", "text": text[:TEXT_SPLIT]},
        {"type": "text", "text": text[TEXT_SPLIT:]},
    ]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert result.parsed == make_assessment()


def test_should_keep_usage_when_reply_is_not_valid_json() -> None:
    body = _reply()
    body["content"] = [{"type": "text", "text": "not json"}]
    provider = _provider(Recorder(200, body))

    result = provider.classify(PROMPT, AdAssessment)

    assert (result.stop, result.usage.output_tokens) == (StopKind.invalid, 400)


@pytest.mark.parametrize(
    ("status", "kind", "message"),
    [
        (403, ProviderErrorKind.auth, "HTTP 403"),
        (413, ProviderErrorKind.bad_request, "HTTP 413: failed"),
        (422, ProviderErrorKind.bad_request, "HTTP 422: failed"),
        (529, ProviderErrorKind.overloaded, "HTTP 529: failed"),
    ],
)
def test_should_map_status_to_error_kind(
    status: int, kind: ProviderErrorKind, message: str
) -> None:
    error = _error(status)

    assert (error.kind, error.message) == (kind, message)


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
    ("status", "expected"),
    [
        (200, KeyCheck(ok=True, message="Key works")),
        (401, KeyCheck(ok=False, message="Anthropic rejected the key")),
        (404, KeyCheck(ok=False, message=f"Anthropic has no model named {SONNET}")),
    ],
    ids=["key works", "rejected key", "unknown model"],
)
def test_should_check_key_against_chosen_model(status: int, expected: KeyCheck) -> None:
    body = anthropic_model(SONNET) if expected.ok else ERROR_BODY
    recorder = Recorder(status, body)
    provider = _provider(recorder)

    check = provider.check_key()

    assert (check, recorder.requests[-1].url.path) == (
        expected,
        f"/v1/models/{SONNET}",
    )


def test_should_send_saved_key_with_request_timeout(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    recorder = Recorder(200, anthropic_model(SONNET))
    route_sdk_network(monkeypatch, recorder)
    provider = _live_provider()

    provider.check_key()

    request = recorder.requests[-1]
    assert (request.headers.get("x-api-key"), request.extensions["timeout"]) == (
        ANTHROPIC_TEST_KEY,
        {"connect": 120, "read": 120, "write": 120, "pool": 120},
    )


def test_should_send_key_without_surrounding_whitespace(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    recorder = Recorder(200, anthropic_model(SONNET))
    route_sdk_network(monkeypatch, recorder)
    provider = AnthropicProvider(
        api_key=f"  {ANTHROPIC_TEST_KEY}\n", model=SONNET, spec=None
    )

    provider.check_key()

    assert recorder.requests[-1].headers.get("x-api-key") == ANTHROPIC_TEST_KEY


def test_should_call_official_api_when_base_url_env_points_elsewhere(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    recorder = Recorder(200, anthropic_model(SONNET))
    route_sdk_network(monkeypatch, recorder)
    monkeypatch.setenv("ANTHROPIC_BASE_URL", ELSEWHERE_URL)
    provider = _live_provider()

    provider.check_key()

    assert str(recorder.requests[-1].url) == (
        f"https://api.anthropic.com/v1/models/{SONNET}"
    )


def test_should_leave_provider_detail_out_of_error_when_it_contains_the_key() -> None:
    body = _bad_request(f"invalid key {ANTHROPIC_TEST_KEY}")
    provider = _provider(Recorder(400, body))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400"


def test_should_keep_clean_provider_detail_in_error() -> None:
    provider = _provider(Recorder(400, _bad_request("invalid\nmodel id")))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400: invalid model id"


def test_should_leave_provider_detail_out_of_auth_error() -> None:
    body = {"type": "error", "error": {"message": MASKED_KEY_MESSAGE}}
    provider = _provider(Recorder(401, body))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 401"


def _custom_url_provider(body: object) -> AnthropicProvider:
    return AnthropicProvider(
        api_key=ANTHROPIC_TEST_KEY,
        model=SONNET,
        spec=None,
        client=anthropic_client(Recorder(400, body), ELSEWHERE_URL),
    )


def test_should_leave_provider_detail_out_for_custom_url() -> None:
    provider = _custom_url_provider(_bad_request(UNSCOPED_KEY_MESSAGE))

    with pytest.raises(ProviderError) as raised:
        provider.classify(PROMPT, AdAssessment)

    assert raised.value.message == "HTTP 400"


def test_should_leave_provider_detail_out_of_key_check_for_custom_url() -> None:
    provider = _custom_url_provider(_bad_request(UNSCOPED_KEY_MESSAGE))

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


def test_should_reject_key_when_not_scoped_to_workspace() -> None:
    provider = _provider(Recorder(400, _bad_request(UNSCOPED_KEY_MESSAGE)))

    check = provider.check_key()

    assert check == KeyCheck(
        ok=False,
        message=f"{BAD_REQUEST_CHECK}. Anthropic said: {UNSCOPED_KEY_MESSAGE}",
    )


@pytest.mark.parametrize(
    "shown_key",
    [
        ANTHROPIC_TEST_KEY,
        ANTHROPIC_TEST_KEY.upper(),
        ANTHROPIC_TEST_KEY.replace("-", "%2D"),
        f"{'a' * (DETAIL_MAX_CHARS - 3)}{ANTHROPIC_TEST_KEY}",
        " ".join(
            ANTHROPIC_TEST_KEY[start : start + KEY_GROUP_CHARS]
            for start in range(0, len(ANTHROPIC_TEST_KEY), KEY_GROUP_CHARS)
        ),
        ANTHROPIC_TEST_KEY[-KEY_FRAGMENT_CHARS:],
        ANTHROPIC_TEST_KEY.replace("-", "%252D"),
        "".join(chr(ord(char) + FULLWIDTH_OFFSET) for char in ANTHROPIC_TEST_KEY),
        ANTHROPIC_TEST_KEY.replace("-", "&#45;"),
    ],
    ids=[
        "exact",
        "uppercased",
        "url-encoded",
        "across the cut",
        "spaced every 10 chars",
        "last 12 chars",
        "double url-encoded",
        "fullwidth",
        "html entity dash",
    ],
)
def test_should_leave_provider_detail_out_when_it_contains_the_key(
    shown_key: str,
) -> None:
    provider = _provider(Recorder(400, _bad_request(shown_key)))

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


def test_should_leave_provider_detail_out_when_it_is_empty() -> None:
    provider = _provider(Recorder(400, _bad_request(" \n ")))

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


@pytest.mark.parametrize(
    "reply",
    [
        {"text": "Bad Request"},
        {"json": {"type": "error", "error": {"type": "invalid_request_error"}}},
        {"json": {"error": {"message": 123}}},
    ],
    ids=["plain text", "no message", "non-string message"],
)
def test_should_leave_provider_detail_out_when_body_has_no_message(
    reply: dict[str, object],
) -> None:
    provider = _provider(lambda request: httpx2.Response(400, **reply))

    check = provider.check_key()

    assert check == KeyCheck(ok=False, message=BAD_REQUEST_CHECK)


def test_should_strip_control_characters_from_provider_detail() -> None:
    message = "bad\x1b[31m request\x07 here\u202e"
    provider = _provider(Recorder(400, _bad_request(message)))

    check = provider.check_key()

    assert check.message == f"{BAD_REQUEST_CHECK}. Anthropic said: bad[31m request here"


def test_should_cut_provider_detail_to_limit() -> None:
    provider = _provider(Recorder(400, _bad_request("a" * (DETAIL_MAX_CHARS + 1))))

    check = provider.check_key()

    assert check.message == f"{BAD_REQUEST_CHECK}. Anthropic said: {'a' * 300}"


@pytest.mark.parametrize("api_key", BLANK_KEYS, ids=BLANK_KEY_IDS)
def test_should_refuse_empty_api_key(api_key: str) -> None:
    with pytest.raises(ValueError, match="^Anthropic needs an API key$"):
        AnthropicProvider(
            api_key=api_key,
            model=SONNET,
            spec=None,
            client=anthropic_client(Recorder(200, {})),
        )


@pytest.mark.parametrize("api_key", NON_ASCII_KEYS, ids=NON_ASCII_KEY_IDS)
def test_should_refuse_non_ascii_api_key(api_key: str) -> None:
    with pytest.raises(ValueError, match="^Anthropic API key must be printable ASCII$"):
        AnthropicProvider(
            api_key=api_key,
            model=SONNET,
            spec=None,
            client=anthropic_client(Recorder(200, {})),
        )
