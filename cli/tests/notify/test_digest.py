from __future__ import annotations

import httpx
import pytest
from factories import make_ad, make_match

from jobranger.matching.classify import Usage
from jobranger.notify import digest
from jobranger.notify.digest import (
    ReportContext,
    build_summary,
    send_discord,
    split_message,
)


def test_should_exclude_mandatory_masters() -> None:
    ad = make_ad(label="junior", requires_master=True)

    summary = build_summary("2026-09-08", [ad])

    assert ad.url not in summary


def test_should_list_evidence_backed_matches() -> None:
    junior = make_ad(
        id="finn:1",
        source_id="1",
        title="Junior utvikler",
        label="junior",
        summary="Frontend developer, React and TypeScript, no experience required",
        role_type="frontend",
        technologies=("React", "TypeScript"),
        requirements=("No experience required",),
    )
    open_ad = make_ad(
        id="finn:2",
        source_id="2",
        title="Utvikler",
        company="Org Two AS",
        url="https://www.finn.no/job/ad/2",
        label="open",
        match=make_match(
            status="stretch", gaps=("Preferred Qt experience not documented",)
        ),
    )
    senior = make_ad(
        id="finn:3",
        source_id="3",
        title="Tech lead",
        company="Org Three AS",
        url="https://www.finn.no/job/ad/3",
        label="senior",
        match=make_match(status="unsuitable"),
    )

    summary = build_summary("2026-08-24", [junior, open_ad, senior])

    assert junior.url in summary
    assert junior.summary is not None
    assert junior.summary not in summary
    assert "`FRONTEND` · React · TypeScript" in summary
    assert "**Requirements:** No experience required" in summary
    assert "**Company:** Org One AS · **Location:** Bergen" in summary
    assert open_ad.url in summary
    assert senior.url not in summary
    assert "2 matching jobs" in summary
    assert "1 strong match" in summary


def test_should_distinguish_scanned_ads_from_qualifying_jobs() -> None:
    summary = build_summary("2026-08-25", [], new_ads_count=5)

    assert "**5 new ads scanned** · **0 matching jobs**" in summary


def test_should_keep_each_discord_message_within_limit() -> None:
    long_line = "x" * 1200
    content = "\n".join([long_line, long_line, "short"])

    chunks = split_message(content)

    assert len(chunks) == 2
    assert all(len(chunk) <= 2000 for chunk in chunks)
    assert "\n".join(chunks).replace("\n", "") == content.replace("\n", "")


def test_should_count_same_ad_from_two_sources_once() -> None:
    finn_ad = make_ad(
        id="finn:1", source_id="1", title="Junior utvikler", label="junior"
    )
    nav_ad = make_ad(
        id="nav:aa-bb",
        source="nav",
        source_id="aa-bb",
        title="Junior Utvikler",
        company="org one",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/aa-bb",
        label="junior",
    )

    summary = build_summary("2026-08-24", [finn_ad, nav_ad])

    assert "1 matching job" in summary
    assert "1 strong match" in summary


def test_should_label_full_listing() -> None:
    junior = make_ad(
        id="finn:1",
        source_id="1",
        title="Juniorutvikler",
        url="https://example.test/junior",
        label="junior",
        summary="A long classification summary that should not be included.",
        display_title="Frontend Developer",
        role_type="frontend",
        technologies=("React", "TypeScript"),
        requirements=("New graduate", "Norwegian"),
    )
    open_ad = make_ad(
        id="finn:2",
        source_id="2",
        title="Systemutvikler",
        url="https://example.test/open",
        label="open",
        match=make_match(
            status="stretch", gaps=("Preferred Qt experience not documented",)
        ),
    )
    senior = make_ad(
        id="finn:3",
        source_id="3",
        title="Seniorutvikler",
        url="https://example.test/senior",
        label="senior",
        match=make_match(status="unsuitable"),
    )
    summary = build_summary("2026-08-24", [junior, open_ad, senior], full_listing=True)

    assert "**jobranger 2026-08-24 - full listing**" in summary
    assert "**2 matching jobs** · 1 strong match · 1 plausible stretch" in summary
    assert "Junior / Graduate · 1-1 of 1" in summary
    assert "Open to junior · 1-1 of 1" in summary
    assert f"[Frontend Developer]({junior.url})" in summary
    assert "`FRONTEND` · React · TypeScript" in summary
    assert "**Requirements:** New graduate · Norwegian" in summary
    assert "**Company:** Org One AS · **Location:** Bergen" in summary
    assert "\n\n" in summary
    assert junior.summary not in summary
    assert senior.url not in summary
    assert "new IT" not in summary


