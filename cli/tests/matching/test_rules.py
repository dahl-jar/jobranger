from __future__ import annotations

import json
from datetime import date

import pytest
from factories import (
    FAKE_LIMITS,
    FAKE_VERSION,
    classification_payload,
    make_ad,
    make_match,
    targets,
)

from jobranger.matching.rules import (
    BASE_RULES,
    band_of,
    build_rules,
    decode_match,
    display_fit,
    encode_match,
    hidden_reason,
    is_reportable,
    is_stale,
    max_years,
    parse_match,
)
from jobranger.settings import Limits

EVIDENCE_IDS = frozenset(
    {
        "education",
        "work_frontend",
        "work_data",
        "java_project",
        "cloud_project",
        "django_project",
        "android_project",
        "cpp_project",
    }
)
EDUCATION_IDS = frozenset({"education"})
TODAY = date(2026, 10, 9)


def _parse(payload: object, limits: Limits = FAKE_LIMITS) -> object:
    return parse_match(payload, EVIDENCE_IDS, EDUCATION_IDS, limits, FAKE_VERSION)


def _match_payload(**overrides: object) -> dict[str, object]:
    payload = dict(classification_payload()["match"])
    payload.update(overrides)
    return payload


@pytest.mark.parametrize(
    "overrides",
    [
        {"status": "excellent"},
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
        {"required_experience_years": "3"},
        {"required_experience_years": True},
        {"required_experience_years": -1},
        {"required_experience_years": float("inf")},
        {"required_experience_years": float("nan")},
    ],
)
def test_should_reject_invalid_match(overrides: dict[str, object]) -> None:
    assert _parse(_match_payload(**overrides)) is None


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
    payload = _match_payload()
    del payload[field]

    assert _parse(payload) is None


@pytest.mark.parametrize(
    "overrides",
    [{"blockers": ["Mandatory SAP experience not documented"]}, {"is_low_code": True}],
)
def test_should_demote_hard_mismatch(overrides: dict[str, object]) -> None:
    result = _parse(_match_payload(**overrides))

    assert result == make_match(status="unsuitable", **_tuples(overrides))


def test_should_preserve_assessment_when_serializing() -> None:
    match = make_match(status="stretch", gaps=("Qt experience not documented",))

    assert decode_match(encode_match(match)) == match


@pytest.mark.parametrize(("length", "valid"), [(300, True), (301, False)])
@pytest.mark.parametrize("field", ["reason", "gaps"])
def test_should_enforce_match_text_limit(field: str, length: int, valid: bool) -> None:
    payload = _match_payload(status="stretch")
    payload[field] = "x" * length if field == "reason" else ["x" * length]

    assert (_parse(payload) is not None) is valid


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

    assert (_parse(_match_payload(evidence=evidence[:count])) is not None) is valid


@pytest.mark.parametrize("version", ["", 1])
def test_should_reject_invalid_stored_version(version: object) -> None:
    assert decode_match(json.dumps(_match_payload(version=version))) is None


@pytest.mark.parametrize("value", [None, "not json", "[]", '{"status": "strong"}'])
def test_should_exclude_unusable_stored_match(value: object) -> None:
    assert decode_match(value) is None


def test_should_reject_evidence_id_missing_from_profile() -> None:
    assert _parse(_match_payload(evidence=["sap_certification"])) is None


def test_should_force_unsuitable_when_required_years_exceed_limit_max() -> None:
    result = _parse(_match_payload(required_experience_years=1.5))

    assert result == make_match(status="unsuitable", required_experience_years=1.5)


def test_should_reject_stretch_backed_only_by_education_ids() -> None:
    payload = _match_payload(status="stretch", evidence=["uni_degree"])

    result = parse_match(
        payload,
        frozenset({"uni_degree", "java_project"}),
        frozenset({"uni_degree"}),
        FAKE_LIMITS,
        FAKE_VERSION,
    )

    assert result is None


def test_should_report_ad_only_when_role_is_in_selected_subfields() -> None:
    ad = make_ad(role_type="backend")

    assert [
        is_reportable(ad, FAKE_VERSION, targets(subfields=roles))
        for roles in (("backend",), ("frontend",))
    ] == [True, False]


def test_should_report_general_software_ad_with_no_subfields_selected() -> None:
    ad = make_ad(role_type="general_software")

    assert is_reportable(ad, FAKE_VERSION, targets(subfields=()))


