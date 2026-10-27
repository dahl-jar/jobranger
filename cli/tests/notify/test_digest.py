from __future__ import annotations

from dataclasses import replace
from datetime import date
from typing import TYPE_CHECKING

import pytest
from factories import FIXTURES, OUTDATED_VERSION, make_ad, make_match, targets

from jobranger.events import RunMode
from jobranger.notify.digest import DigestData, build_digest
from jobranger.pipeline import RunResult
from jobranger.settings import AppConfig, ProfileRef
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from pathlib import Path

    from jobranger.ads.models import Ad

TODAY = date(2026, 10, 9)
CONFIG = AppConfig(
    profile=ProfileRef(path=FIXTURES / "profile_user.yaml"), targets=targets()
)
VERSION = "run-version"


def _ad(index: int, **overrides: object) -> Ad:
    base: dict[str, object] = {
        "id": f"finn:{index}",
        "source_id": str(index),
        "title": f"Developer {index}",
        "company": f"Org {index} AS",
        "url": f"https://jobs.example.test/ad/{index}",
        "match": make_match(version=VERSION),
    }
    base.update(overrides)
    return make_ad(**base)


def _store(tmp_path: Path, *ads: Ad) -> Store:
    store = Store(tmp_path / "ads.db")
    for ad in ads:
        store.upsert_ad(ad)
    return store


def _result(*ads: Ad, **overrides: object) -> RunResult:
    result = RunResult(
        run_id=None,
        status="completed",
        mode=RunMode.manual,
        reported_ids=tuple(ad.id for ad in ads),
        version=VERSION,
    )
    return replace(result, **overrides)


def _digest(
    tmp_path: Path, ads: list[Ad], reported: list[Ad] | None = None
) -> DigestData:
    store = _store(tmp_path, *ads)
    chosen = ads if reported is None else reported
    return build_digest(store, _result(*chosen), CONFIG, TODAY)


def _ids(ads: list[Ad]) -> list[str]:
    return [ad.id for ad in ads]


@pytest.mark.parametrize(
    "title",
    ["Software Engineering Intern", "Utvikler academy"],
    ids=["internship", "training academy"],
)
def test_should_report_entry_level_programme_ad(tmp_path: Path, title: str) -> None:
    ad = _ad(1, company="Org One AS", title=title)

    data = _digest(tmp_path, [ad])

    assert _ids(data.new_today) == [ad.id]


def test_should_put_ad_due_within_seven_days_in_apply_soon(tmp_path: Path) -> None:
    due = _ad(1, deadline="2026-10-16")
    later = _ad(2, deadline="2026-10-17")

    data = _digest(tmp_path, [due, later])

    assert (_ids(data.apply_soon), _ids(data.new_today)) == ([due.id], [later.id])


def test_should_take_apply_soon_from_this_runs_new_matches_only(
    tmp_path: Path,
) -> None:
    earlier = _ad(1, deadline="2026-10-10")
    fresh = _ad(2, deadline="2026-10-12")

    data = _digest(tmp_path, [earlier, fresh], reported=[fresh])

    assert _ids(data.apply_soon) == [fresh.id]


def test_should_keep_raw_title_in_digest(tmp_path: Path) -> None:
    raw = _ad(1, display_title="<b>Dev</b> *x*")

    data = _digest(tmp_path, [raw])

    assert data.new_today[0].display_title == "<b>Dev</b> *x*"


def test_should_count_same_ad_from_two_sources_once(tmp_path: Path) -> None:
    finn_ad = _ad(1, title="Junior utvikler", company="Org One AS")
    nav_ad = _ad(
        2,
        id="nav:aa-bb",
        source="nav",
        source_id="aa-bb",
        title="Junior Utvikler",
        company="org one",
        url="https://jobs.example.test/stilling/aa-bb",
    )

    data = _digest(tmp_path, [finn_ad, nav_ad])

    assert len(data.new_today) == 1


def test_should_prefer_direct_employer_url_for_duplicate(tmp_path: Path) -> None:
    nav_ad = _ad(
        1,
        id="nav:1",
        source="nav",
        title="Graduate Developer",
        company="Org One AS",
        url="https://jobs.example.test/stilling/1",
    )
    direct = _ad(
        2,
        id="teamtailor:example:1",
        source="teamtailor:example",
        title="Graduate Developer",
        company="Org One AS",
        url="https://careers.example.test/jobs/1",
    )

    data = _digest(tmp_path, [nav_ad, direct])

    assert [ad.url for ad in data.new_today] == [direct.url]


def test_should_put_strong_matches_before_stretches(tmp_path: Path) -> None:
    stretch = _ad(
        1,
        match=make_match(version=VERSION, status="stretch", gaps=("Qt preferred",)),
    )
    strong = _ad(2)

    data = _digest(tmp_path, [stretch, strong])

    assert _ids(data.new_today) == [strong.id, stretch.id]


@pytest.mark.parametrize(
    ("overrides", "kept"),
    [
        ({"deadline": "2026-10-09T21:59:59+00:00"}, True),
        ({"deadline": "2026-10-08"}, False),
        ({"apply_status": "expired"}, False),
    ],
    ids=["due today", "past deadline", "expired link"],
)
def test_should_drop_expired_listings(
    tmp_path: Path, overrides: dict[str, object], kept: bool
) -> None:
    ad = _ad(1, **overrides)

    data = _digest(tmp_path, [ad])

    assert bool(data.apply_soon or data.new_today) is kept


