from __future__ import annotations

import logging
from dataclasses import replace
from datetime import UTC, datetime, timedelta
from pathlib import Path

import httpx
import pytest
from factories import (
    ASHBY_BOARD,
    OTHER_TEAMTAILOR_FEED,
    TEAMTAILOR_FEED,
    make_ad,
    make_classification,
    make_match,
)

from jobranger import main
from jobranger.ads import apply_link
from jobranger.ads.models import Ad, SourceSnapshot
from jobranger.config import Settings
from jobranger.main import (
    _check_apply_links,
    _classify_pending,
    _pull_ashby_board,
    _pull_finn,
    _pull_finn_recent,
    _pull_linkedin,
    _pull_nav,
    _pull_teamtailor_feed,
    _report,
    _store_finn_pages,
    _veto_missing_employer_listings,
    run,
)
from jobranger.matching import classify
from jobranger.matching.classify import BatchResult, Usage
from jobranger.net import rate_limit
from jobranger.notify import digest
from jobranger.sources import ashby, finn, linkedin, nav, teamtailor
from jobranger.sources.registry import AshbyBoard, TeamtailorFeed
from jobranger.storage.store import Store


def test_should_report_older_ads_when_classification_completes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description="Build Java services.", match=None)
    store.upsert_ad(ad)
    result = make_classification()
    monkeypatch.setattr(
        classify, "classify_batch", lambda ads, claude: BatchResult({ad.id: result})
    )

    run_started = datetime.now(tz=UTC).isoformat()
    _classify_pending(store, _settings(tmp_path))
    settings = _settings(tmp_path)
    _report(store, replace(settings, report_all=False), run_started)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert ad.url in summary


def test_should_not_repeat_ad_after_reclassification(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description="Build Java services.", match=None)
    store.upsert_ad(ad)
    settings = replace(_settings(tmp_path), report_all=False)

    first_run = datetime.now(tz=UTC).isoformat()
    store.set_classification(ad.id, make_classification())
    _report(store, settings, first_run)
    first = next(settings.report_dir.iterdir()).read_text()
    second_run = datetime.now(tz=UTC).isoformat()
    store.set_classification(ad.id, make_classification())
    _report(store, settings, second_run)
    second = next(settings.report_dir.iterdir()).read_text()

    assert ad.url in first
    assert ad.url not in second


