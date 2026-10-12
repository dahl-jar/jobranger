from __future__ import annotations

import json
import sqlite3
from dataclasses import replace
from datetime import UTC, datetime
from pathlib import Path

import pytest
from factories import (
    FAKE_VERSION,
    OUTDATED_VERSION,
    make_ad,
    make_classification,
    make_match,
    targets,
)

from jobranger.matching.assessment import Check
from jobranger.providers.base import Usage
from jobranger.storage.store import (
    Application,
    ApplicationChange,
    ApplicationEvent,
    NewApplication,
    RunRow,
    RunSummary,
    Store,
    UsageDay,
)

MAX_ATTEMPTS = 3


def test_should_refresh_stale_assessments_without_losing_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    stale = make_ad(
        label="junior",
        description="Build Java services.",
        match=make_match(version=OUTDATED_VERSION),
    )
    fresh = make_ad(
        id="finn:2", source_id="2", label="open", description="Build software."
    )
    store.upsert_ad(stale)
    store.upsert_ad(fresh)
    for _ in range(3):
        store.bump_label_attempts([stale.id])
    store.set_state("nav_position", "cursor-one")

    refreshed = store.refresh_assessments(FAKE_VERSION)

    pending = store.pending_classification(MAX_ATTEMPTS)
    assert refreshed is True
    assert [ad.id for ad in pending] == [stale.id]
    assert pending[0].description == stale.description
    assert pending[0].match is None
    assert store.cursors()["nav_position"] == "cursor-one"
    assert len(store.active_ads()) == 2


def test_should_refresh_reactivated_stale_assessment(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        label="junior",
        description="Build Java services.",
        match=make_match(version=OUTDATED_VERSION),
    )
    store.upsert_ad(ad)
    store.set_active(ad.id, False)
    assert store.refresh_assessments(FAKE_VERSION) is False
    store.set_active(ad.id, True)

    assert store.refresh_assessments(FAKE_VERSION) is True
    assert [row.id for row in store.pending_classification(MAX_ATTEMPTS)] == [ad.id]


def test_should_not_reset_current_failed_attempts(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description="Build Java services.", match=None)
    store.upsert_ad(ad)
    store.refresh_assessments(FAKE_VERSION)
    for _ in range(3):
        store.bump_label_attempts([ad.id])

    assert store.refresh_assessments(FAKE_VERSION) is False
    assert store.pending_classification(MAX_ATTEMPTS) == []


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

    ads = store.reportable_ads("2026-08-24", None, FAKE_VERSION, targets())

    assert [ad.id for ad in ads] == [current.id]


