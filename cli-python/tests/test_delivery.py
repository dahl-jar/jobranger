from __future__ import annotations

import json
import logging
import sqlite3
import time
from contextlib import contextmanager
from dataclasses import replace
from datetime import UTC, datetime, timedelta, tzinfo
from typing import TYPE_CHECKING

import httpx
import keyring
import pytest
from factories import (
    FIXTURES,
    FakeProvider,
    ListSink,
    MemoryKeyring,
    make_ad,
    make_match,
    targets,
)
from mail_fakes import (
    ADDRESS,
    RESEND_EMAIL,
    RESEND_KEY,
    SMTP_EMAIL,
    SMTP_PASSWORD,
    FakeSMTP,
    ResendRecorder,
)

from jobranger import pipeline
from jobranger.delivery import Delivered, deliver
from jobranger.events import CancelToken, RunMode
from jobranger.logging_setup import FIELDS_ATTR, JsonFormatter
from jobranger.matching.classify import StopReason
from jobranger.matching.guidance import DEFAULT_TEXT
from jobranger.matching.profile import load_profile
from jobranger.matching.prompt import assessment_version, build_system
from jobranger.matching.rules import limit_rules
from jobranger.pipeline import DIGEST_CARRY_DAYS, RunResult, run_pass
from jobranger.settings import (
    AppConfig,
    EmailConfig,
    Limits,
    NotifyConfig,
    ProfileRef,
    SourcesConfig,
)
from jobranger.site import SITE_URL
from jobranger.sources.base import Source, SourceBatch
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

    from jobranger.ads.models import Ad
    from jobranger.sources.base import FetchContext

NOW = datetime(2026, 10, 9, 3, 0, tzinfo=UTC)
BEFORE_NOW = "2026-10-09T00:00:00+00:00"
CONFIG = AppConfig(
    profile=ProfileRef(path=FIXTURES / "profile_user.yaml"),
    targets=targets(),
    notify=NotifyConfig(website=False, email=RESEND_EMAIL),
)
VERSION = assessment_version(
    build_system(
        DEFAULT_TEXT,
        limit_rules(CONFIG.targets.limits),
        load_profile(FIXTURES / "profile_user.yaml"),
    )
)


@pytest.fixture(autouse=True)
def secrets(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Iterator[None]:
    monkeypatch.setenv("JOBRANGER_CONFIG_DIR", str(tmp_path / "config"))
    backend = MemoryKeyring()
    backend.set_password("jobranger", "resend_api_key", RESEND_KEY)
    backend.set_password("jobranger", "smtp_password", SMTP_PASSWORD)
    with _use_keyring(backend):
        yield


@pytest.fixture(autouse=True)
def oslo_time(monkeypatch: pytest.MonkeyPatch) -> Iterator[None]:
    monkeypatch.setenv("TZ", "Europe/Oslo")
    time.tzset()
    yield
    monkeypatch.undo()
    time.tzset()


@contextmanager
def _use_keyring(backend: MemoryKeyring) -> Iterator[None]:
    previous = keyring.get_keyring()
    keyring.set_keyring(backend)
    try:
        yield
    finally:
        keyring.set_keyring(previous)


def _ad(**overrides: object) -> Ad:
    return make_ad(match=make_match(version=VERSION), **overrides)


def _store(tmp_path: Path, *ads: Ad) -> Store:
    store = Store(tmp_path / "ads.db")
    for ad in ads:
        store.upsert_ad(ad)
    return store


def _result(*ads: Ad, run_id: int = 1, **overrides: object) -> RunResult:
    result = RunResult(
        run_id=run_id,
        status="completed",
        mode=RunMode.scheduled,
        new_ads=len(ads),
        reported_ids=tuple(ad.id for ad in ads),
        sources_read=("finn",),
        version=VERSION,
    )
    return replace(result, **overrides)


def _config(website: bool = False, email: EmailConfig = RESEND_EMAIL) -> AppConfig:
    return CONFIG.model_copy(
        update={"notify": NotifyConfig(website=website, email=email)}
    )


def _reported(store: Store) -> list[str]:
    return [ad.id for ad in store.reported_ads(BEFORE_NOW)]


def test_should_mark_ads_reported_after_email_is_sent(tmp_path: Path) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)

    delivered = deliver(_config(), store, _result(ad), ResendRecorder().client(), NOW)

    assert (delivered, _reported(store)) == (Delivered(email_to=ADDRESS), [ad.id])


