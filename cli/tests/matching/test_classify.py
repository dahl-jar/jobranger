from __future__ import annotations

import json
import subprocess
from unittest.mock import ANY, patch

import pytest
from factories import classification_payload, cli_output, make_ad, make_match

from jobranger.matching.classify import (
    PROMPT_HEADER,
    Classification,
    Usage,
    classify_batch,
    parse_response,
)


def _response(ad_id: str = "finn:1", **overrides: object) -> str:
    return json.dumps({ad_id: classification_payload(**overrides)})


def _completed(
    ad_id: str = "finn:1", **overrides: object
) -> subprocess.CompletedProcess[str]:
    return _finished(cli_output(_response(ad_id, **overrides)))


def _finished(stdout: str) -> subprocess.CompletedProcess[str]:
    return subprocess.CompletedProcess(args=[], returncode=0, stdout=stdout, stderr="")


def test_should_run_claude_sonnet_with_medium_effort() -> None:
    ad = make_ad(id="finn:1", source_id="1", title="Developer")

    with patch(
        "jobranger.matching.classify.subprocess.run", return_value=_completed()
    ) as run:
        results = classify_batch([ad], "/usr/local/bin/claude").assessments

    run.assert_called_once_with(
        [
            "/usr/local/bin/claude",
            "-p",
            "--model",
            "claude-sonnet-5-5",
            "--effort",
            "medium",
            "--tools",
            "",
            "--no-session-persistence",
            "--output-format",
            "json",
        ],
        input=ANY,
        capture_output=True,
        text=True,
        timeout=300,
        check=True,
    )
    assert results == {
        "finn:1": Classification(
            label="open",
            summary="Build Java services. A bachelor's degree is accepted.",
            requires_master=False,
            is_programming_job=True,
            display_title="Backend Developer",
            role_type="backend",
            technologies=("Java", "Spring Boot"),
            requirements=("Bachelor's degree",),
            match=make_match(),
        )
    }


def test_should_return_empty_for_malformed_response() -> None:
    assert parse_response("sorry, I can't help with that") == {}


def test_should_keep_valid_classifications_from_mixed_response() -> None:
    payload = {
        "finn:1": classification_payload(),
        "finn:2": classification_payload(label="guru"),
        "nav:3": classification_payload(is_programming_job=False),
    }

    results = parse_response("Here you go:\n" + json.dumps(payload))

    assert set(results) == {"finn:1", "nav:3"}
    assert results["finn:1"].match == make_match()
    assert results["nav:3"].display_title is None
    assert results["nav:3"].role_type is None


def test_should_correct_master_flag_from_explicit_summary() -> None:
    response = _response(
        summary="The role requires a relevant master's degree.",
        requires_master=False,
    )

    result = parse_response(response)["finn:1"]

    assert result.requires_master is True
    assert result.match.status == "unsuitable"


@pytest.mark.parametrize(
    "summary",
    [
        "It requires a bachelor's or master's degree in IT.",
        "It requires a master's degree or equivalent practical experience.",
        "Applicants with a relevant master's degree are preferred.",
        "The role does not require a master's degree.",
        "A master's degree is not mandatory; a bachelor's degree is accepted.",
    ],
)
def test_should_preserve_degree_alternatives(summary: str) -> None:
    result = parse_response(_response(summary=summary, requires_master=False))

    assert result["finn:1"].requires_master is False
    assert result["finn:1"].match.status == "strong"


def test_should_keep_complete_requirement_beyond_sixty_characters() -> None:
    requirement = (
        "Relevant education in IT, computer science, data, engineering, or mathematics"
    )

    result = parse_response(_response(requirements=[requirement]))

    assert result["finn:1"].requirements == (requirement,)


def test_should_truncate_oversized_requirement_at_word_boundary() -> None:
    requirement = " ".join(["experience"] * 30)

    result = parse_response(_response(requirements=[requirement]))

    stored = result["finn:1"].requirements[0]
    assert len(stored) <= 160
    assert stored.endswith("experience…")


@pytest.mark.parametrize(
    "title",
    [
        "Senior Developer",
        "Seniorutvikler",
        "Tech Lead",
        "Erfaren utvikler",
        "Experienced Engineer",
    ],
)
def test_should_override_ai_label_for_senior_title(title: str) -> None:
    ad = make_ad(id="finn:1", title=title)

    with patch(
        "jobranger.matching.classify.subprocess.run",
        return_value=_completed(label="junior"),
    ):
        results = classify_batch([ad], "claude").assessments

    assert results[ad.id].label == "senior"


@pytest.mark.parametrize("title", ["Utvikler", "Software Engineer", "Developer"])
def test_should_keep_generic_coding_titles_open_to_junior(title: str) -> None:
    ad = make_ad(id="finn:1", title=title)

    with patch("jobranger.matching.classify.subprocess.run", return_value=_completed()):
        results = classify_batch([ad], "claude").assessments

    assert results[ad.id].label == "open"


