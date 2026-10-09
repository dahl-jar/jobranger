from __future__ import annotations

import pytest
from factories import make_ad

from jobranger.exclusions import is_excluded


@pytest.mark.usefixtures("excluded_company")
def test_should_exclude_listed_company() -> None:
    ad = make_ad(
        company="Vestland Bootcamp AS",
        title="Embedded Software Developer",
        display_title="Embedded Developer",
    )

    assert is_excluded(ad) is True


@pytest.mark.parametrize(
    ("company", "title", "display_title"),
    [
        ("Nordbank", "Software Engineering Intern", "Software Engineer"),
        ("Nordlys Belysning", "Utvikler", "Software Development Intern"),
        ("Fjordbank", "Summer Internship 2027 - Software", None),
        ("Fjordbank", "Software Intern 2027", None),
        ("Fjordbank", "Intern - Backend", None),
        ("Vestkraft", "Sommerjobb IT 2027", None),
        ("Havgass", "Praktikant IT", None),
        ("Havgass", "Praktikantstilling innen IT", None),
    ],
)
def test_should_exclude_internship_titles(
    company: str, title: str, display_title: str | None
) -> None:
    ad = make_ad(company=company, title=title, display_title=display_title)

    assert is_excluded(ad) is True


@pytest.mark.usefixtures("excluded_company")
@pytest.mark.parametrize(
    ("company", "title"),
    [
        ("Vestland Data AS", "Fullstack Developer"),
        ("Fjordbank", "Utvikler til intern plattform"),
        ("Fjordbank", "Intern revisor"),
    ],
)
def test_should_keep_regular_roles(company: str, title: str) -> None:
    ad = make_ad(company=company, title=title, display_title="Software Developer")

    assert is_excluded(ad) is False
