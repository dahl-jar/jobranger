from __future__ import annotations

import pytest

from jobranger.ads.titles import looks_like_developer, title_is_senior


def test_should_accept_developer_job_title_behind_slogan_heading() -> None:
    assert looks_like_developer("Er du klar for å bytte rede?", "Data Engineer")


def test_should_accept_title_named_only_by_an_acronym() -> None:
    assert looks_like_developer("KI-rådgiver", "")


@pytest.mark.parametrize(
    "title", ["Kokk", "Rapporteringsansvarlig", "Is it you we are looking for?"]
)
def test_should_reject_non_developer_titles(title: str) -> None:
    assert not looks_like_developer(title, "")


def test_should_mark_senior_title() -> None:
    assert title_is_senior("Lead Backend Engineer")


def test_should_not_mark_graduate_title_with_senior_word() -> None:
    assert not title_is_senior("Graduate Engineer, Platform Lead track")