def test_should_group_five_jobs_per_message_block() -> None:
    ads = [
        make_ad(
            id=f"finn:{index}",
            source_id=str(index),
            title=f"Developer {index}",
            company=f"Company {index}",
            url=f"https://example.test/{index}",
            label="junior",
        )
        for index in range(1, 7)
    ]

    summary = build_summary("2026-08-24", ads, full_listing=True)

    assert "Junior / Graduate · 1-5 of 6" in summary
    assert "Junior / Graduate · 6-6 of 6" in summary
    assert "\n\n---\n\n" in summary
    assert summary.count("**jobranger") == 1
    for ad in ads:
        assert summary.count(ad.url) == 1


def test_should_merge_same_description_with_different_titles() -> None:
    description = "Build internal services with Python and PostgreSQL. " * 10
    finn_ad = make_ad(
        id="finn:1",
        source_id="1",
        title="Backend developer",
        description=description,
        label="junior",
    )
    nav_ad = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        title="Vil du bygge våre interne tjenester?",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
        description=description,
        label="junior",
        technologies=("Python", "PostgreSQL"),
    )

    summary = build_summary("2026-08-24", [finn_ad, nav_ad], full_listing=True)

    assert "**1 matching job**" in summary
    assert "1 duplicate source listing merged" in summary
    assert summary.count("**1. [") == 1


def test_should_keep_distinct_roles_at_same_company() -> None:
    frontend = make_ad(
        id="finn:1",
        source_id="1",
        title="Frontend developer",
        url="https://example.test/frontend",
        description="Build React user interfaces.",
        label="junior",
    )
    backend = make_ad(
        id="finn:2",
        source_id="2",
        title="Backend developer",
        url="https://example.test/backend",
        description="Build Java services.",
        label="junior",
    )

    summary = build_summary("2026-08-24", [frontend, backend], full_listing=True)

    assert "**2 matching jobs**" in summary
    assert "duplicate source" not in summary


def test_should_prefer_direct_employer_url_for_duplicate() -> None:
    nav_ad = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        title="Graduate Developer",
        label="junior",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
        technologies=("Python", "PostgreSQL"),
    )
    direct_ad = make_ad(
        id="teamtailor:example:1",
        source="teamtailor:example",
        source_id="1",
        title="Graduate Developer",
        label="junior",
        url="https://careers.example.test/jobs/1",
    )

    summary = build_summary("2026-08-25", [nav_ad, direct_ad], full_listing=True)

    assert direct_ad.url in summary
    assert nav_ad.url not in summary
    assert "1 duplicate source listing merged" in summary


def test_should_merge_duplicate_with_employer_name_variant() -> None:
    nav_ad = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        title="Graduate Developer",
        company="Org Two AS",
        label="junior",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
    )
    direct_ad = make_ad(
        id="teamtailor:org-two-norway:1",
        source="teamtailor:org-two-norway",
        source_id="1",
        title="Graduate Developer",
        company="Org Two Norway",
        label="junior",
        url="https://careers.example.test/jobs/1",
    )

    summary = build_summary("2026-08-25", [nav_ad, direct_ad], full_listing=True)

    assert "1 matching job" in summary
    assert direct_ad.url in summary


