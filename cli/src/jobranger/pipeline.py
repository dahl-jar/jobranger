from __future__ import annotations

import contextlib
import fcntl
import logging
import os
import queue
import time
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from datetime import UTC, datetime, timedelta
from typing import TYPE_CHECKING

from jobranger import paths
from jobranger.ads import apply_link
from jobranger.ads.duplicates import companies_match, group_duplicates, split_reported
from jobranger.catalogue import source_plan
from jobranger.events import RunEvent
from jobranger.matching.classify import (
    Classification,
    ClassifyContext,
    StopReason,
    classify_ads,
    with_title_level,
)
from jobranger.matching.merge import preferred_ad
from jobranger.matching.profile import education_ids, evidence_ids, load_profile
from jobranger.matching.prompt import assessment_version, system_for
from jobranger.matching.rules import (
    has_current_match,
    hidden_reason,
    is_stale,
)
from jobranger.providers.base import Usage
from jobranger.sources import finn, nav
from jobranger.sources.base import (
    FetchContext,
    PullCount,
    SourceCancelled,
    SourceError,
    SourceOutcome,
)
from jobranger.sources.registry import employer_feeds, enabled_sources
from jobranger.storage.store import RunSummary

if TYPE_CHECKING:
    from collections.abc import Iterator, Sequence
    from datetime import date
    from pathlib import Path

    import httpx

    from jobranger.ads.models import Ad
    from jobranger.catalogue import EmployerFeed
    from jobranger.events import CancelToken, EventSink, RunMode, RunStatus
    from jobranger.matching.profile import Profile
    from jobranger.matching.rules import HiddenKind
    from jobranger.providers.base import Provider
    from jobranger.settings import AppConfig, Targets
    from jobranger.sources.base import PageResult, Source, SourceBatch
    from jobranger.storage.store import Store, StoredRunStatus

logger = logging.getLogger(__name__)

LOCK_FILE_MODE = 0o600
LOCK_DIR_MODE = 0o700
LABEL_MAX_ATTEMPTS = 3
LABEL_ERROR = "error"
APPLY_CHECK_LIMIT = 60
APPLY_RECHECK_DAYS = 7
APPLY_URL_BACKFILL_LIMIT = 60
REPORTED_LOOKBACK_DAYS = 90
IMPORTED_SOURCES = frozenset({finn.SOURCE, nav.SOURCE})
SOURCE_THREAD_PREFIX = "source"


class RunLocked(Exception):
    pass


@dataclass(frozen=True)
class RunResult:
    run_id: int | None
    status: RunStatus
    mode: RunMode
    ads_read: int = 0
    new_ads: int = 0
    matches: int = 0
    hidden: int = 0
    failed_sources: tuple[str, ...] = ()
    usage: Usage = Usage()
    reported_ids: tuple[str, ...] = ()
    hidden_counts: dict[HiddenKind, int] = field(default_factory=dict)
    sources_read: tuple[str, ...] = ()
    budget_paused: bool = False
    pending: int = 0
    model_stopped: StopReason | None = None
    version: str | None = None


@dataclass
class _Tally:
    ads_read: int = 0
    new_ads: int = 0
    failed: list[str] = field(default_factory=list)
    sources_read: list[str] = field(default_factory=list)
    cancelled: bool = False
    usage: Usage = Usage()
    pending: int = 0
    model_stopped: StopReason | None = None
    reported_ids: tuple[str, ...] = ()
    hidden_counts: dict[HiddenKind, int] = field(default_factory=dict)


@dataclass(frozen=True)
class _Pass:
    config: AppConfig
    store: Store
    events: EventSink
    cancel: CancelToken
    mode: RunMode
    provider: Provider | None
    http: httpx.Client
    started_at: str
    today: date


@contextlib.contextmanager
def run_lock(path: Path) -> Iterator[None]:
    path.parent.mkdir(mode=LOCK_DIR_MODE, parents=True, exist_ok=True)
    descriptor = os.open(path, os.O_RDWR | os.O_CREAT, LOCK_FILE_MODE)
    try:
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RunLocked(str(path)) from error
        yield
    finally:
        os.close(descriptor)


