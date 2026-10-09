from __future__ import annotations

from factories import make_ad

from jobranger.ads.duplicates import (
    VACANCY_JACCARD_THRESHOLD,
    companies_match,
    group_duplicates,
    is_same_vacancy,
    shingle_jaccard,
    split_reported,
)

TEXT = (
    "vi søker en utvikler som vil bygge tjenester for kundene våre i et team med "
    "erfarne kolleger der du får ansvar for backend og integrasjoner mot andre "
    "systemer og jobber tett med produkt og design for å levere løsninger som "
    "brukes av mange hver dag og du lærer mye om skyplattform drift og kvalitet "
    "gjennom kodegjennomgang testing og kontinuerlig leveranse til produksjon"
)
WORDS = TEXT.split()


def _with_words(replacements: dict[int, str]) -> str:
    return " ".join(replacements.get(index, word) for index, word in enumerate(WORDS))


def test_should_match_same_vacancy_by_company_subset_and_title() -> None:
    finn = make_ad(id="finn:1", company="Org One AS", title="Utvikler", location="Oslo")
    nav = make_ad(
        id="nav:1", company="IT, Org One", title="Utvikler", location="Oslo, Oslo"
    )

    assert is_same_vacancy(finn, nav)


def test_should_match_same_vacancy_by_description_over_threshold() -> None:
    finn = make_ad(id="finn:1", title="Graduate developer", description=TEXT)
    linkedin = make_ad(
        id="linkedin:1",
        company="Org One",
        title="Turn code into impact",
        description=_with_words({len(WORDS) - 1: "drift"}),
    )

    assert shingle_jaccard(finn.description, linkedin.description) >= (
        VACANCY_JACCARD_THRESHOLD
    )
    assert is_same_vacancy(finn, linkedin)


def test_should_keep_different_roles_with_similar_text_apart() -> None:
    ios = make_ad(id="finn:1", title="iOS Developer", description=TEXT)
    android = make_ad(
        id="finn:2",
        title="Android Developer",
        description=_with_words({10: "android", 40: "kotlin"}),
    )

    assert shingle_jaccard(ios.description, android.description) < (
        VACANCY_JACCARD_THRESHOLD
    )
    assert not is_same_vacancy(ios, android)


def test_should_keep_roles_apart_just_under_threshold() -> None:
    first = make_ad(id="finn:1", title="iOS Developer", description=TEXT)
    second = make_ad(
        id="finn:2", title="Android Developer", description=_with_words({30: "android"})
    )

    similarity = shingle_jaccard(first.description, second.description)

    assert 0.8 <= similarity < VACANCY_JACCARD_THRESHOLD
    assert not is_same_vacancy(first, second)


def test_should_match_same_vacancy_when_one_location_is_empty() -> None:
    left = make_ad(id="finn:1", title="Utvikler", location="")
    right = make_ad(
        id="nav:1", title="Utvikler", location="Oslo", company="IT, Org One"
    )

    assert is_same_vacancy(left, right)


def test_should_not_match_short_descriptions_by_text() -> None:
    short = "Bygg tjenester i Java og Kotlin for kundene våre."
    left = make_ad(id="finn:1", title="Backend", description=short)
    right = make_ad(id="nav:1", title="Frontend", description=short)

    assert not is_same_vacancy(left, right)


def test_should_not_match_vacancy_at_other_company() -> None:
    left = make_ad(id="finn:1", company="Org One AS", description=TEXT)
    right = make_ad(id="finn:2", company="Org Two AS", description=TEXT)

    assert not is_same_vacancy(left, right)


def test_should_match_company_with_department_prefix() -> None:
    assert companies_match("Virksomhetsteknologi, Org One", "Org One")


def test_should_match_company_ignoring_legal_and_country_words() -> None:
    assert companies_match("Org One Services", "IT, Org One")


def test_should_not_match_agency_with_ad_posted_via_it() -> None:
    assert not companies_match("Agency AS", "Org One via Agency AS")


def test_should_match_company_across_branch_suffixes() -> None:
    assert companies_match("Org One avd Bergen", "Org One avd Oslo")


def test_should_group_twin_with_department_prefix_in_one_report() -> None:
    finn = make_ad(id="finn:1", company="Org One AS", title="Utvikler", location="Oslo")
    nav = make_ad(
        id="nav:1",
        company="IT, Org One",
        title="Utvikler",
        location="Oslo, Oslo",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
    )

    assert len(group_duplicates([finn, nav])) == 1


def test_should_split_out_twins_of_reported_jobs() -> None:
    reported = make_ad(id="finn:1", company="Org One AS", title="Utvikler")
    twin = make_ad(id="nav:9", company="IT, Org One", title="Utvikler")
    fresh = make_ad(id="nav:10", company="Org Two AS", title="Utvikler")

    remaining, covered = split_reported([twin, fresh], [reported])

    assert [ad.id for ad in remaining] == [fresh.id]
    assert covered == [twin.id]