def test_should_retry_only_missing_results() -> None:
    first = make_ad(id="finn:1")
    second = make_ad(id="finn:2")

    with patch(
        "jobranger.matching.classify.subprocess.run",
        side_effect=[_completed(), _completed(second.id)],
    ) as run:
        results = classify_batch([first, second], "claude").assessments

    assert set(results) == {first.id, second.id}
    assert run.call_count == 2
    retry = run.call_args_list[1]
    assert "claude-sonnet-5-5" in retry.args[0]
    assert second.id in retry.kwargs["input"]
    assert first.id not in retry.kwargs["input"]


@pytest.mark.parametrize(
    "overrides",
    [
        {"role_type": "blockchain_wizard"},
        {"match": None},
        {"requires_master": "false"},
        {"is_programming_job": "true"},
    ],
)
def test_should_reject_invalid_classification(overrides: dict[str, object]) -> None:
    assert parse_response(_response(**overrides)) == {}


def test_should_keep_unknown_fit_when_information_is_missing() -> None:
    unknown = dict(classification_payload()["match"], status="unknown", evidence=[])

    with patch(
        "jobranger.matching.classify.subprocess.run",
        return_value=_completed(match=unknown),
    ) as run:
        results = classify_batch([make_ad(id="finn:1")], "claude").assessments

    assert results["finn:1"].match.status == "unknown"
    run.assert_called_once()


@pytest.mark.parametrize(
    "failure",
    [
        subprocess.TimeoutExpired("claude", 300),
        subprocess.CalledProcessError(1, "claude"),
        OSError("unavailable"),
    ],
)
def test_should_bound_retries_when_model_fails(failure: Exception) -> None:
    with patch(
        "jobranger.matching.classify.subprocess.run", side_effect=failure
    ) as run:
        batch = classify_batch([make_ad(id="finn:1")], "claude")

    assert batch.assessments == {}
    assert batch.usage == Usage(calls=3)
    assert run.call_count == 3
    assert [
        call.args[0][call.args[0].index("--model") + 1] for call in run.call_args_list
    ] == [
        "claude-sonnet-5-5",
        "claude-sonnet-5-5",
        "claude-sonnet-5-5",
    ]


def test_should_ignore_unsolicited_ad_ids() -> None:
    with patch(
        "jobranger.matching.classify.subprocess.run",
        return_value=_completed("injected"),
    ):
        results = classify_batch([make_ad(id="finn:1")], "claude").assessments

    assert results == {}


def test_should_list_monitoring_operations_as_non_programming_in_prompt() -> None:
    assert "monitoring/observability platform operation" in PROMPT_HEADER
    assert "administering purchased systems" in PROMPT_HEADER


def test_should_sum_token_usage_across_chunks() -> None:
    ads = [make_ad(id=f"finn:{index}") for index in range(21)]
    first_chunk = json.dumps({ad.id: classification_payload() for ad in ads[:20]})
    outputs = [
        _finished(
            cli_output(
                first_chunk,
                cost_usd=0.5,
                input_tokens=10,
                cache_creation_input_tokens=2_000,
                cache_read_input_tokens=30_000,
                output_tokens=400,
            )
        ),
        _finished(
            cli_output(
                _response(ads[20].id),
                cost_usd=0.25,
                input_tokens=3,
                cache_creation_input_tokens=100,
                cache_read_input_tokens=5_000,
                output_tokens=50,
            )
        ),
    ]

    with patch("jobranger.matching.classify.subprocess.run", side_effect=outputs):
        batch = classify_batch(ads, "claude")

    assert len(batch.assessments) == 21
    assert batch.usage == Usage(
        calls=2,
        input_tokens=13,
        cache_write_tokens=2_100,
        cache_read_tokens=35_000,
        output_tokens=450,
        cost_usd=0.75,
    )
    assert batch.usage.total_tokens == 37_563


@pytest.mark.parametrize(
    "stdout",
    [
        "oops",
        '{"result": "{}"}',
        '{"result": "{}", "total_cost_usd": 0, "usage": null}',
        '{"result": "{}", "total_cost_usd": 0, "usage": {"input_tokens": "n/a"}}',
    ],
)
def test_should_keep_ads_pending_when_cli_output_is_unreadable(stdout: str) -> None:
    with patch(
        "jobranger.matching.classify.subprocess.run", return_value=_finished(stdout)
    ) as run:
        batch = classify_batch([make_ad(id="finn:1")], "claude")

    assert batch.assessments == {}
    assert run.call_count == 3
    assert batch.usage == Usage(calls=3)


def test_should_count_tokens_when_result_text_is_unusable() -> None:
    unusable = cli_output("rate limited", cost_usd=0.1, input_tokens=7)

    with patch(
        "jobranger.matching.classify.subprocess.run", return_value=_finished(unusable)
    ):
        batch = classify_batch([make_ad(id="finn:1")], "claude")

    assert batch.assessments == {}
    assert batch.usage.input_tokens == 21
    assert batch.usage.calls == 3