def test_should_report_reclassified_ad_after_full_listing(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(description="Build Java services.", match=None)
    store.upsert_ad(ad)
    settings = _settings(tmp_path)
    store.set_classification(ad.id, make_classification())
    _report(store, settings, datetime.now(tz=UTC).isoformat())

    daily_run = datetime.now(tz=UTC).isoformat()
    store.set_classification(ad.id, make_classification())
    _report(store, replace(settings, report_all=False), daily_run)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert ad.url in summary


def test_should_not_reuse_stale_duplicate_match(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    store = Store(tmp_path / "ads.db")
    stale = make_ad(
        label="junior",
        description="Build Java services.",
        match=make_match(version="old"),
    )
    pending = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        description="Build Java services.",
        match=None,
    )
    store.upsert_ad(stale)
    store.upsert_ad(pending)
    received: list[Ad] = []

    def unresolved(ads: list[Ad], claude_bin: str) -> BatchResult:
        received.extend(ads)
        return BatchResult({})

    monkeypatch.setattr(classify, "classify_batch", unresolved)

    _classify_pending(store, _settings(tmp_path))

    assert len(received) == 1
    assert all(ad.label is None for ad in store.active_ads())


def _settings(tmp_path: Path) -> Settings:
    return Settings(
        db_path=tmp_path / "ads.db",
        report_dir=tmp_path / "reports",
        finn_occupations="0.23",
        discord_webhook_url="",
        claude_bin="claude",
        report_all=True,
        reclassify_all=False,
        log_dir=tmp_path / "logs",
    )


def test_should_include_ads_from_before_run_in_full_report(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(label="junior")
    store.upsert_ad(ad)
    settings = _settings(tmp_path)

    _report(store, settings, "9999-01-01T00:00:00+00:00")

    summary = next(settings.report_dir.iterdir()).read_text()
    assert ad.url in summary
    assert "full listing" in summary


def test_should_count_new_ads_that_do_not_qualify(tmp_path: Path) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(label="senior", match=make_match(status="unsuitable")))
    settings = replace(_settings(tmp_path), report_all=False)

    _report(store, settings, "2000-01-01T00:00:00+00:00")

    summary = next(settings.report_dir.iterdir()).read_text()
    assert "**1 new ad scanned** · **0 matching jobs**" in summary


def test_should_omit_webhook_url_from_report_failure_log(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    caplog: pytest.LogCaptureFixture,
) -> None:
    webhook_url = "https://discord.example/webhook/secret"
    store = Store(tmp_path / "ads.db")
    settings = replace(_settings(tmp_path), discord_webhook_url=webhook_url)

    def fail_delivery(url: str, content: str) -> None:
        request = httpx.Request("POST", url)
        raise httpx.ConnectError("failed", request=request)

    monkeypatch.setattr(digest, "send_discord", fail_delivery)

    with caplog.at_level(logging.ERROR):
        _report(store, settings, "9999-01-01T00:00:00+00:00")

    assert "report.discord.failed error=ConnectError" in caplog.text
    assert webhook_url not in caplog.text


def test_should_sync_only_the_successful_ashby_board(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    first_board = make_ad(
        id="ashby:example-one:1",
        source="ashby:example-one",
        source_id="1",
    )
    second_board = make_ad(
        id="ashby:example-two:1",
        source="ashby:example-two",
        source_id="1",
    )
    store.upsert_ad(first_board)
    store.upsert_ad(second_board)
    board = ASHBY_BOARD
    monkeypatch.setattr(
        ashby,
        "fetch_board",
        lambda client, selected: SourceSnapshot([], frozenset()),
    )

    with httpx.Client() as client:
        succeeded = _pull_ashby_board(client, store, board)

    assert succeeded is True
    assert [ad.id for ad in store.active_ads()] == [second_board.id]


def test_should_preserve_ashby_board_after_parse_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    existing = make_ad(
        id="ashby:example-one:1",
        source="ashby:example-one",
        source_id="1",
    )
    store.upsert_ad(existing)
    board = ASHBY_BOARD

    def fail_fetch(client: httpx.Client, selected: AshbyBoard) -> SourceSnapshot:
        raise ashby.AshbyParseError("invalid")

    monkeypatch.setattr(ashby, "fetch_board", fail_fetch)

    with httpx.Client() as client:
        succeeded = _pull_ashby_board(client, store, board)

    assert succeeded is False
    assert [ad.id for ad in store.active_ads()] == [existing.id]


def test_should_store_successful_teamtailor_snapshot(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    ad = make_ad(
        id="teamtailor:example:1",
        source="teamtailor:example",
        source_id="1",
        url="https://careers.example.test/jobs/1",
    )
    feed = TEAMTAILOR_FEED
    monkeypatch.setattr(
        teamtailor,
        "fetch_feed",
        lambda client, selected: SourceSnapshot([ad], frozenset({ad.id})),
    )

    with httpx.Client() as client:
        succeeded = _pull_teamtailor_feed(client, store, feed)

    assert succeeded is True
    assert [stored.id for stored in store.active_ads()] == [ad.id]


def test_should_classify_duplicate_rows_once(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    finn_ad = make_ad(
        id="finn:1",
        source_id="1",
        title="Backend Developer",
        label=None,
        description="Build Python services for customers and internal teams.",
    )
    direct_ad = make_ad(
        id="ashby:example-one:1",
        source="ashby:example-one",
        source_id="1",
        title="Backend Developer",
        label=None,
        description="Build Python services.",
        url="https://jobs.example.test/example-one/1",
    )
    store.upsert_ad(finn_ad)
    store.upsert_ad(direct_ad)
    received: list[Ad] = []
    result = make_classification(label="junior")

    def classify_once(ads: list[Ad], claude_bin: str) -> BatchResult:
        received.extend(ads)
        return BatchResult({ads[0].id: result})

    monkeypatch.setattr(classify, "classify_batch", classify_once)

    _classify_pending(store, _settings(tmp_path))

    assert [ad.id for ad in received] == [direct_ad.id]
    assert received[0].description == finn_ad.description
    assert {ad.label for ad in store.active_ads()} == {"junior"}


def test_should_reuse_existing_duplicate_classification(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    classified = make_ad(
        id="finn:1",
        source_id="1",
        title="Backend Developer",
        label="junior",
        description="Build Python services.",
        technologies=("Python",),
        match=make_match(
            status="stretch", gaps=("Preferred Qt experience not documented",)
        ),
    )
    pending = make_ad(
        id="teamtailor:example:1",
        source="teamtailor:example",
        source_id="1",
        title="Backend Developer",
        label=None,
        description="Build Python services.",
        url="https://careers.example.test/jobs/1",
        match=None,
    )
    store.upsert_ad(classified)
    store.upsert_ad(pending)

    def unexpected_call(ads: list[Ad], claude_bin: str) -> BatchResult:
        pytest.fail("classification should have been reused")

    monkeypatch.setattr(classify, "classify_batch", unexpected_call)

    _classify_pending(store, _settings(tmp_path))

    stored = {ad.id: ad for ad in store.active_ads()}
    assert stored[pending.id].label == "junior"
    assert stored[pending.id].technologies == ("Python",)
    assert stored[pending.id].match == classified.match


def test_should_not_reuse_error_classification(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    failed = make_ad(
        id="finn:1",
        source_id="1",
        title="Backend Developer",
        label="error",
        description="Build Python services.",
        is_programming_job=False,
    )
    pending = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        title="Backend Developer",
        label=None,
        description="Build Python services.",
    )
    store.upsert_ad(failed)
    store.upsert_ad(pending)
    calls: list[str] = []
    result = make_classification(label="open")

    def classify_first(ads: list[Ad], claude_bin: str) -> BatchResult:
        ad_id = ads[0].id
        calls.append(ad_id)
        return BatchResult({ad_id: result})

    monkeypatch.setattr(classify, "classify_batch", classify_first)

    _classify_pending(store, _settings(tmp_path))

    assert calls == [pending.id]
    assert {ad.id: ad.label for ad in store.active_ads()}[pending.id] == "open"


def test_should_classify_distinct_same_company_roles_separately(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    frontend = make_ad(
        id="finn:1",
        source_id="1",
        title="Frontend Developer",
        label=None,
        description="Build React interfaces.",
        url="https://example.test/frontend",
    )
    backend = make_ad(
        id="finn:2",
        source_id="2",
        title="Backend Developer",
        label=None,
        description="Build Java services.",
        url="https://example.test/backend",
    )
    store.upsert_ad(frontend)
    store.upsert_ad(backend)
    received: list[str] = []

    def classify_both(ads: list[Ad], claude_bin: str) -> BatchResult:
        received.extend(ad.id for ad in ads)
        return BatchResult({})

    monkeypatch.setattr(classify, "classify_batch", classify_both)

    _classify_pending(store, _settings(tmp_path))

    assert received == [frontend.id, backend.id]


def test_should_name_failed_sources_in_report(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(finn, "fetch_new_ads", _fail_finn_search)
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    settings = replace(_settings(tmp_path), report_all=False)

    exit_code = run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert exit_code == 0
    assert "**0 new ads scanned** · **0 matching jobs**" in summary
    assert "**Sources failed:** finn" in summary


def test_should_write_dated_run_log_during_run(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(finn, "fetch_new_ads", _fail_finn_search)
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    settings = replace(_settings(tmp_path), report_all=False)

    run(settings)

    today = datetime.now(tz=UTC).date().isoformat()
    assert (settings.log_dir / f"{today}.log").exists()


def test_should_name_failed_ashby_board_in_report(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    board = ASHBY_BOARD

    def fail_fetch(client: httpx.Client, selected: AshbyBoard) -> SourceSnapshot:
        raise ashby.AshbyParseError("invalid")

    monkeypatch.setattr(finn, "fetch_new_ads", _fail_finn_search)
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    monkeypatch.setattr(main, "ASHBY_BOARDS", (board,))
    monkeypatch.setattr(ashby, "fetch_board", fail_fetch)
    settings = replace(_settings(tmp_path), report_all=False)

    run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    failed = next(line for line in summary.splitlines() if "Sources failed" in line)
    assert "ashby:example-one" in failed


def test_should_name_failed_teamtailor_feed_in_report(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    feed = OTHER_TEAMTAILOR_FEED

    def fail_fetch(client: httpx.Client, selected: TeamtailorFeed) -> SourceSnapshot:
        raise teamtailor.TeamtailorParseError("invalid")

    monkeypatch.setattr(finn, "fetch_new_ads", _fail_finn_search)
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    monkeypatch.setattr(main, "TEAMTAILOR_FEEDS", (feed,))
    monkeypatch.setattr(teamtailor, "fetch_feed", fail_fetch)
    settings = replace(_settings(tmp_path), report_all=False)

    run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    failed = next(line for line in summary.splitlines() if "Sources failed" in line)
    assert "teamtailor:org-two" in failed


def test_should_log_model_usage(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    caplog: pytest.LogCaptureFixture,
) -> None:
    settings = _run_settings_with_usage(tmp_path, monkeypatch)

    with caplog.at_level(logging.INFO):
        run(settings)

    assert (
        "classify.usage calls=23 input=500 cache_write=300000 "
        "cache_read=1000000 output=52000 total=1352500 cost_usd=6.10"
    ) in caplog.text


def test_should_report_model_usage(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    settings = _run_settings_with_usage(tmp_path, monkeypatch)

    run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert "*Model: 23 calls · 1.4M tokens (1.3M in, 52k out)" in summary


def _run_settings_with_usage(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> Settings:
    usage = Usage(
        calls=23,
        input_tokens=500,
        cache_write_tokens=300_000,
        cache_read_tokens=1_000_000,
        output_tokens=52_000,
        cost_usd=6.1,
    )
    settings = replace(_settings(tmp_path), report_all=False)
    Store(settings.db_path).upsert_ad(make_ad(description="Build.", match=None))
    _stub_finn(monkeypatch, [], frozenset())
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    monkeypatch.setattr(
        classify, "classify_batch", lambda ads, claude: BatchResult({}, usage)
    )
    return settings


def test_should_report_failed_recent_scan(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fail_recent(client: httpx.Client, known: set[str], since: str) -> None:
        raise httpx.ConnectError("refused", request=httpx.Request("GET", "https://f"))

    listed = make_ad(description="Build Java services.")
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(listed)
    store.set_active(listed.id, False)
    _stub_finn(monkeypatch, [], frozenset({listed.id}))
    monkeypatch.setattr(finn, "fetch_recent_ads", fail_recent)
    _stub_other_sources(monkeypatch)
    settings = replace(_settings(tmp_path), report_all=False)

    run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert "**Sources failed:** finn-recent" in summary.splitlines()
    assert [ad.id for ad in Store(settings.db_path).active_ads()] == [listed.id]


RUN_STARTED = "2026-10-07T03:00:00+00:00"


def test_should_store_recent_ad_outside_category_expiry(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    wide = make_ad(id="finn:77", source_id="77", description=None)
    _stub_recent(monkeypatch, [wide])

    with httpx.Client() as client:
        _pull_finn_recent(client, store, RUN_STARTED)
    store.sync_finn_active(frozenset(), frozenset(), None)

    assert [ad.id for ad in store.active_ads()] == [wide.id]


@pytest.mark.parametrize(
    ("stored_since", "expected_since"),
    [(None, "2026-09-30T03:00:00+00:00"), ("2026-10-05T03:00:00+00:00",) * 2],
    ids=["first run", "later run"],
)
def test_should_scan_recent_ads_from_stored_cursor(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    stored_since: str | None,
    expected_since: str,
) -> None:
    store = Store(tmp_path / "ads.db")
    if stored_since:
        store.set_state("finn_recent_since", stored_since)
    calls = _stub_recent(monkeypatch, [])

    with httpx.Client() as client:
        _pull_finn_recent(client, store, RUN_STARTED)

    assert [since for since, _ in calls] == [expected_since]


@pytest.mark.parametrize(
    ("stored_since", "expected_window"),
    [(None, 7 * 86_400), ("2026-10-05T03:00:00+00:00", 2 * 86_400)],
    ids=["first run", "later run"],
)
def test_should_search_linkedin_since_stored_cursor(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    stored_since: str | None,
    expected_window: int,
) -> None:
    store = Store(tmp_path / "ads.db")
    if stored_since:
        store.set_state("linkedin_since", stored_since)
    calls = _stub_linkedin(monkeypatch, [])

    with httpx.Client() as client:
        _pull_linkedin(client, store, RUN_STARTED)

    assert [window for _, window in calls] == [expected_window]


def test_should_pass_stored_ids_to_linkedin(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    existing = make_ad()
    store.upsert_ad(existing)
    calls = _stub_linkedin(monkeypatch, [])

    with httpx.Client() as client:
        _pull_linkedin(client, store, RUN_STARTED)

    assert [known for known, _ in calls] == [{existing.id}]


def test_should_store_new_linkedin_ads(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    found = make_ad(id="linkedin:1", source="linkedin", source_id="1")
    _stub_linkedin(monkeypatch, [found])

    with httpx.Client() as client:
        _pull_linkedin(client, store, RUN_STARTED)

    assert found.id in store.known_ids()


def test_should_advance_linkedin_cursor_one_day_before_run(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    _stub_linkedin(monkeypatch, [])

    with httpx.Client() as client:
        succeeded = _pull_linkedin(client, store, RUN_STARTED)

    assert succeeded is True
    assert store.get_state("linkedin_since") == "2026-10-06T03:00:00+00:00"


@pytest.mark.parametrize(
    "failure",
    [
        httpx.HTTPStatusError(
            "429",
            request=httpx.Request("GET", "https://l"),
            response=httpx.Response(429),
        ),
        linkedin.LinkedInParseError("sign-in wall"),
    ],
)
def test_should_keep_linkedin_cursor_when_search_fails(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, failure: Exception
) -> None:
    def fail(client: httpx.Client, known: set[str], window: int) -> list[Ad]:
        raise failure

    settings = replace(_settings(tmp_path), report_all=False)
    store = Store(settings.db_path)
    store.set_state("linkedin_since", "2026-10-05T03:00:00+00:00")
    listed = make_ad(description="Build Java services.")
    store.upsert_ad(listed)
    _stub_finn(monkeypatch, [], frozenset({listed.id}))
    _stub_recent(monkeypatch, [])
    _stub_other_sources(monkeypatch)
    monkeypatch.setattr(linkedin, "fetch_new_ads", fail)

    run(settings)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert "**Sources failed:** linkedin" in summary.splitlines()
    stored = Store(settings.db_path).get_state("linkedin_since")
    assert stored == "2026-10-05T03:00:00+00:00"


def test_should_pass_stored_ids_to_recent_scan(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    existing = make_ad()
    store.upsert_ad(existing)
    calls = _stub_recent(monkeypatch, [])

    with httpx.Client() as client:
        _pull_finn_recent(client, store, RUN_STARTED)

    assert [known for _, known in calls] == [{existing.id}]


def test_should_advance_recent_cursor_one_day_before_run(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    _stub_recent(monkeypatch, [])

    with httpx.Client() as client:
        _pull_finn_recent(client, store, RUN_STARTED)

    assert store.get_state("finn_recent_since") == "2026-10-06T03:00:00+00:00"


def test_should_sync_recent_ads_by_what_the_recent_scan_saw(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    listed = make_ad(id="finn:77", source_id="77", published=RUN_STARTED)
    removed = make_ad(id="finn:78", source_id="78", published=RUN_STARTED)
    for ad in (listed, removed):
        store.upsert_ad(ad)
    store.mark_category_unlisted([listed.id, removed.id])
    recent = finn.FinnRecentResult(
        [], frozenset({listed.id}), "2026-10-01T00:00:00+00:00"
    )
    _stub_finn(monkeypatch, [], frozenset({"finn:1"}))

    with httpx.Client() as client:
        _pull_finn(client, store, _settings(tmp_path), recent)

    assert [ad.id for ad in store.active_ads()] == [listed.id]


def test_should_not_deactivate_finn_ads_on_empty_result(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    existing = make_ad(description="Build Java services.")
    store.upsert_ad(existing)
    monkeypatch.setattr(
        finn,
        "fetch_new_ads",
        lambda client, occupations, known: finn.FinnSearchResult([], frozenset(), True),
    )

    with httpx.Client() as client:
        succeeded = _pull_finn(client, store, _settings(tmp_path))

    assert succeeded is False
    assert [ad.id for ad in store.active_ads()] == [existing.id]


def test_should_store_apply_url_with_description(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    new_ad = make_ad(description=None)
    _stub_finn(monkeypatch, [new_ad], frozenset({new_ad.id}))

    with httpx.Client() as client:
        _pull_finn(client, store, _settings(tmp_path))

    stored = store.active_ads()[0]
    assert stored.description == "Build Java services."
    assert stored.apply_url == "https://thehub.io/jobs/x"


def test_should_check_only_reportable_apply_links(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    junior = make_ad(
        id="finn:1", source_id="1", label="junior", apply_url="https://thehub.io/jobs/1"
    )
    senior = make_ad(
        id="finn:2", source_id="2", label="senior", apply_url="https://thehub.io/jobs/2"
    )
    native = make_ad(
        id="finn:3",
        source_id="3",
        label="junior",
        apply_url="https://www.finn.no/job-apply/3/apply",
    )
    for ad in (junior, senior, native):
        store.upsert_ad(ad)
    checked: list[str] = []

    def record(client: httpx.Client, url: str) -> str:
        checked.append(url)
        return "expired"

    monkeypatch.setattr(apply_link, "check_apply_link", record)

    with httpx.Client() as client:
        _check_apply_links(client, store, datetime(2026, 9, 16, 5, tzinfo=UTC))

    stored = {ad.id: ad for ad in store.active_ads()}
    assert sorted(checked) == sorted([junior.apply_url, native.apply_url])
    assert stored[junior.id].apply_status == "expired"
    assert stored[native.id].apply_status == "expired"
    assert stored[senior.id].apply_status is None


@pytest.mark.parametrize(
    "feed",
    [
        OTHER_TEAMTAILOR_FEED,
        replace(
            ASHBY_BOARD, key="org-two", company="Org Two AS", board_name="Org Two AS"
        ),
    ],
    ids=["teamtailor", "ashby"],
)
def test_should_expire_listing_missing_from_employer_feed(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    feed: AshbyBoard | TeamtailorFeed,
) -> None:
    is_ashby = isinstance(feed, AshbyBoard)
    monkeypatch.setattr(main, "ASHBY_BOARDS", (feed,) if is_ashby else ())
    monkeypatch.setattr(main, "TEAMTAILOR_FEEDS", () if is_ashby else (feed,))
    store = Store(tmp_path / "ads.db")
    feed_ad = make_ad(
        id=f"{feed.source}:1",
        source=feed.source,
        source_id="1",
        title="Frontend Developer",
        company="Org Two AS",
        url="https://careers.example.test/jobs/1",
    )
    live_import = make_ad(
        id="finn:1",
        source_id="1",
        title="Frontend Developer",
        company="Org Two AS",
        url="https://www.finn.no/job/ad/1",
    )
    dead_import = make_ad(
        id="finn:2",
        source_id="2",
        title="Data Engineer",
        company="Org Two AS",
        url="https://www.finn.no/job/ad/2",
    )
    other_company = make_ad(
        id="finn:3",
        source_id="3",
        title="Data Engineer",
        company="Org One AS",
        url="https://www.finn.no/job/ad/3",
    )
    for ad in (feed_ad, live_import, dead_import, other_company):
        store.upsert_ad(ad)

    _veto_missing_employer_listings(
        store, frozenset({feed.source}), "2026-09-16T05:00:00+00:00"
    )

    ads = {ad.id: ad for ad in store.active_ads()}
    assert ads[dead_import.id].apply_status == "expired"
    assert ads[dead_import.id].apply_checked_at == "2026-09-16T05:00:00+00:00"
    assert ads[live_import.id].apply_status is None
    assert ads[other_company.id].apply_status is None


def test_should_skip_veto_when_employer_feed_failed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _stub_employer_feed(monkeypatch)
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(
        make_ad(
            id="finn:2",
            source_id="2",
            title="Data Engineer",
            company="Org Two AS",
        )
    )

    _veto_missing_employer_listings(store, frozenset(), "2026-09-16T05:00:00+00:00")

    assert store.active_ads()[0].apply_status is None


def test_should_backfill_apply_url_for_existing_ads(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    existing = make_ad(label="junior", description="Build Java services.")
    store.upsert_ad(existing)
    _stub_finn(monkeypatch, [], frozenset({existing.id}))

    with httpx.Client() as client:
        _pull_finn(client, store, _settings(tmp_path))

    assert store.active_ads()[0].apply_url == "https://thehub.io/jobs/x"


def _stub_finn(
    monkeypatch: pytest.MonkeyPatch, new_ads: list[Ad], listed: frozenset[str]
) -> None:
    monkeypatch.setattr(
        finn,
        "fetch_new_ads",
        lambda client, occupations, known: finn.FinnSearchResult(new_ads, listed, True),
    )
    monkeypatch.setattr(
        finn,
        "fetch_ad_page",
        lambda client, url: finn.FinnAdPage(
            "Build Java services.", "https://thehub.io/jobs/x"
        ),
    )


def _stub_recent(
    monkeypatch: pytest.MonkeyPatch, new_ads: list[Ad]
) -> list[tuple[str, set[str]]]:
    calls: list[tuple[str, set[str]]] = []

    def recent(client: httpx.Client, known: set[str], since: str) -> object:
        calls.append((since, known))
        ids = frozenset(ad.id for ad in new_ads)
        return finn.FinnRecentResult(new_ads, ids, None)

    monkeypatch.setattr(finn, "fetch_recent_ads", recent)
    return calls


def _stub_linkedin(
    monkeypatch: pytest.MonkeyPatch, new_ads: list[Ad]
) -> list[tuple[set[str], int]]:
    calls: list[tuple[set[str], int]] = []

    def fetch(client: httpx.Client, known: set[str], window: int) -> list[Ad]:
        calls.append((known, window))
        return new_ads

    monkeypatch.setattr(linkedin, "fetch_new_ads", fetch)
    return calls


def _stub_employer_feed(monkeypatch: pytest.MonkeyPatch) -> None:
    feed = OTHER_TEAMTAILOR_FEED
    monkeypatch.setattr(main, "TEAMTAILOR_FEEDS", (feed,))
    monkeypatch.setattr(main, "ASHBY_BOARDS", ())


def _fail_finn_search(client: httpx.Client, occupations: str, known: set[str]) -> None:
    raise httpx.ConnectError("refused", request=httpx.Request("GET", "https://f"))


def _stub_other_sources(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(nav, "fetch_public_token", lambda client: "token")
    monkeypatch.setattr(nav, "iter_new_entries", lambda client, token, start: iter([]))
    monkeypatch.setattr(
        ashby, "fetch_board", lambda client, board: SourceSnapshot([], frozenset())
    )
    monkeypatch.setattr(
        teamtailor, "fetch_feed", lambda client, feed: SourceSnapshot([], frozenset())
    )
    monkeypatch.setattr(classify, "classify_batch", lambda ads, claude: BatchResult({}))
    _stub_linkedin(monkeypatch, [])


def _report_twin_on_second_day(
    tmp_path: Path, first_reported_at: str | None = None
) -> tuple[Store, Settings, Ad]:
    store = Store(tmp_path / "ads.db")
    settings = replace(_settings(tmp_path), report_all=False)
    first_run = datetime.now(tz=UTC).isoformat()
    first = make_ad(id="finn:1", company="Org One AS", title="Utvikler", match=None)
    store.upsert_ad(first)
    store.set_classification(first.id, make_classification())
    _report(store, settings, first_run)
    if first_reported_at:
        store.mark_reported([first.id], first_reported_at)
    twin = make_ad(
        id="nav:1",
        source="nav",
        source_id="1",
        company="IT, Org One",
        title="Utvikler",
        url="https://arbeidsplassen.nav.no/stillinger/stilling/1",
        match=None,
    )
    second_run = datetime.now(tz=UTC).isoformat()
    store.upsert_ad(twin)
    store.set_classification(twin.id, make_classification())
    _report(store, settings, second_run)
    return store, settings, twin


def test_should_skip_twin_of_job_reported_on_earlier_day(tmp_path: Path) -> None:
    _, settings, twin = _report_twin_on_second_day(tmp_path)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert twin.url not in summary
    assert "0 matching jobs" in summary


def test_should_mark_covered_twin_as_reported(tmp_path: Path) -> None:
    store, _, twin = _report_twin_on_second_day(tmp_path)

    reported = {ad.id for ad in store.reported_ads("2000-01-01")}
    assert twin.id in reported


def test_should_report_twin_of_job_reported_before_lookback(tmp_path: Path) -> None:
    long_ago = (datetime.now(tz=UTC) - timedelta(days=91)).isoformat()

    _, settings, twin = _report_twin_on_second_day(tmp_path, long_ago)

    summary = next(settings.report_dir.iterdir()).read_text()
    assert twin.url in summary


def test_should_label_senior_sibling_of_freshly_classified_group(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    plain = make_ad(
        id="teamtailor:example:1",
        source="teamtailor:example",
        source_id="1",
        title="Fullstack Engineer",
        label=None,
        description=SHARED_DESCRIPTION,
        url="https://careers.example.test/jobs/1",
        match=None,
    )
    senior = replace(
        plain,
        id="teamtailor:example:2",
        source_id="2",
        title="Senior Fullstack Engineer",
        url="https://careers.example.test/jobs/2",
    )
    store.upsert_ad(plain)
    store.upsert_ad(senior)
    result = make_classification(label="open")
    monkeypatch.setattr(
        classify,
        "classify_batch",
        lambda ads, claude: BatchResult({ads[0].id: result}),
    )

    _classify_pending(store, _settings(tmp_path))

    stored = {ad.id: ad for ad in store.active_ads()}
    assert stored[senior.id].label == "senior"


SHARED_DESCRIPTION = (
    "Build the product across the stack with TypeScript and Python, own features "
    "from design to production, and work closely with product and design."
)


def test_should_label_reused_classification_senior_for_senior_title(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    classified = make_ad(
        id="teamtailor:example:1",
        source="teamtailor:example",
        source_id="1",
        title="Fullstack Engineer",
        label="open",
        description=SHARED_DESCRIPTION,
        url="https://careers.example.test/jobs/1",
    )
    pending = make_ad(
        id="teamtailor:example:2",
        source="teamtailor:example",
        source_id="2",
        title="Senior Fullstack Engineer",
        label=None,
        description=SHARED_DESCRIPTION,
        url="https://careers.example.test/jobs/2",
        match=None,
    )
    store.upsert_ad(classified)
    store.upsert_ad(pending)

    def unexpected_call(ads: list[Ad], claude_bin: str) -> BatchResult:
        pytest.fail("classification should have been reused")

    monkeypatch.setattr(classify, "classify_batch", unexpected_call)

    _classify_pending(store, _settings(tmp_path))

    stored = {ad.id: ad for ad in store.active_ads()}
    assert stored[pending.id].label == "senior"


def test_should_skip_page_fetch_for_senior_finn_title(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    senior = make_ad(
        id="finn:1",
        source_id="1",
        title="Senior utvikler",
        description=None,
        match=None,
    )
    plain = make_ad(
        id="finn:2",
        source_id="2",
        title="Utvikler",
        description=None,
        match=None,
        url="https://www.finn.no/job/ad/2",
    )
    store.upsert_ad(senior)
    store.upsert_ad(plain)
    fetched: list[str] = []

    def fetch(client: httpx.Client, url: str) -> finn.FinnAdPage:
        fetched.append(url)
        return finn.FinnAdPage("Build Java services.", "https://thehub.io/jobs/x")

    monkeypatch.setattr(finn, "fetch_ad_page", fetch)

    with httpx.Client() as client:
        _store_finn_pages(client, store, store.pending_description())

    assert fetched == [plain.url]
    stored = {ad.id: ad for ad in store.active_ads()}
    assert stored[senior.id].label == "senior"


def test_should_stop_fetching_page_after_three_failures(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(id="finn:1", source_id="1", description=None, match=None))

    def fail(client: httpx.Client, url: str) -> finn.FinnAdPage:
        raise httpx.ConnectError("refused", request=httpx.Request("GET", url))

    monkeypatch.setattr(finn, "fetch_ad_page", fail)

    with httpx.Client() as client:
        for _ in range(2):
            _store_finn_pages(client, store, store.pending_description())
        still_pending = [ad.id for ad in store.pending_description()]
        _store_finn_pages(client, store, store.pending_description())

    assert still_pending == ["finn:1"]
    assert store.pending_description() == []


def test_should_read_stored_ids_once_per_nav_pull(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    store = Store(tmp_path / "ads.db")
    entries = [
        ({"_feed_entry": {"uuid": f"u{index}", "status": "ACTIVE"}}, "page-1")
        for index in range(3)
    ]
    monkeypatch.setattr(nav, "fetch_public_token", lambda client: "token")
    monkeypatch.setattr(
        nav, "iter_new_entries", lambda client, token, start: iter(entries)
    )
    monkeypatch.setattr(nav, "fetch_entry", lambda client, token, uuid: None)
    calls = 0
    known = store.known_ids

    def counted() -> set[str]:
        nonlocal calls
        calls += 1
        return known()

    monkeypatch.setattr(store, "known_ids", counted)

    with httpx.Client() as client:
        _pull_nav(client, store)

    assert calls == 1


def test_should_fetch_repeated_nav_uuid_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    nav_entry: dict[str, object],
) -> None:
    store = Store(tmp_path / "ads.db")
    entries = [
        ({"_feed_entry": {"uuid": "u1", "status": "ACTIVE"}}, "page-1"),
        ({"_feed_entry": {"uuid": "u1", "status": "ACTIVE"}}, "page-1"),
    ]
    fetched: list[str] = []

    def fetch(client: httpx.Client, token: str, uuid: str) -> dict[str, object]:
        fetched.append(uuid)
        return {**nav_entry["ad_content"], "title": "Utvikler"}

    monkeypatch.setattr(nav, "fetch_public_token", lambda client: "token")
    monkeypatch.setattr(
        nav, "iter_new_entries", lambda client, token, start: iter(entries)
    )
    monkeypatch.setattr(nav, "fetch_entry", fetch)

    with httpx.Client() as client:
        _pull_nav(client, store)

    assert fetched == ["u1"]


def test_should_run_nightly_requests_through_host_rules(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    built: list[object] = []
    hooked: list[int] = []

    class RecordingLimiter(rate_limit.HostLimiter):
        def __init__(self, rules: object, default: rate_limit.HostRule) -> None:
            built.append(rules)
            super().__init__({}, default)

    def recent(client: httpx.Client, known: set[str], since: str) -> object:
        hooked.append(len(client.event_hooks["request"]))
        return finn.FinnRecentResult([], frozenset(), None)

    monkeypatch.setattr(rate_limit, "HostLimiter", RecordingLimiter)
    monkeypatch.setattr(finn, "fetch_recent_ads", recent)
    _stub_finn(monkeypatch, [], frozenset())
    _stub_other_sources(monkeypatch)

    run(replace(_settings(tmp_path), report_all=False))

    assert built == [rate_limit.HOST_RULES]
    assert hooked == [1]
