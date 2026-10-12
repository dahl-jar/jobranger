from __future__ import annotations

import fcntl
import os
import threading
from datetime import UTC, datetime, timedelta
from typing import TYPE_CHECKING

import httpx
import pytest
from factories import (
    CALL_USAGE,
    FIXTURES,
    OTHER_TEAMTAILOR_FEED,
    OUTDATED_VERSION,
    FakeClock,
    FakeProvider,
    ListSink,
    make_ad,
    make_assessment,
    make_match,
    make_search_doc,
    make_search_html,
    targets,
)

from jobranger import paths, pipeline
from jobranger.catalogue import EmployerFeed
from jobranger.events import CancelToken, RunEvent, RunMode
from jobranger.matching.classify import StopReason
from jobranger.matching.profile import load_profile, profile_hash
from jobranger.matching.rules import build_rules
from jobranger.net.rate_limit import HostLimiter, HostRule, event_hooks
from jobranger.pipeline import RunResult, run_pass
from jobranger.providers.base import ProviderError, ProviderErrorKind, StopKind
from jobranger.settings import (
    AppConfig,
    ConfigMissingError,
    Limits,
    ProfileRef,
    SourcesConfig,
)
from jobranger.sources.base import (
    Source,
    SourceBatch,
    SourceCancelled,
    SourceError,
)
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from collections.abc import Callable
    from pathlib import Path

    from jobranger.ads.models import Ad
    from jobranger.sources.base import FetchContext

NO_SOURCES = SourcesConfig(nav=False, employers=False, finn=False, linkedin=False)
CONFIG = AppConfig(
    profile=ProfileRef(path=FIXTURES / "profile_user.yaml"),
    targets=targets(),
    sources=NO_SOURCES,
)
FINN_ONLY_CONFIG = CONFIG.model_copy(
    update={"sources": SourcesConfig(finn=True, nav=False, employers=False)}
)
VERSION = profile_hash(
    load_profile(FIXTURES / "profile_user.yaml"), build_rules(CONFIG.targets.limits)
)
BARRIER_TIMEOUT_SECONDS = 2.0
FEED_HOST_INTERVAL = 2.0
TEAMTAILOR_XML = (FIXTURES / "teamtailor_jobs.xml").read_text()


@pytest.fixture(autouse=True)
def state_dir(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    path = tmp_path / "state"
    monkeypatch.setenv("JOBRANGER_STATE_DIR", str(path))
    return path


@pytest.fixture
def store(tmp_path: Path) -> Store:
    return Store(tmp_path / "ads.db")


def _offline_client() -> httpx.Client:
    return httpx.Client(
        transport=httpx.MockTransport(lambda request: httpx.Response(404))
    )


def _run(store: Store, config: AppConfig = CONFIG, **overrides: object) -> RunResult:
    options: dict[str, object] = {
        "events": ListSink(),
        "cancel": CancelToken(),
        "provider": FakeProvider(),
        "http": _offline_client(),
        "mode": RunMode.manual,
        **overrides,
    }
    return run_pass(
        config,
        store,
        options["events"],
        options["cancel"],
        mode=options["mode"],
        provider=options["provider"],
        http=options["http"],
    )


def _new_ad(index: int, **overrides: object) -> Ad:
    base: dict[str, object] = {
        "id": f"fake:{index}",
        "source": "fake",
        "source_id": str(index),
        "title": f"Utvikler {index}",
        "company": f"Org {index} AS",
        "url": f"https://jobs.example.test/{index}",
        "description": "Build Java services.",
        "match": None,
        "is_programming_job": None,
        "display_title": None,
        "role_type": None,
        "published": None,
    }
    base.update(overrides)
    return make_ad(**base)


def _source(
    name: str, ads: list[Ad], before: Callable[[], None] | None = None
) -> Source:
    def fetch(ctx: FetchContext) -> SourceBatch:
        if before is not None:
            before()
        return SourceBatch(source=name, ads=tuple(ads), read=len(ads))

    return Source(name, fetch)


def _failing(name: str) -> Source:
    def fetch(ctx: FetchContext) -> SourceBatch:
        raise SourceError(f"{name} failed")

    return Source(name, fetch)


def _use_sources(monkeypatch: pytest.MonkeyPatch, sources: list[Source]) -> None:
    monkeypatch.setattr(pipeline, "enabled_sources", lambda config: sources)


def _stages(sink: ListSink, stage: str) -> list[RunEvent]:
    return [event for event in sink.events if event.stage == stage]


def test_should_skip_run_when_lock_is_held(store: Store) -> None:
    lock_path = paths.run_lock_path()
    lock_path.parent.mkdir(parents=True)
    descriptor = os.open(lock_path, os.O_RDWR | os.O_CREAT)
    fcntl.flock(descriptor, fcntl.LOCK_EX)
    try:
        result = _run(store, mode=RunMode.scheduled)
    finally:
        os.close(descriptor)

    assert (result.status, result.mode, store.last_run()) == (
        "locked",
        RunMode.scheduled,
        None,
    )


def test_should_emit_source_done_event_with_new_count(
    store: Store, monkeypatch: pytest.MonkeyPatch, sink: ListSink
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    _run(store, events=sink)

    assert _stages(sink, "source_done") == [
        RunEvent("source_done", source="fake", found_new=2)
    ]


def test_should_record_failed_source_in_run_row(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_failing("bad"), _source("fake", [_new_ad(1)])])

    result = _run(store)

    last = store.last_run()
    assert last is not None
    assert (result.status, last.failed_sources) == ("completed", ("bad",))


def test_should_stop_before_model_stage_when_cancelled(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    cancel = CancelToken()

    class CancellingSink(ListSink):
        def emit(self, event: RunEvent) -> None:
            super().emit(event)
            if event.stage == "source_done":
                cancel.cancel()

    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])
    provider = FakeProvider()

    result = _run(store, provider=provider, events=CancellingSink(), cancel=cancel)

    last = store.last_run()
    assert last is not None
    assert (result.status, last.status, provider.prompts) == (
        "cancelled",
        "cancelled",
        [],
    )


