from __future__ import annotations

import sqlite3
from datetime import UTC, datetime
from pathlib import Path

import pytest
from factories import FIXTURES, make_ad, make_classification, make_match

from jobranger.storage.store import Store


def test_should_upgrade_legacy_database_without_losing_source_data(
    tmp_path: Path,
) -> None:
    path = _legacy_database(tmp_path)
    with sqlite3.connect(path) as connection:
        connection.execute(
            "INSERT INTO state VALUES (?, ?)", ("nav_position", "cursor-one")
        )

    store = Store(path)
    refreshed = store.refresh_assessments()

    assert refreshed is True
    assert store.get_state("nav_position") == "cursor-one"
    assert store.pending_classification()[0].description == "Build Java services."
    assert store.reportable_ads("2026-09-08") == []
    assert store.pending_apply_check("2026-09-08", "2026-09-01", limit=10) == []
    store.set_classification("finn:1", make_classification())
    reopened = Store(path)
    reported = reopened.reportable_ads("2026-09-08")
    assert reported[0].match == make_match()


def test_should_treat_existing_programming_ads_as_reported_on_upgrade(
    tmp_path: Path,
) -> None:
    store = Store(_legacy_database(tmp_path))
    since = datetime.now(tz=UTC).isoformat()

    store.set_classification("finn:1", make_classification())

    assert store.reportable_ads("2026-09-30", since) == []


def test_should_report_unclassified_legacy_ad_after_upgrade(tmp_path: Path) -> None:
    store = Store(_legacy_database(tmp_path, is_programming_job=None))
    since = datetime.now(tz=UTC).isoformat()

    store.set_classification("finn:1", make_classification())

    reported = store.reportable_ads("2026-09-30", since)
    assert [ad.id for ad in reported] == ["finn:1"]


def test_should_round_trip_candidate_match(tmp_path: Path) -> None:
    path = tmp_path / "ads.db"
    store = Store(path)
    match = make_match(status="stretch", gaps=("Qt preferred",))
    store.upsert_ad(make_ad(match=match))

    reopened = Store(path)

    assert reopened.active_ads()[0].match == match


def test_should_refresh_stale_assessments_without_losing_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    stale = make_ad(
        label="junior",
        description="Build Java services.",
        match=make_match(version="old"),
    )
    fresh = make_ad(
        id="finn:2", source_id="2", label="open", description="Build software."
    )
    store.upsert_ad(stale)
    store.upsert_ad(fresh)
    for _ in range(3):
        store.bump_label_attempts([stale.id])
    store.set_state("nav_position", "cursor-one")

    refreshed = store.refresh_assessments()

    pending = store.pending_classification()
    assert refreshed is True
    assert [ad.id for ad in pending] == [stale.id]
    assert pending[0].description == stale.description
    assert pending[0].match is None
    assert store.get_state("nav_position") == "cursor-one"
    assert len(store.active_ads()) == 2


def test_should_refresh_reactivated_stale_assessment(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        label="junior",
        description="Build Java services.",
        match=make_match(version="old"),
    )
    store.upsert_ad(ad)
    store.set_active(ad.id, False)
    assert store.refresh_assessments() is False
    store.set_active(ad.id, True)

    assert store.refresh_assessments() is True
    assert [row.id for row in store.pending_classification()] == [ad.id]


def test_should_not_reset_current_failed_attempts(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description="Build Java services.", match=None)
    store.upsert_ad(ad)
    store.refresh_assessments()
    for _ in range(3):
        store.bump_label_attempts([ad.id])

    assert store.refresh_assessments() is False
    assert store.pending_classification() == []


def test_should_exclude_master_requirement_from_store_report(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(label="junior", requires_master=True))

    assert store.reportable_ads("2026-09-08") == []