def test_should_keep_employer_name_variants_at_different_locations() -> None:
    bergen = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        title="Software Developer",
        company="Org Two AS",
        location="Bergen",
        label="junior",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
    )
    oslo = make_ad(
        id="teamtailor:org-two-norway:1",
        source="teamtailor:org-two-norway",
        source_id="1",
        title="Software Developer",
        company="Org Two Norway",
        location="Oslo",
        label="junior",
        url="https://careers.example.test/jobs/1",
    )

    summary = build_summary("2026-08-25", [bergen, oslo], full_listing=True)

    assert "2 matching jobs" in summary


def test_should_suppress_link_embeds_in_discord(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    payloads: list[dict[str, object]] = []

    def post(url: str, *, json: dict[str, object]) -> httpx.Response:
        payloads.append(json)
        return httpx.Response(204, request=httpx.Request("POST", url))

    monkeypatch.setattr(digest.httpx, "post", post)
    monkeypatch.setattr(digest.time, "sleep", lambda _: None)

    send_discord("https://discord.example/webhook", "[Job](https://example.test)")

    assert payloads == [
        {
            "content": "[Job](https://example.test)",
            "flags": 4,
            "allowed_mentions": {"parse": []},
        }
    ]


def test_should_show_fit_and_gaps_with_strong_matches_first() -> None:
    stretch = make_ad(
        id="finn:1",
        title="Consultant",
        url="https://example.test/stretch",
        match=make_match(
            status="stretch", gaps=("Preferred Qt experience not documented",)
        ),
    )
    strong = make_ad(
        id="finn:2", title="Backend Developer", url="https://example.test/strong"
    )

    summary = build_summary("2026-09-08", [stretch, strong])

    assert summary.index(strong.url) < summary.index(stretch.url)
    assert "Experience not specified · 1-2 of 2" in summary
    assert "Java project" in summary
    assert "**Fit:** Strong match" in summary
    assert "**Fit:** Plausible stretch" in summary
    strong_card, stretch_card = summary.split("**2. [")
    assert "**Fit:** Strong match" in strong_card
    assert "**Fit:** Plausible stretch" in stretch_card
    assert "**Evidence:**" not in summary
    assert "**Check:** Preferred Qt experience not documented" in summary


def test_should_preserve_duplicate_hard_mismatch() -> None:
    safe = make_ad(label="junior")
    blocked = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        label="junior",
        url="https://example.test/duplicate",
        match=make_match(status="unsuitable", blockers=("SAP required",)),
    )

    summary = build_summary("2026-09-08", [safe, blocked])

    assert safe.url not in summary
    assert blocked.url not in summary


def test_should_group_by_entry_level_before_candidate_fit() -> None:
    junior = make_ad(
        label="junior",
        title="Software Engineer",
        url="https://example.test/junior",
        requirements=("Graduate in 2027", "Currently in third year of studies"),
        match=make_match(status="stretch", gaps=("Student status needs checking",)),
    )
    open_ad = make_ad(
        label="open",
        title="Utvikler",
        url="https://example.test/open",
        requirements=("Three years preferred, not required",),
    )

    summary = build_summary("2026-09-10", [open_ad, junior])

    assert summary.index("Junior / Graduate") < summary.index(junior.url)
    assert summary.index(junior.url) < summary.index("Open to junior")
    assert summary.index("Open to junior") < summary.index(open_ad.url)
    assert "Graduate in 2027 · Currently in third year of studies" in summary
    assert "Three years preferred, not required" in summary
    assert "**Fit:** Plausible stretch" in summary
    assert summary.count(open_ad.url) == 1
    assert summary.count(junior.url) == 1


@pytest.mark.parametrize("full_listing", [False, True])
def test_should_exclude_senior_duplicate_from_report(full_listing: bool) -> None:
    junior = make_ad(label="junior")
    senior = make_ad(
        id="nav:1", source="nav", label="senior", url="https://example.test/senior"
    )

    summary = build_summary("2026-09-10", [junior, senior], full_listing=full_listing)

    assert "0 matching jobs" in summary
    assert junior.url not in summary
    assert senior.url not in summary


@pytest.mark.parametrize(("years", "word"), [(0, "years"), (1, "year")])
def test_should_show_required_years_separately_from_fit(years: int, word: str) -> None:
    ad = make_ad(label="junior", match=make_match(required_experience_years=years))

    summary = build_summary("2026-09-10", [ad])

    assert f"**Experience:** {years} {word} required\n**Requirements:**" in summary