def test_should_emit_model_progress_per_ad(
    store: Store, monkeypatch: pytest.MonkeyPatch, sink: ListSink
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    _run(store, events=sink)

    assert [(event.checked, event.total) for event in _stages(sink, "model")] == [
        (1, 2),
        (2, 2),
    ]


def test_should_write_usage_per_provider_call(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])
    provider = FakeProvider([make_assessment(), RuntimeError("provider crashed")])

    with pytest.raises(RuntimeError):
        _run(store, provider=provider)

    assert [day.calls for day in store.usage_days("2000-01-01")] == [1]


def test_should_return_reported_ids_without_marking_them_reported(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])

    result = _run(store)

    assert (result.reported_ids, store.reported_ads("2000-01-01")) == (
        ("fake:1",),
        [],
    )


def test_should_skip_model_step_when_provider_is_none(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    result = run_pass(
        CONFIG,
        store,
        ListSink(),
        CancelToken(),
        mode=RunMode.manual,
        provider=None,
        http=_offline_client(),
    )

    assert (result.model_stopped, result.pending) == (StopReason.auth, 2)


def test_should_reclassify_after_limit_change(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])
    provider = FakeProvider()
    _run(store, provider=provider)
    wider = CONFIG.model_copy(
        update={"targets": targets(limits=Limits(years=frozenset({"0-1", "1-3"})))}
    )

    _run(store, wider, provider=provider)

    assert len(provider.prompts) == 2


def test_should_close_run_row_with_counts(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    _run(store)

    last = store.last_run()
    assert last is not None
    assert (last.status, last.ads_read, last.new_ads, last.matches) == (
        "completed",
        2,
        2,
        2,
    )


def test_should_release_lock_after_failed_run(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])
    with pytest.raises(RuntimeError):
        _run(store, provider=FakeProvider([RuntimeError("provider crashed")]))

    assert _run(store).status == "completed"


def test_should_mark_run_failed_when_profile_is_missing(
    store: Store, tmp_path: Path
) -> None:
    missing = CONFIG.model_copy(
        update={"profile": ProfileRef(path=tmp_path / "absent.yaml")}
    )

    with pytest.raises(ConfigMissingError):
        _run(store, missing)

    last = store.last_run()
    assert last is not None
    assert last.status == "failed"