def run_pass(
    config: AppConfig,
    store: Store,
    events: EventSink,
    cancel: CancelToken,
    *,
    mode: RunMode,
    provider: Provider | None,
    http: httpx.Client,
) -> RunResult:
    try:
        with run_lock(paths.run_lock_path()):
            started = datetime.now(tz=UTC)
            state = _Pass(
                config,
                store,
                events,
                cancel,
                mode,
                provider,
                http,
                started.isoformat(),
                started.date(),
            )
            return _locked_pass(state)
    except RunLocked:
        logger.info("run.locked another run holds the run lock")
        return RunResult(run_id=None, status="locked", mode=mode)


def fetch_sources(
    sources: Sequence[Source], ctx: FetchContext, out: queue.Queue[SourceOutcome]
) -> ThreadPoolExecutor:
    executor = ThreadPoolExecutor(
        max_workers=len(sources), thread_name_prefix=SOURCE_THREAD_PREFIX
    )
    for source in sources:
        executor.submit(_fetch_one, source, ctx, out)
    return executor


def _locked_pass(state: _Pass) -> RunResult:
    run_id = state.store.start_run(state.started_at)
    tally = _Tally()
    status: StoredRunStatus = "failed"
    try:
        profile = load_profile(state.config.profile.path)
        system = system_for(state.config, profile)
        version = assessment_version(system)
        status = _stages(state, tally, profile, system, version)
    finally:
        state.store.finish_run(
            run_id,
            RunSummary(
                finished_at=datetime.now(tz=UTC).isoformat(),
                status=status,
                ads_read=tally.ads_read,
                new_ads=tally.new_ads,
                matches=len(tally.reported_ids),
                hidden=sum(tally.hidden_counts.values()),
                failed_sources=tuple(tally.failed),
            ),
        )
    return RunResult(
        run_id=run_id,
        status=status,
        mode=state.mode,
        ads_read=tally.ads_read,
        new_ads=tally.new_ads,
        matches=len(tally.reported_ids),
        hidden=sum(tally.hidden_counts.values()),
        failed_sources=tuple(tally.failed),
        usage=tally.usage,
        reported_ids=tally.reported_ids,
        hidden_counts=tally.hidden_counts,
        sources_read=tuple(tally.sources_read),
        budget_paused=tally.model_stopped == StopReason.budget,
        pending=tally.pending,
        model_stopped=tally.model_stopped,
        version=version,
    )


def _stages(
    state: _Pass, tally: _Tally, profile: Profile, system: str, version: str
) -> StoredRunStatus:
    if not state.cancel.cancelled:
        _fetch_stage(state, tally)
    state.store.add_usage(state.today.isoformat(), Usage(), ads_read=tally.ads_read)
    if tally.cancelled or state.cancel.cancelled:
        return "cancelled"
    _classify_stage(state, tally, profile, system, version)
    if tally.model_stopped == StopReason.cancelled:
        return "cancelled"
    _report_stage(state, tally, version)
    return "completed"


def _fetch_stage(state: _Pass, tally: _Tally) -> None:
    sources = enabled_sources(state.config.sources)
    if not sources:
        return
    targets = state.config.targets
    plan = source_plan(targets)
    store = state.store
    pending_pages = (
        *store.pending_description(plan.skip_title_levels),
        *store.pending_apply_url(finn.SOURCE, APPLY_URL_BACKFILL_LIMIT, targets),
    )
    ctx = FetchContext(
        http=state.http,
        plan=plan,
        run_started=state.started_at,
        known_ids=store.known_ids(),
        cursors=store.cursors(),
        cancel=state.cancel,
        pending_pages=pending_pages,
    )
    for source in sources:
        state.events.emit(RunEvent("source_started", source=source.name))
    outcomes: queue.Queue[SourceOutcome] = queue.Queue()
    executor = fetch_sources(sources, ctx, outcomes)
    try:
        for _ in sources:
            _record_outcome(state, tally, outcomes.get())
    finally:
        executor.shutdown(wait=True)


def _record_outcome(state: _Pass, tally: _Tally, outcome: SourceOutcome) -> None:
    count = PullCount(0, 0)
    if outcome.batch is not None and not outcome.cancelled:
        count = _write_batch(state.store, outcome.batch, state.started_at)
        tally.ads_read += count.read
        tally.new_ads += count.new
    if outcome.error is not None:
        tally.failed.append(outcome.source)
        state.events.emit(RunEvent("source_failed", source=outcome.source))
    elif outcome.cancelled:
        tally.cancelled = True
    else:
        tally.sources_read.append(outcome.source)
        state.events.emit(
            RunEvent("source_done", source=outcome.source, found_new=count.new)
        )