def test_should_hide_ad_outside_places_when_relocate_is_off() -> None:
    ad = make_ad(location="Oslo")

    hidden = hidden_reason(
        ad, FAKE_VERSION, targets(places=("Bergen",), relocate=False)
    )

    assert hidden is not None
    assert (hidden.kind, hidden.detail) == ("place", "Outside Bergen")


def test_should_match_multi_word_place() -> None:
    ad = make_ad(location="Mo i Rana kommune")

    assert is_reportable(
        ad, FAKE_VERSION, targets(places=("Mo i Rana",), relocate=False)
    )


def test_should_report_ad_with_empty_location_when_places_set() -> None:
    ad = make_ad(location="")

    assert is_reportable(ad, FAKE_VERSION, targets(places=("Bergen",), relocate=False))


def test_should_hide_ad_requiring_four_years_when_band_three_to_six_is_off() -> None:
    ad = make_ad(match=make_match(required_experience_years=4))
    limits = Limits(years=frozenset({"0-1", "1-3", "6+"}))

    hidden = hidden_reason(ad, FAKE_VERSION, targets(limits=limits))

    assert hidden is not None
    assert hidden.kind == "years"


def test_should_report_ad_with_unstated_years() -> None:
    ad = make_ad(match=make_match(required_experience_years=None))
    limits = Limits(years=frozenset({"0-1"}))

    assert is_reportable(ad, FAKE_VERSION, targets(limits=limits))


def test_should_report_senior_ad_when_senior_level_selected() -> None:
    ad = make_ad(label="senior")
    limits = Limits(levels=frozenset({"senior"}))

    assert is_reportable(ad, FAKE_VERSION, targets(limits=limits))


def test_should_report_master_required_ad_when_hide_masters_is_off() -> None:
    ad = make_ad(requires_master=True)
    limits = Limits(hide_masters=False)

    assert is_reportable(ad, FAKE_VERSION, targets(limits=limits))


def test_should_give_other_kind_before_years_for_non_programming_ad() -> None:
    ad = make_ad(
        is_programming_job=False,
        role_type=None,
        match=make_match(required_experience_years=5),
    )

    hidden = hidden_reason(ad, FAKE_VERSION, targets())

    assert hidden is not None
    assert hidden.kind == "other"


def test_should_give_senior_kind_when_ad_level_is_not_selected() -> None:
    ad = make_ad(label="lead", match=make_match(required_experience_years=8))
    limits = Limits(levels=frozenset({"junior", "mid"}))

    hidden = hidden_reason(ad, FAKE_VERSION, targets(limits=limits))

    assert hidden is not None
    assert (hidden.kind, hidden.detail) == ("senior", "Asks for Tech lead level")


def test_should_render_requires_years_detail() -> None:
    ad = make_ad(match=make_match(required_experience_years=4.0))

    hidden = hidden_reason(ad, FAKE_VERSION, targets())

    assert hidden is not None
    assert hidden.detail == "Requires 4 years"


def test_should_hide_ad_with_stale_version_as_other() -> None:
    ad = make_ad(match=make_match(version="outdated"))

    hidden = hidden_reason(ad, FAKE_VERSION, targets())

    assert hidden is not None
    assert hidden.kind == "other"


@pytest.mark.parametrize(
    ("published", "stale"),
    [
        ("2026-07-10T00:00:00+00:00", True),
        ("2026-07-11", False),
        (None, False),
        ("Snarest", False),
    ],
    ids=["91 days", "90 days", "undated", "malformed"],
)
def test_should_mark_ad_published_over_ninety_days_ago_stale(
    published: str | None, stale: bool
) -> None:
    assert is_stale(make_ad(published=published), TODAY) is stale


@pytest.mark.parametrize(
    ("years", "expected"),
    [(0, "0-1"), (1, "0-1"), (1.5, "1-3"), (4, "3-6"), (6, "3-6"), (7, "6+")],
)
def test_should_place_required_years_in_band(years: float, expected: str) -> None:
    assert band_of(years) == expected


def test_should_render_max_years_from_highest_selected_band() -> None:
    assert max_years(Limits(years=frozenset({"0-1", "3-6"}))) == 6


def test_should_render_no_year_cap_when_six_plus_selected() -> None:
    assert max_years(Limits(years=frozenset({"0-1", "6+"}))) is None


def test_should_name_senior_roles_unsuitable_only_when_senior_and_lead_off() -> None:
    with_senior = build_rules(Limits(levels=frozenset({"junior", "senior"})))
    without = build_rules(Limits(levels=frozenset({"junior", "mid"})))

    assert (
        "Senior and lead roles are unsuitable" in with_senior,
        "Senior and lead roles are unsuitable" in without,
    ) == (False, True)


