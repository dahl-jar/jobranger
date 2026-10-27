from __future__ import annotations

from typing import get_args

import pytest

from jobranger.catalogue import (
    RoleId,
    catalogue_for,
    load_catalogue,
    role_label,
    role_list,
    source_plan,
)
from jobranger.settings import Limits, Targets


def test_should_include_base_keywords_with_no_roles_selected() -> None:
    plan = catalogue_for(())

    assert "Software Engineer" in plan.linkedin_keywords


def test_should_add_frontend_styrk_2513_when_frontend_selected() -> None:
    plan = catalogue_for(("frontend",))

    assert "2513" in plan.nav_styrk


def test_should_list_every_role_id_in_catalogue() -> None:
    assert set(load_catalogue().roles) == set(get_args(RoleId.__value__))


@pytest.mark.parametrize("title", ["Trainee 2027", "Sommerjobb 2027"])
def test_should_match_entry_level_title(title: str) -> None:
    assert catalogue_for(()).title_matches(title)


def test_should_skip_senior_titles_only_when_senior_level_is_off() -> None:
    with_senior = Targets(limits=Limits(levels=frozenset({"junior", "senior"})))

    plan = source_plan(with_senior)

    assert plan.skip_title_levels == frozenset({"lead"})


@pytest.mark.parametrize(
    ("role", "label"),
    [("general_software", "Software"), ("backend", "Backend"), (None, "Other")],
)
def test_should_label_role(role: str | None, label: str) -> None:
    assert role_label(role) == label


@pytest.mark.parametrize(
    ("title", "matches"),
    [("Rådgiver KI", True), ("Lag it bra", False)],
    ids=["uppercase acronym", "lowercase word"],
)
def test_should_match_acronyms_case_sensitively(title: str, matches: bool) -> None:
    assert catalogue_for(()).title_matches(title) is matches


def test_should_skip_senior_and_lead_titles_by_default() -> None:
    assert catalogue_for(()).skip_title_levels == frozenset({"senior", "lead"})


def test_should_list_roles_with_labels() -> None:
    assert role_list()[0] == ("backend", "Backend")