def test_should_store_same_ad_once(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad()

    first = store.upsert_ad(ad)
    second = store.upsert_ad(ad)

    assert first is True
    assert second is False
    assert len(store.new_since("2000-01-01T00:00:00+00:00")) == 1


def test_should_report_only_current_active_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    current = make_ad(id="finn:1", source_id="1", deadline="2026-08-24")
    inactive = make_ad(id="finn:2", source_id="2", deadline=None)
    expired = make_ad(id="finn:3", source_id="3", deadline="2026-08-23")
    store.upsert_ad(current)
    store.upsert_ad(inactive)
    store.upsert_ad(expired)
    store.set_active(inactive.id, False)

    ads = store.reportable_ads("2026-08-24")

    assert [ad.id for ad in ads] == [current.id]


def test_should_report_reclassified_ad_once(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(match=None)
    store.upsert_ad(ad)
    since = datetime.now(tz=UTC).isoformat()
    store.set_classification(ad.id, make_classification())

    first = store.reportable_ads("2026-09-30", since)
    store.mark_reported([ad.id], since)
    second = store.reportable_ads("2026-09-30", since)

    assert [found.id for found in first] == [ad.id]
    assert second == []


def test_should_reactivate_current_ads_during_source_sync(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    current = make_ad(id="finn:1", source_id="1", deadline=None)
    removed = make_ad(id="finn:2", source_id="2", deadline=None)
    store.upsert_ad(current)
    store.upsert_ad(removed)
    store.set_active(current.id, False)

    store.sync_source_active("finn", frozenset({current.id}))

    assert [ad.id for ad in store.reportable_ads("2026-08-24")] == [current.id]


WINDOW_START = "2026-09-30T00:00:00+00:00"


def test_should_keep_wide_ad_active_when_published_before_window(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    wide = make_ad(published="2026-09-20T00:00:00+00:00")
    store.upsert_ad(wide)
    store.mark_category_unlisted([wide.id])

    store.sync_finn_active(frozenset(), frozenset(), WINDOW_START)

    assert [ad.id for ad in store.active_ads()] == [wide.id]


def test_should_deactivate_wide_ad_missing_inside_window(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    wide = make_ad(published="2026-10-02T00:00:00+00:00")
    other_source = make_ad(id="nav:1", source="nav", source_id="1")
    store.upsert_ad(wide)
    store.upsert_ad(other_source)
    store.mark_category_unlisted([wide.id])

    store.sync_finn_active(frozenset(), frozenset(), WINDOW_START)

    assert [ad.id for ad in store.active_ads()] == [other_source.id]


def test_should_keep_wide_ad_active_while_recent_scan_lists_it(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    wide = make_ad(published="2026-10-02T00:00:00+00:00")
    store.upsert_ad(wide)
    store.mark_category_unlisted([wide.id])

    store.sync_finn_active(frozenset(), frozenset({wide.id}), WINDOW_START)

    assert [ad.id for ad in store.active_ads()] == [wide.id]


def test_should_deactivate_category_ad_missing_from_category_scan(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad())

    store.sync_finn_active(frozenset(), frozenset(), None)

    assert store.active_ads() == []


def test_should_treat_legacy_finn_rows_as_category_listed(tmp_path: Path) -> None:
    store = Store(_legacy_database(tmp_path))

    store.sync_finn_active(frozenset(), frozenset(), None)

    assert store.active_ads() == []


def test_should_deactivate_recent_ad_once_category_scan_drops_it(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(published="2026-09-20T00:00:00+00:00")
    store.upsert_ad(ad)
    store.mark_category_unlisted([ad.id])
    store.sync_finn_active(frozenset({ad.id}), frozenset(), WINDOW_START)

    store.sync_finn_active(frozenset(), frozenset(), WINDOW_START)

    assert store.active_ads() == []


def test_should_exclude_non_programming_roles(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    developer = make_ad(id="finn:1", source_id="1", deadline=None)
    support = make_ad(
        id="finn:2",
        source_id="2",
        deadline=None,
        is_programming_job=False,
    )
    store.upsert_ad(developer)
    store.upsert_ad(support)

    assert [ad.id for ad in store.reportable_ads("2026-08-24")] == [developer.id]


def test_should_clear_active_results_when_resetting_classifications(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        label="junior",
        description="Software developer role",
        requires_master=True,
    )
    store.upsert_ad(ad)

    store.reset_classifications()

    assert [pending.id for pending in store.pending_classification()] == [ad.id]


def test_should_preserve_structured_classification_fields(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        display_title="C++ Developer",
        role_type="embedded",
        technologies=("C++", "Qt", "Linux"),
        requirements=("0-2 years", "Relevant bachelor's degree"),
    )

    store.upsert_ad(ad)

    stored = store.new_since("2000-01-01T00:00:00+00:00")[0]
    assert stored.display_title == "C++ Developer"
    assert stored.role_type == "embedded"
    assert stored.technologies == ("C++", "Qt", "Linux")
    assert stored.requirements == ("0-2 years", "Relevant bachelor's degree")


def test_should_persist_state_across_store_instances(tmp_path: Path) -> None:
    db_path = tmp_path / "ads.db"
    store = Store(db_path)
    store.set_state("nav_position", "https://example.test/feed/1")
    store.set_state("nav_position", "https://example.test/feed/2")

    reopened = Store(db_path)

    assert reopened.get_state("nav_position") == "https://example.test/feed/2"


def test_should_exclude_exhausted_classifications_from_pending(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    fresh = make_ad(id="finn:1", source_id="1", description="krav: 5 års erfaring")
    exhausted = make_ad(id="finn:2", source_id="2", description="ingen krav")
    store.upsert_ad(fresh)
    store.upsert_ad(exhausted)
    for _ in range(3):
        store.bump_label_attempts([exhausted.id])

    pending = store.pending_classification(max_attempts=3)

    assert [ad.id for ad in pending] == [fresh.id]


def test_should_return_only_active_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    active = make_ad(id="finn:1", source_id="1")
    inactive = make_ad(id="finn:2", source_id="2")
    store.upsert_ad(active)
    store.upsert_ad(inactive)
    store.set_active(inactive.id, False)

    assert [ad.id for ad in store.active_ads()] == [active.id]


def test_should_label_every_duplicate_row(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    first = make_ad(
        id="finn:1",
        source_id="1",
        label=None,
        description="Build services.",
    )
    second = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        label=None,
        description="Build services.",
    )
    store.upsert_ad(first)
    store.upsert_ad(second)
    classification = make_classification(
        label="junior",
        technologies=("Python",),
        requirements=("Bachelor's degree",),
    )

    store.set_classification(first.id, classification)
    store.set_classification(second.id, classification)

    stored = {ad.id: ad for ad in store.active_ads()}
    assert stored[first.id].label == "junior"
    assert stored[second.id].label == "junior"
    assert stored[first.id].technologies == ("Python",)
    assert stored[second.id].requirements == ("Bachelor's degree",)


def test_should_round_trip_apply_fields(tmp_path: Path) -> None:
    path = tmp_path / "ads.db"
    store = Store(path)
    store.upsert_ad(make_ad(apply_url="https://a.test/jobs/abc", republished=True))
    store.set_apply_status("finn:1234", "expired", "2026-09-16T05:00:00+00:00")

    reopened = Store(path)
    stored = reopened.active_ads()[0]

    assert stored.apply_url == "https://a.test/jobs/abc"
    assert stored.apply_status == "expired"
    assert stored.republished is True


def test_should_exclude_expired_apply_status_from_reportable(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(label="junior", apply_url="https://a.test/jobs/abc"))
    store.set_apply_status("finn:1234", "expired", "2026-09-16T05:00:00+00:00")

    assert store.reportable_ads("2026-09-16") == []


def test_should_list_pending_apply_checks_once_per_week(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    unchecked = make_ad(
        id="finn:1", source_id="1", label="junior", apply_url="https://a.test/1"
    )
    checked_recently = make_ad(
        id="finn:2", source_id="2", label="junior", apply_url="https://a.test/2"
    )
    checked_long_ago = make_ad(
        id="finn:3", source_id="3", label="junior", apply_url="https://a.test/3"
    )
    senior = make_ad(
        id="finn:4", source_id="4", label="senior", apply_url="https://a.test/4"
    )
    without_link = make_ad(id="finn:5", source_id="5", label="junior")
    for ad in (unchecked, checked_recently, checked_long_ago, senior, without_link):
        store.upsert_ad(ad)
    store.set_apply_status(checked_recently.id, "active", "2026-09-14T05:00:00+00:00")
    store.set_apply_status(checked_long_ago.id, "active", "2026-09-01T05:00:00+00:00")

    pending = store.pending_apply_check(
        "2026-09-16", "2026-09-09T05:00:00+00:00", limit=10
    )

    assert {ad.id for ad in pending} == {unchecked.id, checked_long_ago.id}
    assert len(store.pending_apply_check("2026-09-16", "2026-09-09", limit=1)) == 1


def test_should_list_expired_active_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    dead_link = make_ad(id="finn:1", source_id="1", label="junior")
    past_deadline = make_ad(
        id="finn:2", source_id="2", label="junior", deadline="2026-09-15T00:00:00"
    )
    live = make_ad(id="finn:3", source_id="3", label="junior")
    for ad in (dead_link, past_deadline, live):
        store.upsert_ad(ad)
    store.set_apply_status(dead_link.id, "expired", "2026-09-16T05:00:00+00:00")

    expired = store.expired_ads("2026-09-16")

    assert {ad.id for ad in expired} == {dead_link.id, past_deadline.id}


def test_should_list_reportable_ads_missing_apply_url(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    missing = make_ad(id="finn:1", source_id="1", label="junior", description="x")
    has_url = make_ad(
        id="finn:2",
        source_id="2",
        label="junior",
        description="x",
        apply_url="https://a.test/2",
    )
    no_text = make_ad(id="finn:3", source_id="3", label="junior", description=None)
    senior = make_ad(id="finn:4", source_id="4", label="senior", description="x")
    nav_ad = make_ad(
        id="nav:5", source="nav", source_id="5", label="junior", description="x"
    )
    for ad in (missing, has_url, no_text, senior, nav_ad):
        store.upsert_ad(ad)

    pending = store.pending_apply_url("finn", limit=10)

    assert [ad.id for ad in pending] == [missing.id]


def _legacy_database(tmp_path: Path, is_programming_job: int | None = 1) -> Path:
    path = tmp_path / "ads.db"
    with sqlite3.connect(path) as connection:
        connection.executescript((FIXTURES / "legacy_schema.sql").read_text())
        connection.execute(
            "INSERT INTO ads (id, source, source_id, title, company, url, "
            "description, label, is_programming_job, dedup_key, first_seen) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (
                "finn:1",
                "finn",
                "1",
                "Developer",
                "Example",
                "https://example.test/1",
                "Build Java services.",
                "junior",
                is_programming_job,
                "example:developer",
                "2026-08-01",
            ),
        )
    return path


def test_should_not_refetch_description_for_senior_label(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description=None, match=None)
    store.upsert_ad(ad)
    store.set_label(ad.id, "senior")

    assert store.pending_description() == []


@pytest.mark.parametrize("status", ["expired", "skipped"])
def test_should_skip_apply_recheck_for_terminal_status(
    tmp_path: Path, status: str
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(apply_url="https://careers.example.test/jobs/1")
    store.upsert_ad(ad)
    store.set_classification(ad.id, make_classification())
    store.set_apply_status(ad.id, status, "2026-01-01T00:00:00+00:00")

    pending = store.pending_apply_check("2026-10-09", "2026-10-02T00:00:00+00:00", 60)

    assert pending == []
