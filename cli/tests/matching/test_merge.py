from __future__ import annotations

import pytest
from factories import FAKE_VERSION, make_ad, make_match

from jobranger.matching.merge import preferred_ad

STALE = make_match(version="old")


def test_should_merge_group_fields_from_best_sources() -> None:
    classified = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        description="Short.",
        summary="Backend work.",
        display_title="Backend Developer",
        role_type="backend",
        technologies=("Java",),
        requirements=("Bachelor",),
        requires_master=True,
        deadline="2026-11-02",
        published="2026-10-02",
        apply_url=None,
        label="junior",
    )
    direct = make_ad(
        id="ashby:org:1",
        source="ashby:org",
        source_id="1",
        description="A much longer description of the same role.",
        summary=None,
        display_title=None,
        technologies=(),
        requirements=(),
        match=STALE,
        deadline="2026-11-01",
        published="2026-10-03",
        apply_url="https://jobs.example.test/apply",
        apply_status="expired",
        republished=True,
        label="junior",
    )

    merged = preferred_ad([classified, direct], FAKE_VERSION)

    assert (
        merged.id,
        merged.description,
        merged.summary,
        merged.display_title,
        merged.role_type,
        merged.technologies,
        merged.requirements,
        merged.requires_master,
        merged.is_programming_job,
        merged.match,
        merged.label,
        merged.deadline,
        merged.published,
        merged.republished,
        merged.apply_status,
        merged.apply_url,
    ) == (
        "ashby:org:1",
        "A much longer description of the same role.",
        "Backend work.",
        "Backend Developer",
        "backend",
        ("Java",),
        ("Bachelor",),
        True,
        True,
        make_match(),
        "junior",
        "2026-11-01",
        "2026-10-02",
        True,
        "expired",
        "https://jobs.example.test/apply",
    )


def test_should_take_first_apply_url_when_selected_ad_has_none() -> None:
    selected = make_ad(id="ashby:org:1", source="ashby:org", apply_url=None)
    other = make_ad(id="finn:1", apply_url="https://jobs.example.test/apply")

    assert preferred_ad([selected, other], FAKE_VERSION).apply_url == other.apply_url


def test_should_rank_by_technology_and_requirement_count() -> None:
    fewer = make_ad(id="finn:1", technologies=("Java",), requirements=())
    more = make_ad(
        id="finn:2",
        technologies=("Java", "Kotlin"),
        requirements=("Bachelor", "Norwegian"),
        summary="More detail.",
    )

    assert preferred_ad([fewer, more], FAKE_VERSION).summary == "More detail."


def test_should_rank_classification_with_display_title_higher() -> None:
    untitled = make_ad(id="finn:1", display_title=None, summary="Untitled.")
    titled = make_ad(id="finn:2", display_title="Developer", summary="Titled.")

    assert preferred_ad([untitled, titled], FAKE_VERSION).summary == "Titled."


def test_should_take_fields_from_other_members_than_selected() -> None:
    selected = make_ad(
        id="ashby:org:1",
        source="ashby:org",
        description="Short.",
        label=None,
        is_programming_job=None,
        match=STALE,
        deadline="2026-11-05",
        republished=False,
        apply_status=None,
    )
    other = make_ad(
        id="finn:1",
        description="A much longer description.",
        label="lead",
        is_programming_job=True,
        deadline="2026-11-01",
        republished=True,
        apply_status="expired",
    )

    merged = preferred_ad([selected, other], FAKE_VERSION)

    assert (
        merged.id,
        merged.description,
        merged.label,
        merged.is_programming_job,
        merged.deadline,
        merged.republished,
        merged.apply_status,
    ) == (
        "ashby:org:1",
        "A much longer description.",
        "lead",
        True,
        "2026-11-01",
        True,
        "expired",
    )


def test_should_prefer_current_match_over_richer_stale_one() -> None:
    stale = make_ad(id="finn:1", match=STALE, technologies=("Java", "Kotlin", "Go"))
    current = make_ad(id="finn:2", technologies=())

    assert preferred_ad([stale, current], FAKE_VERSION).match == make_match()


@pytest.mark.parametrize(
    "blocked",
    [make_match(status="unsuitable"), make_match(is_low_code=True)],
    ids=["unsuitable", "low code"],
)
def test_should_prefer_any_blocked_match(blocked: object) -> None:
    safe = make_ad(id="finn:1", technologies=("Java", "Kotlin"))
    other = make_ad(id="nav:1", source="nav", match=blocked)

    assert preferred_ad([safe, other], FAKE_VERSION).match == blocked
