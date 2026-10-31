from __future__ import annotations

import json
from typing import Any

import pytest
from factories import MESSAGES, make_match

from jobranger.matching.years import StatedYears, apply_years_guard, stated_years
from jobranger.settings import Limits

CASES: list[dict[str, Any]] = json.loads(
    (MESSAGES / "fixtures" / "matching" / "years_cases.json").read_text(
        encoding="utf-8"
    )
)
CASE_IDS = [case["id"] for case in CASES]
JUNIOR_LIMITS = Limits(years=frozenset({"0-1"}))
JAVA_YEARS = "Minimum 3 års erfaring med Java"
GUARD_BLOCKER = "Candidate does not have 3 years of experience"


@pytest.mark.parametrize("case", CASES, ids=CASE_IDS)
def test_should_find_stated_years_for_case(case: dict[str, Any]) -> None:
    expected = (
        None
        if case["stated_years"] is None
        else StatedYears(case["stated_years"], case["phrase"])
    )

    found = stated_years(f"{case['title']}\n{case['description'] or ''}")

    assert found == expected


@pytest.mark.parametrize("case", CASES, ids=CASE_IDS)
def test_should_apply_years_guard_for_case(case: dict[str, Any]) -> None:
    match = make_match(required_experience_years=case["model_years"])
    limits = Limits(years=frozenset(case["bands"]))

    result = apply_years_guard(match, case["title"], case["description"], limits)

    assert (result.status, result.required_experience_years) == (
        case["status"],
        case["years"],
    )


def test_should_name_phrase_in_reason_when_guard_blocks() -> None:
    match = make_match(blockers=("Mandatory Java experience missing",))

    result = apply_years_guard(match, "Utvikler", JAVA_YEARS, JUNIOR_LIMITS)

    assert (result.reason, result.blockers) == (
        'The ad asks for at least 3 years of experience: "Minimum 3 års erfaring". '
        "The search accepts at most 1 year.",
        (GUARD_BLOCKER, "Mandatory Java experience missing"),
    )


def test_should_say_one_year_in_blocker_when_ad_asks_for_one_year() -> None:
    no_bands = Limits(years=frozenset())

    result = apply_years_guard(
        make_match(), "Utvikler", "Minimum 1 års erfaring", no_bands
    )

    assert result.blockers[0] == "Candidate does not have 1 year of experience"


def test_should_keep_five_blockers_when_guard_adds_one() -> None:
    match = make_match(blockers=("b1", "b2", "b3", "b4", "b5"))

    result = apply_years_guard(match, "Utvikler", JAVA_YEARS, JUNIOR_LIMITS)

    assert result.blockers == (GUARD_BLOCKER, "b1", "b2", "b3", "b4")


def test_should_cut_reason_when_phrase_is_long() -> None:
    description = "Minimum 3 års " + "x" * 400 + " erfaring"

    result = apply_years_guard(make_match(), "Utvikler", description, JUNIOR_LIMITS)

    assert result.reason == (
        'The ad asks for at least 3 years of experience: "Minimum 3 års…'
    )