def test_should_omit_master_rule_when_hide_masters_is_off() -> None:
    rules = build_rules(Limits(hide_masters=False))

    assert "Mandatory completed master's" not in rules


def test_should_not_list_unpaid_training_as_unsuitable_in_rules() -> None:
    assert "unpaid training" not in build_rules(FAKE_LIMITS)


@pytest.mark.parametrize(
    "phrase",
    [
        "observability",
        "purchased",
        "business development",
        "significant, extensive",
    ],
)
def test_should_name_required_rules_in_matching_rules(phrase: str) -> None:
    assert phrase in BASE_RULES


def test_should_render_year_cap_from_limits_in_rules() -> None:
    assert "at most 3 years" in build_rules(Limits(years=frozenset({"1-3"})))


def test_should_hide_blocked_match_as_other_with_blocker_detail() -> None:
    ad = make_ad(match=make_match(status="unsuitable", blockers=("SAP required",)))

    hidden = hidden_reason(ad, FAKE_VERSION, targets())

    assert hidden is not None
    assert (hidden.kind, hidden.detail) == ("other", "SAP required")


def _tuples(overrides: dict[str, object]) -> dict[str, object]:
    return {
        key: tuple(value) if isinstance(value, list) else value
        for key, value in overrides.items()
    }


@pytest.mark.parametrize(
    ("overrides", "fit"),
    [
        ({}, "strong"),
        ({"match": make_match(status="stretch", gaps=("Qt",))}, "partial"),
        ({"match": None}, "partial"),
        ({"location": "Oslo"}, "partial"),
    ],
    ids=["strong in place", "stretch", "no match", "outside places"],
)
def test_should_show_strong_fit_only_for_strong_match_in_place(
    overrides: dict[str, object], fit: str
) -> None:
    ad = make_ad(**{"location": "Bergen", **overrides})

    assert display_fit(ad, targets(places=("Bergen",))) == fit


@pytest.mark.parametrize(
    ("overrides", "hidden"),
    [
        (
            {"is_programming_job": False, "role_type": None},
            ("other", "Other area: Other"),
        ),
        ({"role_type": "backend"}, ("role", "Other area: Backend")),
        ({"requires_master": True}, ("masters", "Master's required")),
        ({"match": None}, ("other", "Not checked against your profile yet")),
        ({"match": make_match(is_low_code=True)}, ("other", make_match().reason)),
        ({"match": make_match(status="unknown")}, ("other", make_match().reason)),
    ],
    ids=["not programming", "role", "masters", "no match", "low code", "unknown"],
)
def test_should_give_hidden_kind_and_detail(
    overrides: dict[str, object], hidden: tuple[str, str]
) -> None:
    result = hidden_reason(
        make_ad(**overrides), FAKE_VERSION, targets(subfields=("frontend",))
    )

    assert result is not None
    assert (result.kind, result.detail) == hidden


def test_should_name_every_place_in_place_detail() -> None:
    result = hidden_reason(
        make_ad(location="Oslo"),
        FAKE_VERSION,
        targets(places=("Bergen", "Stavanger"), relocate=False),
    )

    assert result is not None
    assert result.detail == "Outside Bergen, Stavanger"


@pytest.mark.parametrize("years", [0, 0.5, 1])
def test_should_keep_strong_fit_for_years_within_limit(years: float) -> None:
    assert _parse(_match_payload(required_experience_years=years)) == make_match(
        required_experience_years=years
    )


def test_should_cap_no_years_when_no_band_selected() -> None:
    assert max_years(Limits(years=frozenset())) == 0


def test_should_list_accepted_levels_in_rules_when_senior_selected() -> None:
    rules = build_rules(Limits(levels=frozenset({"junior", "senior"})))

    assert "The search accepts these levels: Junior, Senior." in rules


def test_should_name_role_of_non_programming_ad_in_detail() -> None:
    result = hidden_reason(
        make_ad(is_programming_job=False, role_type="backend"), FAKE_VERSION, targets()
    )

    assert result is not None
    assert result.detail == "Other area: Backend"


def test_should_demote_strong_with_gaps_to_stretch() -> None:
    result = _parse(_match_payload(gaps=["Norwegian needs confirmation"]))

    assert result == make_match(
        status="stretch", gaps=("Norwegian needs confirmation",)
    )