def _fetch_one(
    source: Source, ctx: FetchContext, out: queue.Queue[SourceOutcome]
) -> None:
    try:
        batch = source.fetch(ctx)
    except SourceCancelled:
        out.put(SourceOutcome(source.name, None, None, cancelled=True))
    except SourceError as error:
        out.put(SourceOutcome(source.name, error.partial, str(error), cancelled=False))
    except Exception as error:
        logger.exception("source.crashed source=%s", source.name)
        out.put(SourceOutcome(source.name, None, type(error).__name__, cancelled=False))
    else:
        out.put(SourceOutcome(source.name, batch, None, cancelled=False))


def _write_batch(store: Store, batch: SourceBatch, run_started: str) -> PullCount:
    new = sum(store.upsert_ad(ad) for ad in batch.ads)
    for ad_id, active in batch.active.items():
        store.set_active(ad_id, active)
    sync = batch.finn_sync
    if sync is not None:
        store.mark_category_unlisted(list(sync.unlisted_ids))
        if sync.complete:
            store.sync_finn_active(
                sync.category_ids, sync.recent_ids, sync.window_start
            )
    for page in batch.pages:
        _write_page(store, page, run_started)
    if batch.listed_ids is not None:
        store.sync_source_active(batch.source, batch.listed_ids)
    for key, value in batch.cursors.items():
        store.set_state(key, value)
    return PullCount(batch.read, new)


def _write_page(store: Store, page: PageResult, run_started: str) -> None:
    if page.label is not None:
        store.set_label(page.ad_id, page.label)
    elif page.description is None:
        store.bump_description_attempts(page.ad_id)
    else:
        store.set_description(page.ad_id, page.description, page.apply_url)
        if page.apply_url is None:
            store.set_apply_status(page.ad_id, apply_link.STATUS_SKIPPED, run_started)


def _classify_stage(
    state: _Pass, tally: _Tally, profile: Profile, system: str, version: str
) -> None:
    store = state.store
    if store.refresh_assessments(version):
        logger.info("classify.refreshed assessment version changed version=%s", version)
    pending = store.pending_classification(LABEL_MAX_ATTEMPTS)
    representatives, groups = _prepare_classification(store, pending, version)
    if state.provider is None:
        tally.model_stopped = StopReason.auth
        tally.pending = len(representatives)
    elif representatives:
        result = classify_ads(
            representatives,
            ClassifyContext(
                store=store,
                config=state.config,
                provider=state.provider,
                system=system,
                evidence=evidence_ids(profile),
                education=education_ids(profile),
                version=version,
                mode=state.mode,
                events=state.events,
                cancel=state.cancel,
                today=state.today,
                sleep=time.sleep,
                monotonic=time.monotonic,
            ),
        )
        for representative_id, classification in result.assessments.items():
            _store_group(store, groups[representative_id], classification, version)
        store.bump_label_attempts(
            [ad.id for ad_id in result.failed for ad in groups[ad_id]]
        )
        tally.usage = result.usage
        tally.pending = result.pending
        tally.model_stopped = result.stopped
    for ad in store.exhausted_classification(LABEL_MAX_ATTEMPTS):
        store.set_label(ad.id, LABEL_ERROR)


def _prepare_classification(
    store: Store, pending: list[Ad], version: str
) -> tuple[list[Ad], dict[str, list[Ad]]]:
    pending_ids = {ad.id for ad in pending}
    representatives: list[Ad] = []
    groups: dict[str, list[Ad]] = {}
    for group in group_duplicates(store.active_ads()):
        targets = [ad for ad in group if ad.id in pending_ids]
        if not targets:
            continue
        completed = [ad for ad in group if _can_reuse(ad, version)]
        if completed:
            shared = _classification_from_ad(preferred_ad(completed, version))
            _store_group(store, targets, shared, version)
            continue
        representative = preferred_ad(targets, version)
        representatives.append(representative)
        groups[representative.id] = targets
    return representatives, groups


def _store_group(
    store: Store, ads: list[Ad], classification: Classification, version: str
) -> None:
    for ad in ads:
        store.set_classification(
            ad.id, with_title_level(ad.title, classification), version
        )


def _can_reuse(ad: Ad, version: str) -> bool:
    return (
        ad.label not in {None, LABEL_ERROR}
        and ad.is_programming_job is not None
        and has_current_match(ad, version)
    )


