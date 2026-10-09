from __future__ import annotations

import json

import pytest
from factories import classification_payload, make_ad, make_match

from jobranger.matching.classify import build_prompt
from jobranger.matching.rules import (
    MATCHING_RULES,
    PROFILE_VERSION,
    decode_match,
    encode_match,
    is_reportable,
    parse_match,
)


def test_should_include_profile_evidence_in_prompt() -> None:
    prompt = build_prompt([make_ad(description="Build software.")])

    for evidence in (
        "Employer One AS",
        "Java project",
        "Cloud project",
        "Django project",
        "Android project",
        "C++ project",
    ):
        assert evidence in prompt
    assert "2025-03" in prompt


def test_should_preserve_qualifications_at_end_of_ad() -> None:
    qualification = "A completed master's degree is mandatory."
    description = "About our company. " * 1000 + qualification

    prompt = build_prompt([make_ad(description=description)])

    assert qualification in prompt
    assert description in prompt


@pytest.mark.parametrize(
    "overrides",
    [
        {"status": "excellent"},
        {"evidence": ["imaginary-sap-certification"]},
        {"evidence": []},
        {"evidence": ["education"]},
        {"evidence": "java_project"},
        {"reason": " "},
        {"reason": 42},
        {"gaps": "none"},
        {"blockers": None},
        {"blockers": [42]},
        {"blockers": [" "]},
        {"is_low_code": "false"},
        {"gaps": ["Mandatory qualification not verified"]},
        {"required_experience_years": "3"},
        {"required_experience_years": True},
        {"required_experience_years": -1},
        {"required_experience_years": float("inf")},
        {"required_experience_years": float("nan")},
    ],
)
def test_should_reject_invalid_match(overrides: dict[str, object]) -> None:
    payload = dict(classification_payload()["match"])
    payload.update(overrides)

    assert parse_match(payload) is None


@pytest.mark.parametrize(
    "field",
    [
        "status",
        "reason",
        "evidence",
        "gaps",
        "blockers",
        "is_low_code",
        "required_experience_years",
    ],
)
def test_should_reject_missing_match_field(field: str) -> None:
    payload = dict(classification_payload()["match"])
    del payload[field]

    assert parse_match(payload) is None


@pytest.mark.parametrize(
    "overrides",
    [{"blockers": ["Mandatory SAP experience not documented"]}, {"is_low_code": True}],
)
def test_should_demote_hard_mismatch(overrides: dict[str, object]) -> None:
    payload = dict(classification_payload()["match"])
    payload.update(overrides)

    result = parse_match(payload)

    assert result is not None
    assert result.status == "unsuitable"


def test_should_preserve_assessment_when_serializing() -> None:
    match = make_match(status="stretch", gaps=("Qt experience not documented",))

    result = decode_match(encode_match(match))

    assert result == match
    assert result.version == PROFILE_VERSION


@pytest.mark.parametrize(("length", "valid"), [(300, True), (301, False)])
@pytest.mark.parametrize("field", ["reason", "gaps"])
def test_should_enforce_match_text_limit(field: str, length: int, valid: bool) -> None:
    payload = dict(classification_payload()["match"], status="stretch")
    payload[field] = "x" * length if field == "reason" else ["x" * length]

    result = parse_match(payload)

    assert (result is not None) is valid


@pytest.mark.parametrize(("count", "valid"), [(5, True), (6, False)])
def test_should_enforce_evidence_count_limit(count: int, valid: bool) -> None:
    evidence = [
        "java_project",
        "cloud_project",
        "django_project",
        "android_project",
        "cpp_project",
        "work_data",
    ]
    payload = dict(classification_payload()["match"], evidence=evidence[:count])

    result = parse_match(payload)

    assert (result is not None) is valid


@pytest.mark.parametrize("version", ["", 1])
def test_should_reject_invalid_stored_version(version: object) -> None:
    payload = dict(classification_payload()["match"], version=version)

    assert decode_match(json.dumps(payload)) is None


@pytest.mark.parametrize("value", [None, "not json", "[]", '{"status": "strong"}'])
def test_should_exclude_unusable_stored_match(value: object) -> None:
    assert decode_match(value) is None


def test_should_preserve_stale_version_when_decoding() -> None:
    payload = dict(classification_payload()["match"], version="outdated")

    match = decode_match(json.dumps(payload))

    assert match is not None
    assert match.version == "outdated"
    assert is_reportable(make_ad(match=match)) is False


@pytest.mark.parametrize(
    "overrides",
    [
        {"requires_master": True},
        {"is_programming_job": False},
        {"match": None},
        {"match": make_match(status="unknown")},
        {"match": make_match(status="unsuitable")},
        {"match": make_match(is_low_code=True)},
        {"match": make_match(blockers=("Master required",))},
        {"match": make_match(evidence=())},
        {"match": make_match(evidence=("education",))},
        {"match": make_match(version="outdated")},
    ],
)
def test_should_exclude_unqualified_ad(overrides: dict[str, object]) -> None:
    assert is_reportable(make_ad(**overrides)) is False


@pytest.mark.parametrize("status", ["strong", "stretch"])
def test_should_exclude_senior_despite_candidate_fit(status: str) -> None:
    ad = make_ad(
        label="senior",
        match=make_match(status=status),
    )

    assert is_reportable(ad) is False


@pytest.mark.parametrize(
    ("years", "expected"),
    [(None, True), (0, True), (1, True), (1.5, False), (2, False), (3, False)],
)
def test_should_enforce_required_experience_cap(
    years: float | None, expected: bool
) -> None:
    payload = dict(classification_payload()["match"], required_experience_years=years)

    match = parse_match(payload)

    assert match is not None
    assert match.required_experience_years == years
    assert is_reportable(make_ad(label="junior", match=match)) is expected
    assert match.status == ("strong" if expected else "unsuitable")


@pytest.mark.parametrize(
    "phrase",
    [
        "observability",
        "purchased",
        "business development",
        "unpaid training",
        "above 1 year",
        "master's, or a bachelor's with",
        "significant, extensive, solid",
    ],
)
def test_should_name_required_rules_in_matching_rules(phrase: str) -> None:
    assert phrase in MATCHING_RULES


def test_should_reject_excessive_years_even_in_a_strong_stored_match() -> None:
    ad = make_ad(match=make_match(required_experience_years=3))

    assert is_reportable(ad) is False


def test_should_hide_unknown_label_without_requirements() -> None:
    ad = make_ad(label="unknown", requirements=())

    assert not is_reportable(ad)


def test_should_keep_unknown_label_with_requirements() -> None:
    ad = make_ad(label="unknown", requirements=("Bachelor's degree in IT",))

    assert is_reportable(ad)