def test_should_fetch_sources_concurrently(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    barrier = threading.Barrier(2, timeout=BARRIER_TIMEOUT_SECONDS)

    def meet() -> None:
        barrier.wait()

    _use_sources(
        monkeypatch,
        [_source("one", [_new_ad(1)], meet), _source("two", [_new_ad(2)], meet)],
    )

    result = _run(store)

    assert result.failed_sources == ()


def test_should_keep_requests_to_one_host_sequential(store: Store) -> None:
    clock = FakeClock()
    clock.sleep = clock.sleeps.append
    limiter = HostLimiter(
        {},
        HostRule(rate=1 / FEED_HOST_INTERVAL, burst=1),
        clock=clock,
        sleep=clock.sleep,
    )
    http = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(200, text=TEAMTAILOR_XML)
        ),
        event_hooks=event_hooks(limiter),
    )
    feeds = tuple(
        EmployerFeed(
            kind="teamtailor",
            key=key,
            company="Org One AS",
            board=f"https://careers.example.test/{key}.rss",
        )
        for key in ("one", "two")
    )
    config = CONFIG.model_copy(
        update={"sources": SourcesConfig(nav=False, employer_feeds=feeds)}
    )

    _run(store, config, http=http)

    assert clock.sleeps == [pytest.approx(FEED_HOST_INTERVAL)]