def test_should_render_missing_ad_details_without_inventing_requirements() -> None:
    ad = make_ad(
        title="Utvikler",
        display_title=None,
        role_type=None,
        company="",
        location="",
    )

    summary = build_summary("2026-09-10", [ad])

    assert summary.startswith(
        "**jobranger 2026-09-10**\n"
        "**1 matching job** · 1 strong match · 0 plausible stretches\n\n"
        "**Experience not specified"
    )
    assert (
        "**Experience not specified · 1-1 of 1**\n\n"
        "**1. [Utvikler](https://www.finn.no/job/ad/1234)**\n"
        "`SOFTWARE` · Stack not stated\n"
        "**Requirements:** No explicit requirements stated\n"
        "**Company:** Not stated · **Location:** Not stated\n"
        "**Fit:** Strong match · Java project demonstrates Java and Spring Boot "
        "service development."
    ) in summary
    assert summary.endswith("service development.")
    assert "**Experience:**" not in summary


def test_should_keep_gaps_without_displaying_evidence() -> None:
    ad = make_ad(
        match=make_match(
            status="stretch",
            evidence=("java_project", "cloud_project"),
            gaps=("Norwegian needs confirmation", "Three years preferred"),
        )
    )

    summary = build_summary("2026-09-10", [ad])

    assert "**Evidence:**" not in summary
    assert "Cloud project" not in summary
    assert "**Check:** Norwegian needs confirmation · Three years preferred" in summary


def test_should_list_failed_sources_in_header() -> None:
    summary = build_summary(
        "2026-09-16",
        [],
        new_ads_count=0,
        context=ReportContext(failed_sources=("finn", "nav")),
    )

    assert "**0 new ads scanned** · **0 matching jobs**" in summary
    assert "**Sources failed:** finn, nav" in summary


def test_should_hide_expired_listings_and_count_them() -> None:
    expired = make_ad(
        label="junior",
        apply_status="expired",
        apply_checked_at="2026-09-16T05:10:00+00:00",
    )
    live = make_ad(
        id="finn:2",
        source_id="2",
        title="Frontend Developer",
        company="Org Two AS",
        url="https://www.finn.no/job/ad/2",
        label="junior",
    )

    summary = build_summary(
        "2026-09-16",
        [live],
        context=ReportContext(
            expired_ads=(expired,), expired_since="2026-09-16T05:00:00+00:00"
        ),
    )

    assert live.url in summary
    assert expired.url not in summary
    assert "*1 expired listing hidden*" in summary
    assert "Org One AS · Utvikler" in summary
    assert "duplicate source" not in summary


def test_should_use_earliest_deadline_across_sources() -> None:
    finn_ad = make_ad(label="junior", deadline=None)
    nav_twin = make_ad(
        id="nav:aa-bb",
        source="nav",
        source_id="aa-bb",
        company="Org One",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/aa-bb",
        label="junior",
        deadline="2026-09-15T02:45:17.312050119+02:00",
    )

    summary = build_summary(
        "2026-09-16", [finn_ad], context=ReportContext(expired_ads=(nav_twin,))
    )

    assert finn_ad.url not in summary
    assert "*1 expired listing hidden*" in summary


def test_should_keep_listing_whose_deadline_is_today() -> None:
    due_today = make_ad(label="junior", deadline="2026-09-16T21:59:59.999000+00:00")

    summary = build_summary("2026-09-16", [due_today])

    assert due_today.url in summary
    assert "expired listing" not in summary


def test_should_not_list_ads_that_expired_before_the_run() -> None:
    expired = make_ad(
        label="junior",
        apply_status="expired",
        apply_checked_at="2026-09-10T05:00:00+00:00",
    )

    summary = build_summary(
        "2026-09-16",
        [],
        context=ReportContext(
            expired_ads=(expired,), expired_since="2026-09-16T05:00:00+00:00"
        ),
    )

    assert "expired listing" not in summary
    assert "0 matching jobs" in summary


