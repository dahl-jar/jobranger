from __future__ import annotations

import json
from datetime import date
from typing import TYPE_CHECKING

import pytest
from factories import (
    CALL_USAGE,
    FAKE_LIMITS,
    FAKE_VERSION,
    FakeProvider,
    ListSink,
    make_ad,
    make_assessment,
    make_match,
    targets,
)

from jobranger.events import CancelToken, RunMode
from jobranger.matching.assessment import AdAssessment, Check
from jobranger.matching.classify import (
    Classification,
    ClassifyContext,
    StopReason,
    build_prompt,
    classify_ads,
    to_classification,
)
from jobranger.matching.profile import Profile, education_ids, evidence_ids
from jobranger.providers.base import ProviderError, ProviderErrorKind, StopKind
from jobranger.settings import AppConfig, ProfileRef
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from pathlib import Path

TODAY = date(2026, 10, 9)
SYSTEM = "Locked rules, guidance, limits and profile."


def _context(
    tmp_path: Path,
    provider: FakeProvider,
    profile: Profile,
    cancel: CancelToken | None = None,
) -> ClassifyContext:
    config = AppConfig(
        profile=ProfileRef(path=tmp_path / "profile.yaml"), targets=targets()
    )
    return ClassifyContext(
        store=Store(tmp_path / "ads.db"),
        config=config,
        provider=provider,
        system=SYSTEM,
        evidence=evidence_ids(profile),
        education=education_ids(profile),
        version=FAKE_VERSION,
        mode=RunMode.manual,
        events=ListSink(),
        cancel=cancel or CancelToken(),
        today=TODAY,
        sleep=lambda seconds: None,
        monotonic=lambda: 0.0,
    )


def _ads(count: int) -> list:
    return [
        make_ad(id=f"finn:{index}", source_id=str(index), match=None)
        for index in range(1, count + 1)
    ]


def _convert(profile: Profile, title: str = "Utvikler", **overrides: object) -> object:
    return to_classification(
        make_assessment(**overrides),
        make_ad(title=title),
        evidence_ids(profile),
        education_ids(profile),
        FAKE_LIMITS,
        FAKE_VERSION,
    )