def test_should_write_all_ads_through_single_writer(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    writers: set[int] = set()

    class RecordingStore(Store):
        def upsert_ad(self, ad: Ad) -> bool:
            writers.add(threading.get_ident())
            return super().upsert_ad(ad)

    store = RecordingStore(tmp_path / "ads.db")
    _use_sources(
        monkeypatch,
        [_source(name, [_new_ad(index)]) for index, name in enumerate("abc")],
    )

    _run(store)

    assert (writers, len(store.known_ids())) == ({threading.get_ident()}, 3)


def test_should_isolate_failure_of_one_source(
    store: Store, monkeypatch: pytest.MonkeyPatch, sink: ListSink
) -> None:
    _use_sources(
        monkeypatch,
        [
            _source("one", [_new_ad(1)]),
            _failing("bad"),
            _source("two", [_new_ad(2)]),
        ],
    )

    result = _run(store, events=sink)

    assert (
        result.failed_sources,
        [event.source for event in _stages(sink, "source_failed")],
        sorted(store.known_ids()),
    ) == (("bad",), ["bad"], ["fake:1", "fake:2"])


def test_should_emit_source_done_in_finish_order(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    fast_done = threading.Event()

    class SignallingSink(ListSink):
        def emit(self, event: RunEvent) -> None:
            super().emit(event)
            if event == RunEvent("source_done", source="fast", found_new=1):
                fast_done.set()

    def wait_for_fast() -> None:
        if not fast_done.wait(BARRIER_TIMEOUT_SECONDS):
            raise SourceError("fast source was not written first")

    sink = SignallingSink()
    _use_sources(
        monkeypatch,
        [
            _source("slow", [_new_ad(1)], wait_for_fast),
            _source("fast", [_new_ad(2)]),
        ],
    )

    _run(store, events=sink)

    assert [event.source for event in _stages(sink, "source_done")] == [
        "fast",
        "slow",
    ]


def test_should_stop_workers_between_requests_when_cancelled(store: Store) -> None:
    cancel = CancelToken()
    requested: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requested.append(request.url.path)
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        cancel.cancel()
        page = int(request.url.params.get("page", "1"))
        return httpx.Response(
            200,
            json={"items": [], "next_url": f"/api/v1/feed?page={page + 1}"},
        )

    config = CONFIG.model_copy(update={"sources": SourcesConfig(employers=False)})
    http = httpx.Client(transport=httpx.MockTransport(respond))

    result = _run(store, config, cancel=cancel, http=http)

    assert (result.status, len(requested)) == ("cancelled", 2)


def test_should_mark_malformed_apply_url_skipped(store: Store) -> None:
    broken = make_ad(
        apply_url="http://[::1",
        description="Build Java services.",
        match=make_match(version=VERSION),
    )
    store.upsert_ad(broken)

    result = _run(store)

    assert (result.status, store.active_ads()[0].apply_status) == (
        "completed",
        "skipped",
    )


def test_should_classify_duplicate_rows_once(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    finn_ad = _new_ad(
        1,
        id="finn:1",
        source="finn",
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services for customers and internal teams.",
    )
    direct = _new_ad(
        2,
        id="ashby:example-one:1",
        source="ashby:example-one",
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services.",
    )
    _use_sources(monkeypatch, [_source("fake", [finn_ad, direct])])
    provider = FakeProvider()

    _run(store, provider=provider)

    assert (len(provider.prompts), {ad.label for ad in store.active_ads()}) == (
        1,
        {"none"},
    )


def test_should_reuse_existing_duplicate_classification(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    classified = make_ad(
        id="finn:1",
        title="Backend Developer",
        label="junior",
        description="Build Python services.",
        match=make_match(version=VERSION),
    )
    store.upsert_ad(classified)
    pending = _new_ad(
        1,
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services.",
    )
    _use_sources(monkeypatch, [_source("fake", [pending])])
    provider = FakeProvider()

    _run(store, provider=provider)

    labels = {ad.id: ad.label for ad in store.active_ads()}
    assert (provider.prompts, labels[pending.id]) == ([], "junior")


def test_should_label_senior_sibling_of_freshly_classified_group(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    description = (
        "Build the product across the stack with TypeScript and Python, own "
        "features from design to production, and work closely with design."
    )
    plain = _new_ad(
        1, title="Fullstack Engineer", company="Org One AS", description=description
    )
    senior = _new_ad(
        2,
        title="Senior Fullstack Engineer",
        company="Org One AS",
        description=description,
    )
    _use_sources(monkeypatch, [_source("fake", [plain, senior])])

    _run(store)

    labels = {ad.id: ad.label for ad in store.active_ads()}
    assert labels[senior.id] == "senior"


def test_should_keep_attempts_for_ads_left_pending_by_outage(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])
    provider = FakeProvider(
        [make_assessment(), ProviderError(ProviderErrorKind.server, "down")]
    )

    result = _run(store, provider=provider)

    assert (
        result.model_stopped,
        result.pending,
        len(store.pending_classification(1)),
    ) == (
        StopReason.provider_down,
        1,
        1,
    )


@pytest.mark.usefixtures("no_retry_wait")
@pytest.mark.parametrize("succeeded", [True, False], ids=["feed read", "feed failed"])
def test_should_expire_import_missing_from_employer_feed(
    store: Store, succeeded: bool
) -> None:
    feed = OTHER_TEAMTAILOR_FEED
    dead_import = make_ad(
        id="finn:2",
        source_id="2",
        title="Data Engineer",
        company="Org Two AS",
        description="Build pipelines.",
        match=make_match(version=VERSION),
    )
    store.upsert_ad(dead_import)
    status = 200 if succeeded else 500
    http = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(status, text="<rss><channel/></rss>")
        )
    )
    config = CONFIG.model_copy(
        update={"sources": SourcesConfig(nav=False, employer_feeds=(feed,))}
    )

    _run(store, config, http=http)

    expected = "expired" if succeeded else None
    assert store.active_ads()[0].apply_status == expected


@pytest.mark.parametrize(
    "stored",
    [
        {"label": "junior", "match": make_match(version=OUTDATED_VERSION)},
        {"label": "error", "is_programming_job": False},
    ],
    ids=["stale match", "error label"],
)
def test_should_not_reuse_unusable_duplicate_classification(
    store: Store, monkeypatch: pytest.MonkeyPatch, stored: dict[str, object]
) -> None:
    existing = make_ad(
        id="finn:1",
        title="Backend Developer",
        description="Build Python services.",
        **stored,
    )
    store.upsert_ad(existing)
    pending = _new_ad(
        1,
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services.",
    )
    _use_sources(monkeypatch, [_source("fake", [pending])])
    provider = FakeProvider()

    _run(store, provider=provider)

    assert len(provider.prompts) == 1


def test_should_check_only_reportable_apply_links(store: Store) -> None:
    checked: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        checked.append(request.url.path)
        return httpx.Response(404)

    for index, label in ((1, "junior"), (2, "senior")):
        store.upsert_ad(
            make_ad(
                id=f"finn:{index}",
                source_id=str(index),
                label=label,
                description="Build Java services.",
                apply_url=f"http://93.184.215.14/apply/{index}",
                match=make_match(version=VERSION),
            )
        )

    _run(store, http=httpx.Client(transport=httpx.MockTransport(respond)))

    assert checked == ["/apply/1"]


def test_should_store_finn_page_text_and_apply_url(store: Store) -> None:
    apply_url = "https://jobs.example.test/apply/1"
    page = (FIXTURES / "finn_ad_imported.html").read_text() + (
        f'<script>\\"applicationUrl\\",\\"{apply_url}\\"</script>'
    )

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.startswith("/job/ad/"):
            return httpx.Response(200, text=page)
        docs = [] if "sort" in request.url.params else [make_search_doc(5)]
        return httpx.Response(200, text=make_search_html(docs, len(docs)))

    config = FINN_ONLY_CONFIG

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    [stored] = store.active_ads()
    assert (stored.apply_url, stored.description is not None) == (apply_url, True)


def test_should_finish_cancelled_when_cancelled_between_model_calls(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    cancel = CancelToken()

    class CancellingSink(ListSink):
        def emit(self, event: RunEvent) -> None:
            super().emit(event)
            if event.stage == "model":
                cancel.cancel()

    sink = CancellingSink()
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    result = _run(store, events=sink, cancel=cancel)

    assert (result.status, _stages(sink, "apply_checks")) == ("cancelled", [])


def test_should_close_run_row_with_run_facts(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)]), _failing("bad")])

    result = _run(store, mode=RunMode.dry_run)

    last = store.last_run()
    assert last is not None
    assert (
        result.mode,
        result.run_id,
        result.ads_read,
        result.new_ads,
        result.matches,
        result.hidden,
        result.usage,
        result.reported_ids,
        result.hidden_counts,
        result.sources_read,
        result.budget_paused,
        result.pending,
        result.model_stopped,
    ) == (
        RunMode.dry_run,
        last.id,
        1,
        1,
        1,
        0,
        CALL_USAGE,
        ("fake:1",),
        {},
        ("fake",),
        False,
        0,
        None,
    )
    assert last.finished_at is not None
    assert last.finished_at.endswith("+00:00")