def test_should_note_republished_listing_with_first_published_date() -> None:
    finn_ad = make_ad(
        label="junior", republished=True, published="2026-09-16T00:51:09+00:00"
    )
    nav_twin = make_ad(
        id="nav:aa-bb",
        source="nav",
        source_id="aa-bb",
        company="Org One",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/aa-bb",
        label="junior",
        published="2026-07-04T00:02:17.796+02:00",
        deadline="2027-03-15T00:00:00+01:00",
    )

    summary = build_summary("2026-09-16", [finn_ad, nav_twin])

    assert "**Note:** Republished listing, first published 2026-07-04" in summary


def test_should_note_old_listing_without_republished_flag() -> None:
    old = make_ad(label="junior", published="2026-06-18T00:03:45.171+02:00")

    summary = build_summary("2026-09-16", [old])

    assert "**Note:** Republished listing, first published 2026-06-18" in summary


def test_should_hide_listing_first_published_over_ninety_days_ago() -> None:
    evergreen = make_ad(label="junior", published="2023-11-06T16:36:13+00:00")

    summary = build_summary("2026-09-29", [evergreen])

    assert "0 matching jobs" in summary


def test_should_keep_listing_without_published_date() -> None:
    undated = make_ad(label="junior", published=None)

    summary = build_summary("2026-09-29", [undated])

    assert "**1 matching job**" in summary


@pytest.mark.usefixtures("excluded_company")
def test_should_hide_excluded_employer() -> None:
    excluded = make_ad(label="junior", company="Vestland Bootcamp AS")

    summary = build_summary("2026-08-30", [excluded])

    assert "Vestland Bootcamp" not in summary


@pytest.mark.usefixtures("excluded_company")
@pytest.mark.parametrize(
    ("company", "published"),
    [
        ("Vestland Bootcamp AS", "2026-09-10T00:00:00+00:00"),
        ("Org One AS", "2023-11-06T16:36:13+00:00"),
    ],
)
def test_should_not_count_unwanted_listing_as_expired(
    company: str, published: str
) -> None:
    expired = make_ad(
        label="junior",
        company=company,
        published=published,
        apply_status="expired",
        apply_checked_at="2026-09-16T05:10:00+00:00",
    )

    summary = build_summary(
        "2026-09-16",
        [],
        context=ReportContext(
            expired_ads=(expired,), expired_since="2026-09-16T05:00:00+00:00"
        ),
    )

    assert "expired listing" not in summary


def test_should_not_note_recent_listing() -> None:
    recent = make_ad(label="junior", published="2026-09-10T00:00:00+00:00")

    summary = build_summary("2026-09-16", [recent])

    assert "Republished" not in summary


def test_should_show_model_usage_in_report_header() -> None:
    usage = Usage(
        calls=23,
        input_tokens=500,
        cache_write_tokens=300_000,
        cache_read_tokens=1_000_000,
        output_tokens=52_000,
        cost_usd=6.1,
    )

    summary = build_summary(
        "2026-10-08", [], new_ads_count=60, context=ReportContext(usage=usage)
    )

    assert (
        "*Model: 23 calls · 1.4M tokens (1.3M in, 52k out) · $6.10 at API prices*"
        in summary
    )


@pytest.mark.parametrize(
    ("tokens", "text"),
    [
        (812, "812"),
        (1_000, "1k"),
        (52_000, "52k"),
        (999_600, "1.0M"),
        (1_352_500, "1.4M"),
    ],
)
def test_should_abbreviate_token_counts(tokens: int, text: str) -> None:
    usage = Usage(calls=1, output_tokens=tokens)

    summary = build_summary("2026-10-08", [], context=ReportContext(usage=usage))

    assert f"Model: 1 call · {text} tokens (0 in, {text} out)" in summary


def test_should_omit_usage_line_without_model_calls() -> None:
    summary = build_summary("2026-10-08", [], context=ReportContext(usage=Usage()))

    assert "Model:" not in summary