def _classification_from_ad(ad: Ad) -> Classification:
    return Classification(
        label=ad.label or LABEL_ERROR,
        summary=ad.summary,
        requires_master=ad.requires_master,
        is_programming_job=bool(ad.is_programming_job),
        display_title=ad.display_title,
        role_type=ad.role_type,
        technologies=ad.technologies,
        requirements=ad.requirements,
        match=ad.match,
    )


def _report_stage(state: _Pass, tally: _Tally, version: str) -> None:
    store = state.store
    targets = state.config.targets
    now = datetime.now(tz=UTC)
    state.events.emit(RunEvent("apply_checks"))
    _check_apply_links(state.http, store, now, targets)
    _veto_missing_employer_listings(
        store,
        frozenset(tally.sources_read),
        employer_feeds(state.config.sources),
        now.isoformat(),
    )
    today = state.today.isoformat()
    store.close_past_deadline(today)
    state.events.emit(RunEvent("report"))
    ads = store.reportable_ads(today, state.started_at, version, targets)
    lookback = (now - timedelta(days=REPORTED_LOOKBACK_DAYS)).isoformat()
    remaining, _ = split_reported(ads, store.reported_ads(lookback))
    tally.reported_ids = tuple(
        ad.id for ad in remaining if not is_stale(ad, state.today)
    )
    tally.hidden_counts = _hidden_counts(
        store.new_since(state.started_at), set(tally.reported_ids), version, targets
    )


def _hidden_counts(
    new_ads: list[Ad], reported: set[str], version: str, targets: Targets
) -> dict[HiddenKind, int]:
    kinds: Counter[HiddenKind] = Counter()
    for ad in new_ads:
        if ad.id in reported or ad.label is None:
            continue
        hidden = hidden_reason(ad, version, targets)
        if hidden is not None:
            kinds[hidden.kind] += 1
    return dict(kinds)


def _check_apply_links(
    client: httpx.Client, store: Store, checked_at: datetime, targets: Targets
) -> None:
    today = checked_at.date().isoformat()
    recheck_before = (checked_at - timedelta(days=APPLY_RECHECK_DAYS)).isoformat()
    counts: Counter[str] = Counter()
    for ad in store.pending_apply_check(
        today, recheck_before, APPLY_CHECK_LIMIT, targets
    ):
        if ad.apply_url is None:
            continue
        status = _apply_status(client, ad.id, ad.apply_url)
        store.set_apply_status(ad.id, status, checked_at.isoformat())
        counts[status] += 1
    logger.info(
        "apply.check.done checked %d apply links expired=%d unknown=%d "
        "skipped=%d blocked=%d",
        sum(counts.values()),
        counts[apply_link.STATUS_EXPIRED],
        counts[apply_link.STATUS_UNKNOWN],
        counts[apply_link.STATUS_SKIPPED],
        counts[apply_link.STATUS_BLOCKED],
    )


def _apply_status(client: httpx.Client, ad_id: str, url: str) -> str:
    try:
        if not apply_link.is_checkable(url):
            return apply_link.STATUS_SKIPPED
    except ValueError:
        logger.warning(
            "apply.check.malformed apply url could not be parsed id=%s", ad_id
        )
        return apply_link.STATUS_SKIPPED
    return apply_link.check_apply_link(client, url)


def _veto_missing_employer_listings(
    store: Store,
    succeeded_sources: frozenset[str],
    feeds: tuple[EmployerFeed, ...],
    checked_at: str,
) -> None:
    succeeded = [feed for feed in feeds if feed.source in succeeded_sources]
    if not succeeded:
        return
    for group in group_duplicates(store.active_ads()):
        for feed in succeeded:
            _veto_group(store, group, feed, checked_at)


def _veto_group(
    store: Store, group: list[Ad], feed: EmployerFeed, checked_at: str
) -> None:
    if any(ad.source == feed.source for ad in group):
        return
    for ad in group:
        if (
            ad.source in IMPORTED_SOURCES
            and ad.apply_status != apply_link.STATUS_EXPIRED
            and companies_match(ad.company, feed.company)
        ):
            store.set_apply_status(ad.id, apply_link.STATUS_EXPIRED, checked_at)
            logger.info(
                "apply.veto listing missing from employer feed id=%s feed=%s",
                ad.id,
                feed.source,
            )