def test_should_keep_ads_unreported_when_email_fails(tmp_path: Path) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)
    recorder = ResendRecorder(500, {"message": "internal"})

    delivered = deliver(_config(), store, _result(ad), recorder.client(), NOW)

    assert (delivered, _reported(store)) == (
        Delivered(email_error="Resend answered 500."),
        [],
    )


def test_should_mark_ads_at_once_when_email_is_off(tmp_path: Path) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)
    recorder = ResendRecorder()
    config = _config(website=True, email=EmailConfig())

    delivered = deliver(config, store, _result(ad), recorder.client(), NOW)

    assert (delivered, _reported(store), recorder.requests) == (
        Delivered(),
        [ad.id],
        [],
    )


def test_should_skip_email_when_run_has_no_news(tmp_path: Path) -> None:
    recorder = ResendRecorder()

    delivered = deliver(_config(), _store(tmp_path), _result(), recorder.client(), NOW)

    assert (delivered, recorder.requests) == (Delivered(), [])


@pytest.mark.parametrize(
    "notice",
    [{"failed_sources": ("nav",)}, {"budget_paused": True, "pending": 4}],
    ids=["failed source", "budget paused"],
)
def test_should_email_notice_without_matches(
    tmp_path: Path, notice: dict[str, object]
) -> None:
    recorder = ResendRecorder()

    deliver(_config(), _store(tmp_path), _result(**notice), recorder.client(), NOW)

    assert len(recorder.requests) == 1


@pytest.mark.parametrize(
    ("website", "linked"), [(True, True), (False, False)], ids=["on", "off"]
)
def test_should_link_email_to_site_only_when_website_is_on(
    tmp_path: Path, website: bool, linked: bool
) -> None:
    ad = _ad()
    recorder = ResendRecorder()

    deliver(_config(website), _store(tmp_path, ad), _result(ad), recorder.client(), NOW)

    assert (f"{SITE_URL}/app" in str(recorder.sent()["html"])) is linked


def test_should_reuse_idempotency_key_when_same_run_is_delivered_again(
    tmp_path: Path,
) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)
    recorder = ResendRecorder(500, {"message": "internal"})

    for _ in range(2):
        deliver(_config(), store, _result(ad), recorder.client(), NOW)

    first, second = (
        request.headers["Idempotency-Key"] for request in recorder.requests
    )
    assert first == second


def test_should_change_idempotency_key_when_same_run_sends_other_cards(
    tmp_path: Path,
) -> None:
    first = _ad()
    second = _ad(id="finn:2", source_id="2", company="Org Two AS")
    store = _store(tmp_path, first, second)
    recorder = ResendRecorder(500, {"message": "internal"})

    for result in (_result(first), _result(first, second)):
        deliver(_config(), store, result, recorder.client(), NOW)

    first_key, second_key = (
        request.headers["Idempotency-Key"] for request in recorder.requests
    )
    assert first_key != second_key


def test_should_change_idempotency_key_for_another_run(tmp_path: Path) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)
    recorder = ResendRecorder(500, {"message": "internal"})

    for run_id in (1, 2):
        deliver(_config(), store, _result(ad, run_id=run_id), recorder.client(), NOW)

    first, second = (
        request.headers["Idempotency-Key"] for request in recorder.requests
    )
    assert first != second