def test_should_emit_stages_in_run_order(
    store: Store, monkeypatch: pytest.MonkeyPatch, sink: ListSink
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])

    _run(store, events=sink)

    assert sink.events == [
        RunEvent("source_started", source="fake"),
        RunEvent("source_done", source="fake", found_new=1),
        RunEvent("model", checked=1, total=1),
        RunEvent("apply_checks"),
        RunEvent("report"),
    ]


def test_should_count_hidden_new_ads_by_kind(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    ads = [_new_ad(1), _new_ad(2), _new_ad(3)]
    _use_sources(monkeypatch, [_source("fake", ads)])
    needs_years = make_assessment(
        match={**make_assessment().match.model_dump(), "required_experience_years": 3}
    )
    provider = FakeProvider([needs_years, needs_years, make_assessment()])

    result = _run(store, provider=provider)

    assert (result.hidden_counts, result.reported_ids) == ({"years": 2}, ("fake:3",))


def test_should_label_ad_error_after_three_failed_runs(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])

    for _ in range(3):
        _run(store, provider=FakeProvider([StopKind.invalid]))

    assert store.active_ads()[0].label == "error"


def test_should_skip_twin_of_ad_reported_earlier(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    earlier = make_ad(id="finn:1", company="Org One AS", title="Utvikler")
    store.upsert_ad(earlier)
    store.mark_reported([earlier.id], datetime.now(tz=UTC).isoformat())
    twin = _new_ad(1, company="IT, Org One", title="Utvikler")
    _use_sources(monkeypatch, [_source("fake", [twin])])

    result = _run(store)

    assert result.reported_ids == ()


def test_should_record_crashed_source_as_failed(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    def crash(ctx: FetchContext) -> SourceBatch:
        raise KeyError("boom")

    _use_sources(monkeypatch, [Source("crashy", crash), _source("fake", [_new_ad(1)])])

    result = _run(store)

    assert (result.failed_sources, sorted(store.known_ids())) == (
        ("crashy",),
        ["fake:1"],
    )


def test_should_write_nothing_for_cancelled_source(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    def cancelled(ctx: FetchContext) -> SourceBatch:
        raise SourceCancelled

    _use_sources(monkeypatch, [Source("stopped", cancelled)])

    result = _run(store)

    assert (result.status, result.failed_sources, store.known_ids()) == (
        "cancelled",
        (),
        frozenset(),
    )


def test_should_apply_nav_activity_and_cursor(store: Store) -> None:
    known = make_ad(id="nav:u1", source="nav", source_id="u1")
    store.upsert_ad(known)

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        return httpx.Response(
            200,
            json={
                "feed_url": "/api/v1/feed/page-1",
                "items": [{"_feed_entry": {"uuid": "u1", "status": "INACTIVE"}}],
            },
        )

    config = CONFIG.model_copy(update={"sources": SourcesConfig(employers=False)})

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    assert (store.active_ads(), store.cursors().get("nav_position")) == (
        [],
        "https://pam-stilling-feed.nav.no/api/v1/feed/page-1",
    )


def test_should_store_finn_page_outcomes(store: Store) -> None:
    docs = [
        make_search_doc(1, heading="Senior utvikler"),
        make_search_doc(2, heading="Utvikler"),
        make_search_doc(3, heading="Utvikler"),
    ]
    page = (FIXTURES / "finn_ad_imported.html").read_text()
    pages: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.startswith("/job/ad/"):
            pages.append(request.url.path)
        if request.url.path == "/job/ad/2":
            return httpx.Response(404)
        if request.url.path == "/job/ad/3":
            return httpx.Response(200, text=page)
        found = [] if "sort" in request.url.params else docs
        return httpx.Response(200, text=make_search_html(found, len(found)))

    config = FINN_ONLY_CONFIG

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    stored = {ad.id: ad for ad in store.active_ads()}
    assert (pages, stored["finn:2"].description, stored["finn:3"].apply_status) == (
        ["/job/ad/2", "/job/ad/3"],
        None,
        "skipped",
    )


def test_should_stop_fetching_finn_page_after_three_failures(store: Store) -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.startswith("/job/ad/"):
            return httpx.Response(404)
        found = [] if "sort" in request.url.params else [make_search_doc(2)]
        return httpx.Response(200, text=make_search_html(found, len(found)))

    config = FINN_ONLY_CONFIG
    http = httpx.Client(transport=httpx.MockTransport(respond))

    for _ in range(3):
        _run(store, config, http=http)

    assert store.pending_description(frozenset()) == []


def test_should_recheck_apply_link_only_after_a_week(store: Store) -> None:
    checked: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        checked.append(request.url.path)
        return httpx.Response(200)

    for index, days_ago in ((1, 3), (2, 8)):
        ad_id = f"finn:{index}"
        store.upsert_ad(
            make_ad(
                id=ad_id,
                source_id=str(index),
                description="Build Java services.",
                apply_url=f"http://93.184.215.14/apply/{index}",
                match=make_match(version=VERSION),
            )
        )
        checked_at = datetime.now(tz=UTC) - timedelta(days=days_ago)
        store.set_apply_status(ad_id, "active", checked_at.isoformat())

    _run(store, http=httpx.Client(transport=httpx.MockTransport(respond)))

    rechecked = {ad.id: ad.apply_checked_at for ad in store.active_ads()}
    assert checked == ["/apply/2"]
    assert rechecked["finn:2"] is not None
    assert rechecked["finn:2"] > rechecked["finn:1"]


def test_should_copy_reused_classification_fields(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    classified = make_ad(
        id="finn:1",
        title="Backend Developer",
        label="junior",
        description="Build Python services.",
        summary="Python backend.",
        display_title="Python Developer",
        role_type="backend",
        technologies=("Python",),
        requirements=("Bachelor",),
        match=make_match(version=VERSION),
    )
    store.upsert_ad(classified)
    pending = _new_ad(
        1,
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services.",
    )
    _use_sources(monkeypatch, [_source("fake", [pending])])

    _run(store)

    stored = {ad.id: ad for ad in store.active_ads()}[pending.id]
    assert (
        stored.summary,
        stored.display_title,
        stored.role_type,
        stored.technologies,
        stored.requirements,
        stored.match,
        stored.is_programming_job,
    ) == (
        "Python backend.",
        "Python Developer",
        "backend",
        ("Python",),
        ("Bachelor",),
        make_match(version=VERSION),
        True,
    )


@pytest.mark.usefixtures("no_retry_wait")
def test_should_expire_only_same_company_import_missing_from_feed(
    store: Store,
) -> None:
    feed = OTHER_TEAMTAILOR_FEED
    for index, company in ((1, "Org Two AS"), (2, "Org One AS")):
        store.upsert_ad(
            make_ad(
                id=f"nav:{index}",
                source="nav",
                source_id=str(index),
                title=f"Data Engineer {index}",
                company=company,
                description="Build pipelines.",
                match=make_match(version=VERSION),
            )
        )
    http = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(200, text="<rss><channel/></rss>")
        )
    )
    config = CONFIG.model_copy(
        update={"sources": SourcesConfig(nav=False, employer_feeds=(feed,))}
    )

    _run(store, config, http=http)

    statuses = {ad.id: ad.apply_status for ad in store.active_ads()}
    assert statuses == {"nav:1": "expired", "nav:2": None}


def test_should_record_ads_read_from_all_sources(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(
        monkeypatch,
        [_source("one", [_new_ad(1)]), _source("two", [_new_ad(2), _new_ad(3)])],
    )

    result = _run(store)

    [day] = store.usage_days("2000-01-01")
    assert (result.ads_read, result.new_ads, day.ads_read) == (3, 3, 3)


def test_should_count_hidden_ads_listed_after_reported_one(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    needs_years = make_assessment(
        match={**make_assessment().match.model_dump(), "required_experience_years": 3}
    )
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1), _new_ad(2)])])

    result = _run(store, provider=FakeProvider([make_assessment(), needs_years]))

    assert (result.hidden_counts, result.hidden) == ({"years": 1}, 1)