def test_should_leave_other_out_of_hidden_counts(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    result = RunResult(
        run_id=None,
        status="completed",
        mode=RunMode.manual,
        hidden_counts={"years": 2, "other": 5},
    )

    data = build_digest(store, result, CONFIG, TODAY)

    assert data.hidden == {"years": 2}


def test_should_carry_run_facts_into_digest(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.start_run("2026-10-08T03:00:00+00:00")
    store.upsert_ad(_ad(1, deadline="2026-10-08"))
    store.close_past_deadline("2026-10-09")
    current = store.start_run("2026-10-09T03:00:00+00:00")
    result = RunResult(
        run_id=current,
        status="completed",
        mode=RunMode.manual,
        ads_read=40,
        new_ads=6,
        failed_sources=("linkedin",),
        sources_read=("nav", "teamtailor:org-two", "ashby:org-one"),
        budget_paused=True,
        pending=3,
    )

    data = build_digest(store, result, CONFIG, TODAY)

    assert (
        data.day,
        data.ads_read,
        data.new_ads,
        data.sources_read,
        data.employer_pages,
        data.failed_sources,
        data.closed_since_last,
        data.budget_paused,
        data.pending,
        data.max_years,
        data.ran_at.tzinfo is not None,
    ) == (
        TODAY,
        40,
        6,
        ["nav", "teamtailor:org-two", "ashby:org-one"],
        2,
        ["linkedin"],
        1,
        True,
        3,
        1,
        True,
    )


def test_should_report_no_closed_ads_without_previous_run(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(_ad(1, deadline="2026-10-08"))
    store.close_past_deadline("2026-10-09")
    run_id = store.start_run("2026-10-09T03:00:00+00:00")
    result = RunResult(run_id=run_id, status="completed", mode=RunMode.manual)

    assert build_digest(store, result, CONFIG, TODAY).closed_since_last == 0


def test_should_sort_apply_soon_by_deadline(tmp_path: Path) -> None:
    later = _ad(1, deadline="2026-10-14")
    sooner = _ad(2, deadline="2026-10-11")

    data = _digest(tmp_path, [later, sooner])

    assert _ids(data.apply_soon) == [sooner.id, later.id]


def test_should_order_new_matches_by_deadline_with_undated_last(
    tmp_path: Path,
) -> None:
    undated = _ad(1)
    far = _ad(2, deadline="2026-12-20")
    near = _ad(3, deadline="2026-11-20")

    data = _digest(tmp_path, [undated, far, near])

    assert _ids(data.new_today) == [near.id, far.id, undated.id]


def test_should_report_duplicate_by_its_current_match(tmp_path: Path) -> None:
    current = _ad(1, title="Backend Developer", company="Org One AS")
    stale = _ad(
        2,
        id="nav:1",
        source="nav",
        title="Backend Developer",
        company="Org One AS",
        match=make_match(version="old-version", status="unsuitable"),
    )

    data = _digest(tmp_path, [current, stale])

    assert _ids(data.new_today) == [current.id]


def test_should_keep_shown_anyway_ad_that_rules_would_hide(tmp_path: Path) -> None:
    ad = _ad(1, is_programming_job=False)
    store = _store(tmp_path, ad)
    store.mark_ad(ad.id, "show_anyway", "2026-10-09T00:00:00+00:00")

    data = build_digest(store, _result(ad), CONFIG, TODAY)

    assert _ids(data.new_today) == [ad.id]


def test_should_list_ads_from_earlier_runs_apart_from_new_ones(tmp_path: Path) -> None:
    earlier, fresh = _ad(1), _ad(2)
    store = _store(tmp_path, earlier, fresh)
    result = _result(earlier, fresh, carried_ids=(earlier.id,))

    data = build_digest(store, result, CONFIG, TODAY)

    assert (_ids(data.new_today), _ids(data.carried)) == ([fresh.id], [earlier.id])


def test_should_sort_earlier_ads_by_deadline_with_undated_last(tmp_path: Path) -> None:
    undated = _ad(1)
    later = _ad(2, deadline="2026-10-30")
    sooner = _ad(3, deadline="2026-10-20")
    store = _store(tmp_path, undated, later, sooner)
    result = _result(
        undated, later, sooner, carried_ids=(undated.id, later.id, sooner.id)
    )

    data = build_digest(store, result, CONFIG, TODAY)

    assert _ids(data.carried) == [sooner.id, later.id, undated.id]


def test_should_take_match_from_current_twin_when_other_is_shown_anyway(
    tmp_path: Path,
) -> None:
    current = _ad(1, company="Org One AS", title="Developer")
    outdated = _ad(
        2,
        company="Org One AS",
        title="Developer",
        technologies=("Java", "Kotlin"),
        match=make_match(version=OUTDATED_VERSION),
    )
    store = _store(tmp_path, current, outdated)
    store.mark_ad(outdated.id, "show_anyway", "2026-10-09T00:00:00+00:00")

    data = build_digest(store, _result(current, outdated), CONFIG, TODAY)

    [shown] = data.new_today
    assert shown.match is not None
    assert shown.match.version == VERSION