def test_should_send_through_smtp_when_via_is_smtp(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    server = FakeSMTP().install(monkeypatch)
    ad = _ad()

    deliver(
        _config(email=SMTP_EMAIL),
        _store(tmp_path, ad),
        _result(ad),
        ResendRecorder().client(),
        NOW,
    )

    assert (server.messages[0]["To"], server.logins) == (
        ADDRESS,
        [(ADDRESS, SMTP_PASSWORD)],
    )


def test_should_log_email_failure_type_without_reason(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    ad = _ad()

    def refuse(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection refused", request=request)

    client = httpx.Client(transport=httpx.MockTransport(refuse))

    with caplog.at_level(logging.INFO, logger="jobranger.delivery"):
        deliver(_config(), _store(tmp_path, ad), _result(ad), client, NOW)

    assert [
        (record.msg, getattr(record, FIELDS_ATTR))
        for record in caplog.records
        if record.name == "jobranger.delivery"
    ] == [
        (
            "email.failed",
            {"error_type": "ConnectError", "failure": "resend_unreachable"},
        )
    ]


def test_should_date_digest_by_local_day(tmp_path: Path) -> None:
    late_evening_utc = datetime(2026, 10, 8, 22, 30, tzinfo=UTC)
    ad = _ad(deadline="2026-10-09")
    recorder = ResendRecorder()

    deliver(
        _config(),
        _store(tmp_path, ad),
        _result(ad),
        recorder.client(),
        late_evening_utc,
    )

    assert "9 Oct, today" in str(recorder.sent()["text"])


def test_should_store_report_time_in_utc(tmp_path: Path) -> None:
    ad = _ad()
    store = _store(tmp_path, ad)

    deliver(_config(), store, _result(ad), ResendRecorder().client(), NOW)

    with sqlite3.connect(tmp_path / "ads.db") as db:
        stored = db.execute("SELECT reported_at FROM ads").fetchall()
    assert stored == [("2026-10-09T03:00:00+00:00",)]


def test_should_log_sent_email_with_card_count(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    soon = _ad(id="finn:1", deadline="2026-10-10")
    later = _ad(
        id="finn:2",
        source_id="2",
        company="Org Two AS",
        url="https://jobs.example.test/ad/2",
    )
    store = _store(tmp_path, soon, later)

    with caplog.at_level(logging.INFO, logger="jobranger.delivery"):
        deliver(_config(), store, _result(soon, later), ResendRecorder().client(), NOW)

    assert [
        (record.msg, getattr(record, FIELDS_ATTR))
        for record in caplog.records
        if record.name == "jobranger.delivery"
    ] == [("email.sent", {"count": 2})]


def _posting(index: int) -> Ad:
    return make_ad(
        id=f"fake:{index}",
        source="fake",
        source_id=str(index),
        title=f"Utvikler {index}",
        company=f"Org {index} AS",
        url=f"https://jobs.example.test/{index}",
        description="Build Java services.",
        match=None,
        is_programming_job=None,
        display_title=None,
        role_type=None,
        published=None,
    )


def _pass(
    monkeypatch: pytest.MonkeyPatch, store: Store, config: AppConfig, ads: list[Ad]
) -> RunResult:
    def fetch(ctx: FetchContext) -> SourceBatch:
        return SourceBatch(source="fake", ads=tuple(ads), read=len(ads))

    monkeypatch.setattr(pipeline, "enabled_sources", lambda _: [Source("fake", fetch)])
    offline = httpx.Client(transport=httpx.MockTransport(lambda _: httpx.Response(404)))
    return run_pass(
        config,
        store,
        ListSink(),
        CancelToken(),
        mode=RunMode.scheduled,
        provider=FakeProvider(),
        http=offline,
    )


def test_should_send_ads_from_failed_email_with_next_run(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setenv("JOBRANGER_STATE_DIR", str(tmp_path / "state"))
    config = _config().model_copy(
        update={"sources": SourcesConfig(nav=False, employers=False)}
    )
    store = _store(tmp_path)
    first, second = _posting(1), _posting(2)
    failing, working = ResendRecorder(500, {"message": "internal"}), ResendRecorder()
    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, [first]),
        failing.client(),
        datetime.now(tz=UTC),
    )

    delivered = deliver(
        config,
        store,
        _pass(monkeypatch, store, config, [first, second]),
        working.client(),
        datetime.now(tz=UTC),
    )

    text = str(working.sent()["text"])
    new_today, waiting = text.index("NEW TODAY"), text.index("STILL WAITING")
    assert (
        delivered.email_to,
        new_today < text.index("Org 2 AS") < waiting < text.index("Org 1 AS"),
        sorted(ad.id for ad in store.reported_ads("2000-01-01")),
    ) == (ADDRESS, True, [first.id, second.id])


def test_should_log_failure_category_and_status(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    ad = _ad()
    recorder = ResendRecorder(422, {"message": "Invalid `to` field LEAK"})

    with caplog.at_level(logging.INFO, logger="jobranger.delivery"):
        deliver(_config(), _store(tmp_path, ad), _result(ad), recorder.client(), NOW)

    [line] = [
        JsonFormatter().format(record)
        for record in caplog.records
        if record.name == "jobranger.delivery"
    ]
    entry = json.loads(line)
    assert (
        {key: entry.get(key) for key in ("failure", "status", "error_type")},
        "LEAK" in line,
    ) == (
        {"failure": "resend_rejected", "status": 422, "error_type": "EmailError"},
        False,
    )


def _backdate(tmp_path: Path, ad_id: str, days: int) -> None:
    seen = (datetime.now(tz=UTC) - timedelta(days=days)).isoformat()
    with sqlite3.connect(tmp_path / "ads.db") as db:
        db.execute("UPDATE ads SET first_seen = ? WHERE id = ?", (seen, ad_id))


def _sources_off() -> AppConfig:
    return _config().model_copy(
        update={"sources": SourcesConfig(nav=False, employers=False)}
    )


@pytest.fixture
def run_state(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("JOBRANGER_STATE_DIR", str(tmp_path / "state"))


@pytest.mark.usefixtures("run_state")
def test_should_mark_ad_without_email_when_first_seen_is_past_carry_window(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    old = _ad(id="fake:9", source_id="9", deadline=None)
    store.upsert_ad(old)
    _backdate(tmp_path, old.id, 30)
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, []),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert (recorder.requests, [ad.id for ad in store.reported_ads("2000")]) == (
        [],
        [old.id],
    )


@pytest.mark.usefixtures("run_state")
def test_should_mark_twin_of_earlier_report_without_email(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    earlier = _ad(id="fake:1", source_id="1")
    store.upsert_ad(earlier)
    store.mark_reported([earlier.id], datetime.now(tz=UTC).isoformat())
    twin = _ad(id="other:1", source="other", source_id="1")
    store.upsert_ad(twin)
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, []),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert (recorder.requests, sorted(ad.id for ad in store.reported_ads("2000"))) == (
        [],
        sorted([earlier.id, twin.id]),
    )


@pytest.mark.usefixtures("run_state")
def test_should_mark_stale_ad_without_email(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    stale = _ad(published="2020-01-01T00:00:00+00:00")
    store.upsert_ad(stale)
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, []),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert (recorder.requests, [ad.id for ad in store.reported_ads("2000")]) == (
        [],
        [stale.id],
    )


def test_should_mark_only_ads_shown_in_email(tmp_path: Path) -> None:
    shown = _ad()
    twin = _ad(id="other:1234", source="other")
    store = _store(tmp_path, shown, twin)

    deliver(_config(), store, _result(shown, twin), ResendRecorder().client(), NOW)

    assert len(_reported(store)) == 1


def test_should_mark_silent_ads_when_email_fails(tmp_path: Path) -> None:
    ad, silent = _ad(), _ad(id="fake:2", source_id="2")
    store = _store(tmp_path, ad, silent)
    recorder = ResendRecorder(500, {"message": "internal"})

    deliver(
        _config(),
        store,
        _result(ad, silent_ids=(silent.id,)),
        recorder.client(),
        NOW,
    )

    assert _reported(store) == [silent.id]


@pytest.mark.usefixtures("run_state")
def test_should_email_backlog_matches_classified_today(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    backlog = [_posting(1), _posting(2)]
    for ad in backlog:
        store.upsert_ad(ad)
        _backdate(tmp_path, ad.id, 8)
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, backlog),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    text = str(recorder.sent()["text"])
    assert ("Org 1 AS" in text, "Org 2 AS" in text, "STILL WAITING" in text) == (
        True,
        True,
        False,
    )


@pytest.mark.usefixtures("run_state")
def test_should_email_month_old_ad_once_when_user_asks_to_see_it(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    old = _ad(id="fake:9", source_id="9", deadline=None)
    store.upsert_ad(old)
    _backdate(tmp_path, old.id, 30)
    store.mark_ad(old.id, "show_anyway", datetime.now(tz=UTC).isoformat())
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, []),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert (len(recorder.requests), _reported(store)) == (1, [old.id])


@pytest.mark.parametrize(
    ("stopped", "pending", "sent"),
    [
        (StopReason.auth, 3, 1),
        (StopReason.provider_down, 3, 1),
        (StopReason.batch_open, 3, 0),
        (StopReason.auth, 0, 0),
    ],
    ids=["auth", "provider down", "batch open", "nothing waiting"],
)
def test_should_email_notice_when_model_stops_on_provider_error(
    tmp_path: Path, stopped: StopReason, pending: int, sent: int
) -> None:
    recorder = ResendRecorder()
    result = _result(model_stopped=stopped, pending=pending)

    deliver(_config(), _store(tmp_path), result, recorder.client(), NOW)

    assert len(recorder.requests) == sent


@pytest.mark.usefixtures("run_state")
def test_should_email_stored_match_first_seen_today(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path, _ad())
    recorder = ResendRecorder()

    deliver(
        config,
        store,
        _pass(monkeypatch, store, config, []),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert len(recorder.requests) == 1


def _backdate_classification(tmp_path: Path, ad_id: str, days: int) -> None:
    then = (datetime.now(tz=UTC) - timedelta(days=days)).isoformat()
    with sqlite3.connect(tmp_path / "ads.db") as db:
        db.execute(
            "UPDATE ads SET first_seen = ?, first_classified_at = ? WHERE id = ?",
            (then, then, ad_id),
        )


@pytest.mark.usefixtures("run_state")
def test_should_keep_old_ad_silent_when_limits_change_reclassifies_it(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _sources_off()
    store = _store(tmp_path)
    old = _posting(1)
    _pass(monkeypatch, store, config, [old])
    _backdate_classification(tmp_path, old.id, 30)
    widened = config.model_copy(
        update={
            "targets": targets(limits=Limits(hide_masters=False)),
        }
    )
    recorder = ResendRecorder()

    deliver(
        widened,
        store,
        _pass(monkeypatch, store, widened, [old]),
        recorder.client(),
        datetime.now(tz=UTC),
    )

    assert (recorder.requests, _reported(store)) == ([], [old.id])


class _FrozenDatetime(datetime):
    @classmethod
    def now(cls, tz: tzinfo | None = None) -> _FrozenDatetime:
        return cls.fromisoformat(NOW.isoformat())


def _set_matched_at(tmp_path: Path, ad_id: str, at: datetime) -> None:
    with sqlite3.connect(tmp_path / "ads.db") as db:
        db.execute(
            "UPDATE ads SET first_seen = ?, first_classified_at = ? WHERE id = ?",
            (at.isoformat(), at.isoformat(), ad_id),
        )


@pytest.mark.usefixtures("run_state")
@pytest.mark.parametrize(
    ("matched_at", "carried"),
    [(NOW - timedelta(days=DIGEST_CARRY_DAYS), True), (NOW, False)],
    ids=["carry window start", "run start"],
)
def test_should_place_ad_matched_at_window_edge(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, matched_at: datetime, carried: bool
) -> None:
    monkeypatch.setattr(pipeline, "datetime", _FrozenDatetime)
    config = _sources_off()
    ad = _ad(deadline=None)
    store = _store(tmp_path, ad)
    _set_matched_at(tmp_path, ad.id, matched_at)
    recorder = ResendRecorder()

    deliver(
        config, store, _pass(monkeypatch, store, config, []), recorder.client(), NOW
    )

    text = str(recorder.sent()["text"])
    assert (ad.company in text, "STILL WAITING" in text) == (True, carried)