def test_should_not_count_ad_covered_by_earlier_report_as_hidden(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    earlier = make_ad(id="finn:1", company="Org One AS", title="Utvikler")
    store.upsert_ad(earlier)
    store.mark_reported([earlier.id], datetime.now(tz=UTC).isoformat())
    _use_sources(
        monkeypatch,
        [_source("fake", [_new_ad(1, company="IT, Org One", title="Utvikler")])],
    )

    result = _run(store)

    assert (result.reported_ids, result.hidden_counts) == ((), {})


def test_should_not_count_unchecked_ads_as_hidden(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])

    result = _run(store, provider=None)

    assert (result.hidden_counts, result.hidden) == ({}, 0)


def test_should_not_report_ad_published_over_ninety_days_ago(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    published = (datetime.now(tz=UTC) - timedelta(days=100)).isoformat()
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1, published=published)])])

    result = _run(store)

    assert (result.reported_ids, result.matches) == ((), 0)


def test_should_classify_each_ad_once_across_runs(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_sources(monkeypatch, [_source("fake", [_new_ad(1)])])
    provider = FakeProvider()

    _run(store, provider=provider)
    _run(store, provider=provider)

    assert len(provider.prompts) == 1


def test_should_keep_reused_classification_current_across_runs(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    store.upsert_ad(
        make_ad(
            id="finn:1",
            title="Backend Developer",
            label="junior",
            description="Build Python services.",
            match=make_match(version=VERSION),
        )
    )
    pending = _new_ad(
        1,
        title="Backend Developer",
        company="Org One AS",
        description="Build Python services.",
    )
    _use_sources(monkeypatch, [_source("fake", [pending])])
    provider = FakeProvider()

    _run(store, provider=provider)
    _run(store, provider=provider)

    assert provider.prompts == []


def test_should_classify_pending_group_after_settled_group(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    store.upsert_ad(
        make_ad(
            id="aaa:1",
            source="aaa",
            label="junior",
            description="Settled.",
            match=make_match(version=VERSION),
        )
    )
    _use_sources(
        monkeypatch, [_source("fake", [_new_ad(9, title="Plattformutvikler")])]
    )
    provider = FakeProvider()

    _run(store, provider=provider)

    assert len(provider.prompts) == 1


def test_should_report_new_ad_with_future_deadline_only(
    store: Store, monkeypatch: pytest.MonkeyPatch
) -> None:
    store.upsert_ad(
        make_ad(
            id="finn:1",
            label="junior",
            description="x",
            match=make_match(version=VERSION),
        )
    )
    future = _new_ad(2, deadline="2099-01-01")
    _use_sources(monkeypatch, [_source("fake", [future])])

    assert _run(store).reported_ids == (future.id,)


def test_should_close_ad_whose_deadline_passed(store: Store) -> None:
    store.upsert_ad(make_ad(deadline="2026-01-01", description="x"))

    _run(store)

    [closed] = store.closed_ads(datetime.now(tz=UTC).date().isoformat())
    assert closed.why == "deadline"


def test_should_check_at_most_sixty_apply_links_per_run(store: Store) -> None:
    checked: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        checked.append(request.url.path)
        return httpx.Response(200)

    for index in range(61):
        store.upsert_ad(
            make_ad(
                id=f"finn:{index}",
                source_id=str(index),
                description="x",
                deadline="2099-01-01",
                apply_url=f"http://93.184.215.14/apply/{index}",
                match=make_match(version=VERSION),
            )
        )

    _run(store, http=httpx.Client(transport=httpx.MockTransport(respond)))

    assert len(checked) == 60


def test_should_stamp_apply_check_with_utc_time(store: Store) -> None:
    store.upsert_ad(
        make_ad(
            description="x",
            apply_url="http://93.184.215.14/apply/1",
            match=make_match(version=VERSION),
        )
    )

    _run(
        store,
        http=httpx.Client(
            transport=httpx.MockTransport(lambda request: httpx.Response(200))
        ),
    )

    stamp = store.active_ads()[0].apply_checked_at
    assert stamp is not None
    assert stamp.endswith("+00:00")


@pytest.mark.usefixtures("no_retry_wait")
def test_should_stamp_veto_time_in_utc(store: Store) -> None:
    feed = OTHER_TEAMTAILOR_FEED
    store.upsert_ad(
        make_ad(
            id="nav:1",
            source="nav",
            source_id="1",
            company="Org Two AS",
            description="x",
            match=make_match(version=VERSION),
        )
    )
    http = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(200, text="<rss><channel/></rss>")
        )
    )
    config = CONFIG.model_copy(
        update={"sources": SourcesConfig(nav=False, employer_feeds=(feed,))}
    )

    _run(store, config, http=http)

    stamp = store.active_ads()[0].apply_checked_at
    assert stamp is not None
    assert stamp.endswith("+00:00")


def test_should_close_feed_ad_no_longer_listed(store: Store) -> None:
    feed = EmployerFeed(
        kind="teamtailor",
        key="example",
        company="Example AS",
        board="https://careers.example.test/example.rss",
    )
    store.upsert_ad(
        make_ad(
            id="teamtailor:example:old",
            source="teamtailor:example",
            source_id="old",
            url="https://careers.example.test/jobs/old",
        )
    )
    http = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(200, text=TEAMTAILOR_XML)
        )
    )
    config = CONFIG.model_copy(
        update={"sources": SourcesConfig(nav=False, employer_feeds=(feed,))}
    )

    _run(store, config, http=http)

    assert "teamtailor:example:old" not in {ad.id for ad in store.active_ads()}