def test_should_send_one_ad_per_provider_call(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider()

    classify_ads(_ads(3), _context(tmp_path, provider, fake_profile))

    assert [
        [ad_id for ad_id in ("finn:1", "finn:2", "finn:3") if ad_id in prompt.user]
        for prompt in provider.prompts
    ] == [["finn:1"], ["finn:2"], ["finn:3"]]


def test_should_send_context_system_text_with_every_ad(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider()

    classify_ads(_ads(2), _context(tmp_path, provider, fake_profile))

    assert [prompt.system for prompt in provider.prompts] == [SYSTEM, SYSTEM]


def test_should_keep_ad_pending_when_provider_raises(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider([ProviderError(ProviderErrorKind.server, "unavailable")])
    context = _context(tmp_path, provider, fake_profile)

    result = classify_ads(_ads(2), context)

    assert (result.stopped, result.pending, result.failed) == (
        StopReason.provider_down,
        2,
        (),
    )


def test_should_stop_with_auth_reason_when_key_is_rejected(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider([ProviderError(ProviderErrorKind.auth, "bad key")])

    result = classify_ads(_ads(1), _context(tmp_path, provider, fake_profile))

    assert result.stopped == StopReason.auth


def test_should_log_provider_failure_kind(
    tmp_path: Path, fake_profile: Profile, caplog: pytest.LogCaptureFixture
) -> None:
    provider = FakeProvider([ProviderError(ProviderErrorKind.server, "down")])

    classify_ads(_ads(1), _context(tmp_path, provider, fake_profile))

    assert [record.getMessage() for record in caplog.records] == [
        "classify.provider.failed model call stopped kind=server"
    ]


def test_should_fail_ad_without_second_call_when_reply_is_invalid(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider([StopKind.invalid])

    result = classify_ads(_ads(1), _context(tmp_path, provider, fake_profile))

    assert (result.failed, len(provider.prompts)) == (("finn:1",), 1)


def test_should_record_usage_through_budget_after_each_call(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider(
        [make_assessment(), ProviderError(ProviderErrorKind.server, "down")]
    )
    context = _context(tmp_path, provider, fake_profile)

    classify_ads(_ads(2), context)

    days = context.store.usage_days(TODAY.isoformat())
    assert [(day.calls, day.ads_checked) for day in days] == [(1, 1)]


def test_should_keep_checks_from_assessment_in_classification(
    fake_profile: Profile,
) -> None:
    checks = [
        {"text": "Java", "state": "met"},
        {"text": "Kubernetes", "state": "partly"},
        {"text": "Norwegian", "state": "missing"},
    ]

    result = _convert(fake_profile, checks=checks)

    assert result.checks == tuple(Check.model_validate(check) for check in checks)


def test_should_stop_classifying_when_cancelled(
    tmp_path: Path, fake_profile: Profile
) -> None:
    cancel = CancelToken()
    cancel.cancel()
    provider = FakeProvider()

    result = classify_ads(_ads(2), _context(tmp_path, provider, fake_profile, cancel))

    assert (result.stopped, result.pending, provider.prompts) == (
        StopReason.cancelled,
        2,
        [],
    )


def test_should_label_lead_title_as_lead_in_classification(
    fake_profile: Profile,
) -> None:
    assert _convert(fake_profile, title="Tech Lead Backend").label == "lead"


def test_should_correct_master_flag_from_explicit_summary(
    fake_profile: Profile,
) -> None:
    result = _convert(
        fake_profile,
        summary="The role requires a relevant master's degree.",
        requires_master=False,
    )

    assert (result.requires_master, result.match.status) == (True, "unsuitable")


def test_should_block_ad_when_description_states_years_above_limit(
    fake_profile: Profile,
) -> None:
    result = to_classification(
        make_assessment(),
        make_ad(description="Krav: minimum 3 års erfaring med Java"),
        evidence_ids(fake_profile),
        education_ids(fake_profile),
        FAKE_LIMITS,
        FAKE_VERSION,
    )

    assert (result.match.status, result.match.required_experience_years) == (
        "unsuitable",
        3.0,
    )


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
def test_should_preserve_degree_alternatives(
    fake_profile: Profile, summary: str
) -> None:
    result = _convert(fake_profile, summary=summary, requires_master=False)

    assert (result.requires_master, result.match.status) == (False, "strong")


def test_should_reject_programming_job_without_role_type(
    fake_profile: Profile,
) -> None:
    assert _convert(fake_profile, role_type=None) is None


def test_should_keep_unknown_fit_when_information_is_missing(
    fake_profile: Profile,
) -> None:
    unknown = dict(make_assessment().match.model_dump(), status="unknown", evidence=[])

    result = _convert(fake_profile, match=unknown)

    assert result.match == make_match(status="unknown", evidence=())


def test_should_copy_assessment_fields_into_classification(
    fake_profile: Profile,
) -> None:
    result = _convert(fake_profile)

    assert result == Classification(
        label="none",
        summary="Build Java services. A bachelor's degree is accepted.",
        requires_master=False,
        is_programming_job=True,
        display_title="Backend Developer",
        role_type="backend",
        technologies=("Java", "Spring Boot"),
        requirements=("Bachelor's degree",),
        match=make_match(),
        checks=(Check(text="Java and Spring Boot services", state="met"),),
    )


def test_should_keep_at_most_five_technologies_and_three_requirements(
    fake_profile: Profile,
) -> None:
    words = [f"Item{index}" for index in range(8)]

    result = _convert(fake_profile, technologies=words, requirements=words)

    assert (len(result.technologies), len(result.requirements)) == (5, 3)


def test_should_reject_programming_job_without_display_title(
    fake_profile: Profile,
) -> None:
    assert _convert(fake_profile, display_title=None) is None


def test_should_not_flag_master_for_empty_summary(fake_profile: Profile) -> None:
    assert _convert(fake_profile, summary="").requires_master is False


def test_should_flag_master_written_with_curly_apostrophe(
    fake_profile: Profile,
) -> None:
    result = _convert(fake_profile, summary="The role requires a master’s degree.")

    assert result.requires_master is True


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("word " * 32 + "x" * 4, ("word " * 31).strip() + "…"),
        ("x" * 160, "x" * 160),
        ("x" * 161, "x" * 159 + "…"),
        ("alpha, " + "b" * 170, "alpha…"),
        ("," * 170, "," * 159 + "…"),
        ("alphaX " + "b" * 170, "alphaX…"),
    ],
    ids=[
        "word boundary",
        "at limit",
        "one word too long",
        "strips comma",
        "only punctuation",
        "word ending in X",
    ],
)
def test_should_truncate_requirement_text(
    fake_profile: Profile, text: str, expected: str
) -> None:
    assert _convert(fake_profile, requirements=[text]).requirements == (expected,)


def test_should_sum_usage_across_calls(tmp_path: Path, fake_profile: Profile) -> None:
    result = classify_ads(_ads(2), _context(tmp_path, FakeProvider(), fake_profile))

    assert result.usage == CALL_USAGE + CALL_USAGE


def test_should_send_assessment_schema_per_call(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider()

    classify_ads(_ads(2), _context(tmp_path, provider, fake_profile))

    assert provider.schemas == [AdAssessment, AdAssessment]


def test_should_fail_truncated_reply_even_with_parsed_body(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider([(StopKind.truncated, make_assessment())])

    result = classify_ads(_ads(1), _context(tmp_path, provider, fake_profile))

    assert result.failed == ("finn:1",)


def test_should_send_ad_fields_as_json() -> None:
    ad = make_ad(description=None)

    prompt = build_prompt(ad, SYSTEM, TODAY.isoformat())

    assert json.loads(prompt.user.splitlines()[-1]) == {
        "id": ad.id,
        "title": ad.title,
        "employer": ad.company,
        "location": ad.location,
        "description": "",
    }


def test_should_send_ad_text_without_unicode_escapes() -> None:
    ad = make_ad(title="Utvikler i Bodø")

    prompt = build_prompt(ad, SYSTEM, TODAY.isoformat())

    assert '"title": "Utvikler i Bodø"' in prompt.user


def test_should_keep_usage_of_calls_made_before_outage(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider(
        [make_assessment(), ProviderError(ProviderErrorKind.server, "down")]
    )

    result = classify_ads(_ads(2), _context(tmp_path, provider, fake_profile))

    assert result.usage == CALL_USAGE


def test_should_keep_usage_when_cancelled_midway(
    tmp_path: Path, fake_profile: Profile
) -> None:
    cancel = CancelToken()

    class CancellingProvider(FakeProvider):
        def classify(self, prompt, schema):
            cancel.cancel()
            return super().classify(prompt, schema)

    result = classify_ads(
        _ads(2), _context(tmp_path, CancellingProvider(), fake_profile, cancel)
    )

    assert (result.usage, result.pending) == (CALL_USAGE, 1)


def test_should_send_run_date_in_each_prompt(
    tmp_path: Path, fake_profile: Profile
) -> None:
    provider = FakeProvider()

    classify_ads(_ads(2), _context(tmp_path, provider, fake_profile))

    assert [TODAY.isoformat() in prompt.user for prompt in provider.prompts] == [
        True,
        True,
    ]