def test_should_report_reclassified_ad_once(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(match=None)
    store.upsert_ad(ad)
    since = datetime.now(tz=UTC).isoformat()
    store.set_classification(ad.id, make_classification(), FAKE_VERSION)

    first = store.reportable_ads("2026-09-30", since, FAKE_VERSION, targets())
    store.mark_reported([ad.id], since)
    second = store.reportable_ads("2026-09-30", since, FAKE_VERSION, targets())

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

    assert [
        ad.id
        for ad in store.reportable_ads("2026-08-24", None, FAKE_VERSION, targets())
    ] == [current.id]


WINDOW_START = "2026-09-30T00:00:00+00:00"
SKIPPED_LEVELS = frozenset({"senior", "lead"})


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


def test_should_persist_state_across_store_instances(tmp_path: Path) -> None:
    db_path = tmp_path / "ads.db"
    store = Store(db_path)
    store.set_state("nav_position", "https://example.test/feed/1")
    store.set_state("nav_position", "https://example.test/feed/2")

    reopened = Store(db_path)

    assert reopened.cursors()["nav_position"] == "https://example.test/feed/2"


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


def test_should_count_only_active_outdated_ads(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    outdated = make_match(version=OUTDATED_VERSION)
    store.upsert_ad(make_ad(id="finn:1", source_id="1", match=outdated))
    store.upsert_ad(make_ad(id="finn:2", source_id="2", match=None))
    store.upsert_ad(make_ad(id="finn:3", source_id="3"))
    inactive = make_ad(id="finn:4", source_id="4", match=outdated)
    store.upsert_ad(inactive)
    store.set_active(inactive.id, False)

    assert store.count_outdated(FAKE_VERSION) == 2


def test_should_exclude_expired_apply_status_from_reportable(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(label="junior", apply_url="https://a.test/jobs/abc"))
    store.set_apply_status("finn:1234", "expired", "2026-09-16T05:00:00+00:00")

    assert store.reportable_ads("2026-09-16", None, FAKE_VERSION, targets()) == []


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
        "2026-09-16", "2026-09-09T05:00:00+00:00", 10, targets()
    )

    assert {ad.id for ad in pending} == {unchecked.id, checked_long_ago.id}
    assert len(store.pending_apply_check("2026-09-16", "2026-09-09", 1, targets())) == 1


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

    pending = store.pending_apply_url("finn", 10, targets())

    assert [ad.id for ad in pending] == [missing.id]


def test_should_not_refetch_description_for_senior_label(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description=None, match=None)
    store.upsert_ad(ad)
    store.set_label(ad.id, "senior")

    assert store.pending_description(SKIPPED_LEVELS) == []


@pytest.mark.parametrize("status", ["expired", "skipped"])
def test_should_skip_apply_recheck_for_terminal_status(
    tmp_path: Path, status: str
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(apply_url="https://careers.example.test/jobs/1")
    store.upsert_ad(ad)
    store.set_classification(ad.id, make_classification(), FAKE_VERSION)
    store.set_apply_status(ad.id, status, "2026-01-01T00:00:00+00:00")

    pending = store.pending_apply_check(
        "2026-10-09", "2026-10-02T00:00:00+00:00", 60, targets()
    )

    assert pending == []


AT = "2026-10-09T10:00:00+00:00"
LATER = "2026-10-10T10:00:00+00:00"


def _store_with_ad(tmp_path: Path, **overrides: object) -> tuple[Store, str]:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(**overrides)
    store.upsert_ad(ad)
    return store, ad.id


def test_should_create_application_when_ad_marked_applied(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path, display_title="Backend Developer")

    store.mark_ad(ad_id, "applied", AT, cv_name="cv.pdf")

    [application] = store.applications()
    assert (application.ad_id, application.role, application.cv_name) == (
        ad_id,
        "Backend Developer",
        "cv.pdf",
    )
    assert [event.text for event in application.events] == ["Applied"]


def test_should_clear_other_exclusive_actions_when_marking(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "not_for_me", AT)

    store.mark_ad(ad_id, "skipped", LATER)

    assert store.ad_actions() == {ad_id: frozenset({"skipped"})}


def test_should_keep_show_anyway_when_marking_skipped(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "show_anyway", AT)

    store.mark_ad(ad_id, "skipped", LATER)

    assert store.ad_actions() == {ad_id: frozenset({"show_anyway", "skipped"})}


def test_should_soft_delete_application_when_applied_is_unmarked(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)

    store.unmark_ad(ad_id, "applied", LATER)

    [application] = store.applications(include_removed=True)
    assert (store.applications(), application.removed_at, len(application.events)) == (
        [],
        LATER,
        1,
    )


def test_should_restore_removed_application_with_its_events(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)
    [application] = store.applications()
    store.update_application(application.id, ApplicationChange(status="interview"), AT)
    store.remove_application(application.id, LATER)

    store.restore_application(application.id)

    [restored] = store.applications()
    assert (restored.id, [event.text for event in restored.events]) == (
        application.id,
        ["Applied", "Status: Interview"],
    )


def test_should_restore_application_when_ad_marked_applied_again(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)
    store.unmark_ad(ad_id, "applied", AT)

    store.mark_ad(ad_id, "applied", LATER)

    assert [app.removed_at for app in store.applications(include_removed=True)] == [
        None
    ]


def test_should_include_shown_anyway_ad_in_reportable_ads(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path, requires_master=True)
    store.mark_ad(ad_id, "show_anyway", AT)

    ads = store.reportable_ads("2026-10-09", None, FAKE_VERSION, targets())

    assert [ad.id for ad in ads] == [ad_id]


def test_should_reject_application_with_blank_company(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    blank = NewApplication(
        company=" ",
        role="Developer",
        place="",
        deadline=None,
        note="",
        applied_at=AT,
        cv_name=None,
        ad_id=None,
    )

    with pytest.raises(sqlite3.IntegrityError):
        store.create_application(blank)


def test_should_append_event_when_status_changes(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)
    [application] = store.applications()

    store.update_application(application.id, ApplicationChange(status="offer"), LATER)

    [updated] = store.applications()
    assert (updated.status, updated.events[-1]) == (
        "offer",
        ApplicationEvent(LATER, "Status: Offer"),
    )


def test_should_accumulate_usage_for_same_day(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    usage = Usage(calls=1, input_tokens=10, output_tokens=5, cost_usd=0.5)

    store.add_usage("2026-10-09", usage, ads_checked=1)
    store.add_usage("2026-10-09", usage, ads_read=7, ads_checked=1)

    assert store.usage_days("2026-10-01") == [
        UsageDay("2026-10-09", 2, 20, 10, 1.0, 7, 2)
    ]


def test_should_set_closed_at_and_reason_removed_when_ad_leaves_source(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path)

    store.sync_source_active("finn", frozenset())

    [closed] = store.closed_ads(_today())
    assert (closed.ad.id, closed.why) == (ad_id, "removed")


def test_should_set_reason_filled_when_apply_link_expires(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)

    store.set_apply_status(ad_id, "expired", AT)

    [closed] = store.closed_ads("2026-10-09")
    assert (closed.why, closed.closed_on) == ("filled", "2026-10-09")


def test_should_clear_closed_at_when_ad_returns(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.sync_source_active("finn", frozenset())

    store.sync_source_active("finn", frozenset({ad_id}))

    assert (store.closed_ads(_today()), store.active_ads()[0].closed_at) == ([], None)


def test_should_list_closed_ads_within_thirty_days_with_reason_and_action(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    edge = make_ad(id="finn:1", source_id="1", deadline="2026-08-01")
    old = make_ad(id="finn:2", source_id="2", deadline="2026-08-01")
    store.upsert_ad(old)
    store.close_past_deadline("2026-08-31")
    store.upsert_ad(edge)
    store.close_past_deadline("2026-09-01")
    store.mark_ad(edge.id, "applied", AT)

    closed = store.closed_ads("2026-10-01")

    assert [(item.ad.id, item.why, item.you) for item in closed] == [
        (edge.id, "deadline", "applied")
    ]


def test_should_mark_stale_running_row_failed_on_next_start(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    crashed = store.start_run(AT)

    store.start_run(LATER)

    [_, stale] = store.runs(limit=2)
    assert (stale.id, stale.status, stale.finished_at) == (crashed, "failed", LATER)


def test_should_report_ad_after_subfield_added_without_reclassifying(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path, role_type="frontend")
    before = store.reportable_ads(
        "2026-10-09", None, FAKE_VERSION, targets(subfields=("backend",))
    )

    after = store.reportable_ads(
        "2026-10-09", None, FAKE_VERSION, targets(subfields=("backend", "frontend"))
    )

    assert ([ad.id for ad in before], [ad.id for ad in after]) == ([], [ad_id])


def _today() -> str:
    return datetime.now(tz=UTC).date().isoformat()


def test_should_stop_listing_description_after_three_failed_fetches(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path, description=None, match=None)
    for _ in range(3):
        store.bump_description_attempts(ad_id)

    assert store.pending_description(SKIPPED_LEVELS) == []


def test_should_read_back_every_stored_ad_field(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        id="nav:abc",
        source="nav",
        source_id="abc",
        location="Trondheim",
        deadline="2026-11-01",
        description="Build services.",
        label="junior",
        summary="Backend work.",
        requires_master=True,
        display_title="Backend Developer",
        role_type="backend",
        technologies=("Java",),
        requirements=("Bachelor",),
        apply_url="https://jobs.example.test/apply",
        apply_status="active",
        apply_checked_at="2026-10-01T00:00:00+00:00",
        republished=True,
    )
    store.upsert_ad(ad)

    [stored] = store.active_ads()

    assert stored == replace(ad, first_seen=stored.first_seen)
    assert stored.first_seen is not None


def test_should_read_back_application_fields_after_note_change(
    tmp_path: Path,
) -> None:
    store = Store(tmp_path / "ads.db")
    app_id = store.create_application(
        NewApplication(
            company="Org One AS",
            role="Developer",
            place="Bergen",
            deadline="2026-11-01",
            note="",
            applied_at=AT,
            cv_name="cv.pdf",
            ad_id=None,
        )
    )

    store.update_application(app_id, ApplicationChange(note="Called back"), LATER)
    store.add_application_event(app_id, LATER, "Phone screen")

    assert store.applications() == [
        Application(
            id=app_id,
            ad_id=None,
            company="Org One AS",
            role="Developer",
            place="Bergen",
            deadline="2026-11-01",
            status="waiting",
            note="Called back",
            applied_at=AT,
            cv_name="cv.pdf",
            removed_at=None,
            events=(
                ApplicationEvent(AT, "Applied"),
                ApplicationEvent(LATER, "Phone screen"),
            ),
        )
    ]


def test_should_copy_ad_details_into_application_when_applied(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(
        tmp_path, location="Oslo", deadline="2026-11-01", display_title=None
    )

    store.mark_ad(ad_id, "applied", AT)

    [application] = store.applications()
    assert (
        application.role,
        application.place,
        application.deadline,
        application.note,
    ) == ("Utvikler", "Oslo", "2026-11-01", "")


def test_should_remove_application_at_switch_time_when_marked_skipped(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)

    store.mark_ad(ad_id, "skipped", LATER)

    [application] = store.applications(include_removed=True)
    assert application.removed_at == LATER


def test_should_keep_application_when_other_action_is_unmarked(
    tmp_path: Path,
) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.mark_ad(ad_id, "applied", AT)
    store.mark_ad(ad_id, "show_anyway", AT)

    store.unmark_ad(ad_id, "show_anyway", LATER)

    assert len(store.applications()) == 1


def test_should_read_back_finished_run(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.start_run(AT)
    run_id = store.start_run(LATER)
    summary = RunSummary(
        finished_at="2026-10-10T10:05:00+00:00",
        status="completed",
        ads_read=7,
        new_ads=5,
        matches=3,
        hidden=2,
        failed_sources=("nav",),
    )

    store.finish_run(run_id, summary)

    assert store.last_run() == RunRow(
        id=run_id,
        started_at=LATER,
        finished_at="2026-10-10T10:05:00+00:00",
        status="completed",
        ads_read=7,
        new_ads=5,
        matches=3,
        hidden=2,
        failed_sources=("nav",),
    )


def test_should_count_ads_closed_since_a_time(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(id="finn:1", source_id="1", deadline="2026-08-01"))
    store.close_past_deadline("2026-09-01")
    store.upsert_ad(make_ad(id="finn:2", source_id="2", deadline="2026-09-15"))
    store.close_past_deadline("2026-10-01")

    assert store.closed_since("2026-09-15") == 1


def test_should_list_newest_pending_apply_url_first(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    for index in (1, 2):
        store.upsert_ad(
            make_ad(id=f"finn:{index}", source_id=str(index), description="x")
        )

    pending = store.pending_apply_url("finn", 1, targets())

    assert [ad.id for ad in pending] == ["finn:2"]


def test_should_list_unchecked_apply_links_before_rechecks(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    for index in (1, 2):
        store.upsert_ad(
            make_ad(
                id=f"finn:{index}",
                source_id=str(index),
                apply_url=f"https://a.test/{index}",
            )
        )
    store.set_apply_status("finn:2", "active", "2026-09-01T00:00:00+00:00")

    pending = store.pending_apply_check("2026-10-09", "2026-10-02", 2, targets())

    assert [ad.id for ad in pending] == ["finn:1", "finn:2"]


def test_should_expose_source_cursors_without_schema_version(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.set_state("nav_position", "page-1")

    assert store.cursors() == {"nav_position": "page-1"}


def test_should_reopen_closed_finn_ad_listed_again(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    store.sync_finn_active(frozenset(), frozenset(), None)

    store.sync_finn_active(frozenset({ad_id}), frozenset(), None)

    assert [ad.id for ad in store.active_ads()] == [ad_id]


def test_should_default_usage_counts_to_zero(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")

    store.add_usage("2026-10-09", Usage(calls=1))

    [day] = store.usage_days("2026-10-09")
    assert (day.ads_read, day.ads_checked) == (0, 0)


def test_should_store_checks_inside_assessment(tmp_path: Path) -> None:
    store, ad_id = _store_with_ad(tmp_path)
    checks = (Check(text="Java", state="met"),)

    store.set_classification(ad_id, make_classification(checks=checks), FAKE_VERSION)

    with sqlite3.connect(tmp_path / "ads.db") as conn:
        [raw] = conn.execute("SELECT assessment FROM ads").fetchone()
    assert json.loads(raw)["checks"] == [{"text": "Java", "state": "met"}]


def test_should_read_back_alternate_flag_values(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(display_title=None, republished=False, is_programming_job=True)
    store.upsert_ad(ad)

    [stored] = store.active_ads()

    assert (stored.is_programming_job, stored.republished) == (True, False)


def test_should_show_closed_date_on_ad(tmp_path: Path) -> None:
    store, _ = _store_with_ad(tmp_path, deadline="2026-08-01")
    store.close_past_deadline("2026-09-01")

    [stored] = store.active_ads()

    assert stored.closed_at == "2026-09-01"


def test_should_create_database_readable_by_owner_only(tmp_path: Path) -> None:
    path = tmp_path / "data" / "jobranger" / "ads.db"

    Store(path)

    assert (path.stat().st_mode & 0o777, path.parent.stat().st_mode & 0o777) == (
        0o600,
        0o700,
    )