def test_should_reopen_nav_ad_listed_active_again(store: Store) -> None:
    store.upsert_ad(make_ad(id="nav:u1", source="nav", source_id="u1"))
    store.set_active("nav:u1", False)

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        return httpx.Response(
            200, json={"items": [{"_feed_entry": {"uuid": "u1", "status": "ACTIVE"}}]}
        )

    config = CONFIG.model_copy(update={"sources": SourcesConfig(employers=False)})

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    assert [ad.id for ad in store.active_ads()] == ["nav:u1"]


def test_should_keep_nav_cursor_written_before_feed_failure(store: Store) -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        if request.url.path.endswith("/broken"):
            return httpx.Response(404)
        return httpx.Response(
            200,
            json={
                "feed_url": "/api/v1/feed/page-1",
                "items": [{"_feed_entry": {"uuid": "u1", "status": "INACTIVE"}}],
                "next_url": "/api/v1/feed/broken",
            },
        )

    config = CONFIG.model_copy(update={"sources": SourcesConfig(employers=False)})

    result = _run(
        store, config, http=httpx.Client(transport=httpx.MockTransport(respond))
    )

    assert (result.failed_sources, store.cursors().get("nav_position")) == (
        ("nav",),
        "https://pam-stilling-feed.nav.no/api/v1/feed/page-1",
    )


def test_should_close_finn_ads_missing_from_complete_scan(store: Store) -> None:
    store.upsert_ad(make_ad(id="finn:99", source_id="99", description="x"))
    window_ad = make_ad(
        id="finn:98",
        source_id="98",
        description="x",
        published="2099-01-01T00:00:00+00:00",
    )
    store.upsert_ad(window_ad)
    store.mark_category_unlisted([window_ad.id])

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.startswith("/job/ad/"):
            return httpx.Response(404)
        recent = "sort" in request.url.params
        docs = [make_search_doc(5, published=1791331200000)]
        found = docs if recent else [make_search_doc(6)]
        return httpx.Response(200, text=make_search_html(found, len(found)))

    config = FINN_ONLY_CONFIG

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    active = {ad.id for ad in store.active_ads()}
    assert ("finn:99" in active, "finn:98" in active) == (False, False)


def test_should_backfill_apply_url_for_stored_finn_ad(store: Store) -> None:
    store.upsert_ad(
        make_ad(
            id="finn:7",
            source_id="7",
            description="x",
            match=make_match(version=VERSION),
        )
    )
    pages: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.startswith("/job/ad/"):
            pages.append(request.url.path)
            return httpx.Response(404)
        found = [] if "sort" in request.url.params else [make_search_doc(7)]
        return httpx.Response(200, text=make_search_html(found, len(found)))

    config = FINN_ONLY_CONFIG

    _run(store, config, http=httpx.Client(transport=httpx.MockTransport(respond)))

    assert pages == ["/job/ad/1234"]
