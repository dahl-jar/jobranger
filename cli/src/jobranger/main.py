from __future__ import annotations

import logging
from collections import Counter
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta

import httpx

from jobranger import run_logs
from jobranger.ads import apply_link
from jobranger.ads.duplicates import (
    companies_match,
    group_duplicates,
    split_reported,
)
from jobranger.ads.models import Ad
from jobranger.ads.titles import title_is_senior
from jobranger.config import Settings
from jobranger.matching import classify
from jobranger.matching.merge import preferred_ad
from jobranger.matching.rules import PROFILE_VERSION, has_current_match
from jobranger.net import rate_limit
from jobranger.notify import digest
from jobranger.sources import ashby, finn, linkedin, nav, teamtailor
from jobranger.sources.registry import (
    ASHBY_BOARDS,
    TEAMTAILOR_FEEDS,
    AshbyBoard,
    TeamtailorFeed,
)
from jobranger.storage.store import Store

logger = logging.getLogger(__name__)

HTTP_TIMEOUT_SECONDS = 30.0
NAV_POSITION_KEY = "nav_position"
FINN_RECENT_KEY = "finn_recent_since"
LINKEDIN_SINCE_KEY = "linkedin_since"
RECENT_FIRST_RUN_DAYS = 7
RECENT_OVERLAP_DAYS = 1
LABEL_MAX_ATTEMPTS = 3
LABEL_ERROR = "error"
APPLY_CHECK_LIMIT = 60
REPORTED_LOOKBACK_DAYS = 90
APPLY_RECHECK_DAYS = 7
APPLY_URL_BACKFILL_LIMIT = 60
IMPORTED_SOURCES = frozenset({"finn", "nav"})
NO_USAGE = classify.Usage()


@dataclass(frozen=True)
class SourceOutcome:
    name: str
    succeeded: bool


def run(settings: Settings) -> int:
    run_started = datetime.now(tz=UTC)
    today = run_started.date()
    log_handler = run_logs.attach_file_handler(settings.log_dir, today.isoformat())
    try:
        run_logs.prune_old_logs(settings.log_dir, today)
        outcomes = _daily_pass(settings)
        return 0 if any(outcome.succeeded for outcome in outcomes) else 1
    finally:
        logging.getLogger().removeHandler(log_handler)
        log_handler.close()


def _daily_pass(settings: Settings) -> list[SourceOutcome]:
    run_started = run_timestamp()
    store = Store(settings.db_path)
    limiter = rate_limit.HostLimiter(rate_limit.HOST_RULES, rate_limit.DEFAULT_RULE)
    with httpx.Client(
        timeout=HTTP_TIMEOUT_SECONDS,
        follow_redirects=True,
        event_hooks=rate_limit.event_hooks(limiter),
    ) as client:
        recent = _pull_finn_recent(client, store, run_started)
        outcomes = [
            SourceOutcome("finn-recent", recent is not None),
            SourceOutcome("finn", _pull_finn(client, store, settings, recent)),
            SourceOutcome("nav", _pull_nav(client, store)),
            SourceOutcome("linkedin", _pull_linkedin(client, store, run_started)),
            *(
                SourceOutcome(board.source, _pull_ashby_board(client, store, board))
                for board in ASHBY_BOARDS
            ),
            *(
                SourceOutcome(feed.source, _pull_teamtailor_feed(client, store, feed))
                for feed in TEAMTAILOR_FEEDS
            ),
        ]
        if settings.reclassify_all:
            store.reset_classifications()
        usage = _classify_pending(store, settings)
        checked_at = datetime.now(tz=UTC)
        _check_apply_links(client, store, checked_at)
        succeeded = frozenset(outcome.name for outcome in outcomes if outcome.succeeded)
        _veto_missing_employer_listings(store, succeeded, checked_at.isoformat())
    failed_sources = tuple(
        outcome.name for outcome in outcomes if not outcome.succeeded
    )
    _report(store, settings, run_started, failed_sources, usage)
    return outcomes


def _check_apply_links(
    client: httpx.Client, store: Store, checked_at: datetime
) -> None:
    today = checked_at.date().isoformat()
    recheck_before = (checked_at - timedelta(days=APPLY_RECHECK_DAYS)).isoformat()
    counts: Counter[str] = Counter()
    for ad in store.pending_apply_check(today, recheck_before, APPLY_CHECK_LIMIT):
        if ad.apply_url is None:
            continue
        status = (
            apply_link.check_apply_link(client, ad.apply_url)
            if apply_link.is_checkable(ad.apply_url)
            else apply_link.STATUS_SKIPPED
        )
        store.set_apply_status(ad.id, status, checked_at.isoformat())
        counts[status] += 1
    logger.info(
        "apply.check.done checked %d apply links expired=%d unknown=%d skipped=%d",
        sum(counts.values()),
        counts[apply_link.STATUS_EXPIRED],
        counts[apply_link.STATUS_UNKNOWN],
        counts[apply_link.STATUS_SKIPPED],
    )


def _veto_missing_employer_listings(
    store: Store, succeeded_sources: frozenset[str], checked_at: str
) -> None:
    employer_feeds: tuple[AshbyBoard | TeamtailorFeed, ...] = (
        *ASHBY_BOARDS,
        *TEAMTAILOR_FEEDS,
    )
    succeeded_feeds = [
        feed for feed in employer_feeds if feed.source in succeeded_sources
    ]
    if not succeeded_feeds:
        return
    for group in group_duplicates(store.active_ads()):
        for feed in succeeded_feeds:
            _veto_group(store, group, feed, checked_at)


def _veto_group(
    store: Store,
    group: list[Ad],
    feed: AshbyBoard | TeamtailorFeed,
    checked_at: str,
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


def _pull_finn_recent(
    client: httpx.Client, store: Store, run_started: str
) -> finn.FinnRecentResult | None:
    started = datetime.fromisoformat(run_started)
    since = _cursor_start(store, FINN_RECENT_KEY, started).isoformat()
    try:
        recent = finn.fetch_recent_ads(client, store.known_ids(), since)
    except (httpx.HTTPError, finn.FinnParseError):
        logger.exception("finn.recent.failed")
        return None
    for ad in recent.new_ads:
        store.upsert_ad(ad)
    store.mark_category_unlisted([ad.id for ad in recent.new_ads])
    _advance_cursor(store, FINN_RECENT_KEY, started)
    return recent


def _pull_linkedin(client: httpx.Client, store: Store, run_started: str) -> bool:
    started = datetime.fromisoformat(run_started)
    window_start = _cursor_start(store, LINKEDIN_SINCE_KEY, started)
    window = int((started - window_start).total_seconds())
    try:
        ads = linkedin.fetch_new_ads(client, store.known_ids(), window)
    except (httpx.HTTPError, linkedin.LinkedInParseError):
        logger.exception("linkedin.failed")
        return False
    for ad in ads:
        store.upsert_ad(ad)
    _advance_cursor(store, LINKEDIN_SINCE_KEY, started)
    logger.info("linkedin.done new=%d window_hours=%d", len(ads), window // 3600)
    return True


def _cursor_start(store: Store, key: str, started: datetime) -> datetime:
    since = store.get_state(key)
    if since:
        return datetime.fromisoformat(since)
    return started - timedelta(days=RECENT_FIRST_RUN_DAYS)


def _advance_cursor(store: Store, key: str, started: datetime) -> None:
    overlap = timedelta(days=RECENT_OVERLAP_DAYS)
    store.set_state(key, (started - overlap).isoformat())


def _pull_finn(
    client: httpx.Client,
    store: Store,
    settings: Settings,
    recent: finn.FinnRecentResult | None = None,
) -> bool:
    try:
        search = finn.fetch_new_ads(
            client, settings.finn_occupations, store.known_ids()
        )
    except (httpx.HTTPError, finn.FinnParseError):
        logger.exception("finn.search.failed")
        return False
    if search.complete and not search.active_ids:
        logger.warning(
            "finn.search.empty complete scan returned no ads, active status unchanged"
        )
        return False
    for ad in search.new_ads:
        store.upsert_ad(ad)
    if search.complete:
        store.sync_finn_active(
            search.active_ids,
            recent.seen_ids if recent else frozenset(),
            recent.window_start if recent else None,
        )
    else:
        logger.warning("finn.search.incomplete active status unchanged")
    finn_pending = [ad for ad in store.pending_description() if ad.source == "finn"]
    backfill = store.pending_apply_url("finn", APPLY_URL_BACKFILL_LIMIT)
    _store_finn_pages(client, store, [*finn_pending, *backfill])
    return True


def _store_finn_pages(client: httpx.Client, store: Store, ads: list[Ad]) -> None:
    for ad in ads:
        if title_is_senior(ad.title):
            store.set_label(ad.id, classify.SENIOR_LABEL)
            continue
        try:
            page = finn.fetch_ad_page(client, ad.url)
        except (httpx.HTTPError, finn.FinnParseError):
            store.bump_description_attempts(ad.id)
            logger.warning("finn.ad_text.failed id=%s", ad.id)
            continue
        store.set_description(ad.id, page.description, page.apply_url)
        if page.apply_url is None:
            store.set_apply_status(ad.id, apply_link.STATUS_SKIPPED, run_timestamp())


def run_timestamp() -> str:
    return datetime.now(tz=UTC).isoformat()


def _pull_nav(client: httpx.Client, store: Store) -> bool:
    try:
        token = nav.fetch_public_token(client)
    except (httpx.HTTPError, nav.NavTokenError):
        logger.exception("nav.token.failed")
        return False
    start_url = store.get_state(NAV_POSITION_KEY)
    known = store.known_ids()
    stored = 0
    current_page = start_url
    try:
        for item, page_url in nav.iter_new_entries(client, token, start_url):
            if current_page is not None and page_url != current_page:
                store.set_state(NAV_POSITION_KEY, current_page)
            current_page = page_url
            feed_entry = item.get("_feed_entry")
            if not isinstance(feed_entry, dict):
                continue
            uuid = str(feed_entry.get("uuid", ""))
            if feed_entry.get("status") != "ACTIVE":
                if uuid:
                    store.set_active(f"nav:{uuid}", False)
                continue
            ad_id = f"nav:{uuid}"
            store.set_active(ad_id, True)
            if ad_id in known:
                continue
            content = _fetch_nav_entry(client, token, uuid)
            if content is not None and nav.is_developer_ad(content):
                stored += int(store.upsert_ad(nav.to_ad(uuid, content)))
                known.add(ad_id)
    except httpx.HTTPError:
        logger.exception("nav.feed.failed, position saved at last completed page")
        if current_page is not None:
            store.set_state(NAV_POSITION_KEY, current_page)
        return False
    if current_page is not None:
        store.set_state(NAV_POSITION_KEY, current_page)
    logger.info("nav.feed.done new=%d", stored)
    return True


def _fetch_nav_entry(
    client: httpx.Client, token: str, uuid: str
) -> dict[str, object] | None:
    try:
        return nav.fetch_entry(client, token, uuid)
    except httpx.HTTPError:
        logger.warning("nav.entry.failed uuid=%s, skipped", uuid)
        return None


def _pull_ashby_board(client: httpx.Client, store: Store, board: AshbyBoard) -> bool:
    try:
        snapshot = ashby.fetch_board(client, board)
    except (httpx.HTTPError, ashby.AshbyParseError):
        logger.exception("ashby.board.failed key=%s", board.key)
        return False
    for ad in snapshot.ads:
        store.upsert_ad(ad)
    store.sync_source_active(board.source, snapshot.active_ids)
    logger.info("ashby.board.done key=%s active=%d", board.key, len(snapshot.ads))
    return True


def _pull_teamtailor_feed(
    client: httpx.Client, store: Store, feed: TeamtailorFeed
) -> bool:
    try:
        snapshot = teamtailor.fetch_feed(client, feed)
    except (httpx.HTTPError, teamtailor.TeamtailorParseError):
        logger.exception("teamtailor.feed.failed key=%s", feed.key)
        return False
    for ad in snapshot.ads:
        store.upsert_ad(ad)
    store.sync_source_active(feed.source, snapshot.active_ids)
    logger.info("teamtailor.feed.done key=%s active=%d", feed.key, len(snapshot.ads))
    return True


def _classify_pending(store: Store, settings: Settings) -> classify.Usage:
    if store.refresh_assessments():
        logger.info("classify.profile_refreshed version=%s", PROFILE_VERSION)
    pending = store.pending_classification(LABEL_MAX_ATTEMPTS)
    if not pending:
        return NO_USAGE
    representatives, targets, reused = _prepare_classification(store, pending)
    store.bump_label_attempts(
        [ad_id for target_ids in targets.values() for ad_id in target_ids]
    )
    batch = (
        classify.classify_batch(representatives, settings.claude_bin)
        if representatives
        else classify.BatchResult({})
    )
    results = batch.assessments
    pending_by_id = {ad.id: ad for ad in pending}
    for representative_id, result in results.items():
        group = [pending_by_id[ad_id] for ad_id in targets[representative_id]]
        _store_with_title_labels(store, group, result)
    for ad in store.exhausted_classification(LABEL_MAX_ATTEMPTS):
        store.set_label(ad.id, LABEL_ERROR)
    labeled = reused + sum(len(targets[ad_id]) for ad_id in results)
    logger.info(
        "classify.done labeled=%d of=%d requests=%d reused=%d",
        labeled,
        len(pending),
        len(representatives),
        reused,
    )
    _log_usage(batch.usage)
    return batch.usage


def _log_usage(usage: classify.Usage) -> None:
    logger.info(
        "classify.usage calls=%d input=%d cache_write=%d cache_read=%d "
        "output=%d total=%d cost_usd=%.2f",
        usage.calls,
        usage.input_tokens,
        usage.cache_write_tokens,
        usage.cache_read_tokens,
        usage.output_tokens,
        usage.total_tokens,
        usage.cost_usd,
    )


def _prepare_classification(
    store: Store, pending: list[Ad]
) -> tuple[list[Ad], dict[str, list[str]], int]:
    pending_ids = {ad.id for ad in pending}
    representatives: list[Ad] = []
    targets_by_representative: dict[str, list[str]] = {}
    reused = 0
    for group in group_duplicates(store.active_ads()):
        reused += _prepare_classification_group(
            store,
            group,
            pending_ids,
            representatives,
            targets_by_representative,
        )
    return representatives, targets_by_representative, reused


def _prepare_classification_group(
    store: Store,
    group: list[Ad],
    pending_ids: set[str],
    representatives: list[Ad],
    targets_by_representative: dict[str, list[str]],
) -> int:
    targets = [ad.id for ad in group if ad.id in pending_ids]
    if not targets:
        return 0
    completed = [ad for ad in group if _can_reuse(ad)]
    if completed:
        shared = _classification_from_ad(preferred_ad(completed))
        _store_with_title_labels(
            store, [ad for ad in group if ad.id in pending_ids], shared
        )
        return len(targets)
    representative = preferred_ad([ad for ad in group if ad.id in pending_ids])
    representatives.append(representative)
    targets_by_representative[representative.id] = targets
    return 0


def _store_with_title_labels(
    store: Store, ads: list[Ad], result: classify.Classification
) -> None:
    for ad in ads:
        store.set_classification(ad.id, classify.with_title_label(ad.title, result))


def _can_reuse(ad: Ad) -> bool:
    return (
        ad.label not in {None, LABEL_ERROR}
        and ad.is_programming_job is not None
        and has_current_match(ad)
    )


def _classification_from_ad(ad: Ad) -> classify.Classification:
    if ad.label is None or ad.is_programming_job is None:
        raise ValueError("Cannot reuse an incomplete classification")
    return classify.Classification(
        label=ad.label,
        summary=ad.summary,
        requires_master=ad.requires_master,
        is_programming_job=ad.is_programming_job,
        display_title=ad.display_title,
        role_type=ad.role_type,
        technologies=ad.technologies,
        requirements=ad.requirements,
        match=ad.match,
    )


def _report(
    store: Store,
    settings: Settings,
    run_started: str,
    failed_sources: tuple[str, ...] = (),
    usage: classify.Usage = NO_USAGE,
) -> None:
    today = datetime.now(tz=UTC).date().isoformat()
    since = None if settings.report_all else run_started
    new_ads_count = None if settings.report_all else len(store.new_since(run_started))
    ads = store.reportable_ads(today, since)
    covered: list[str] = []
    if since is not None:
        lookback = datetime.now(tz=UTC) - timedelta(days=REPORTED_LOOKBACK_DAYS)
        ads, covered = split_reported(ads, store.reported_ads(lookback.isoformat()))
    summary = digest.build_summary(
        today,
        ads,
        new_ads_count=new_ads_count,
        full_listing=settings.report_all,
        context=digest.ReportContext(
            failed_sources=failed_sources,
            expired_ads=tuple(store.expired_ads(today)),
            expired_since=since,
            usage=usage,
        ),
    )
    settings.report_dir.mkdir(parents=True, exist_ok=True)
    (settings.report_dir / f"{today}.md").write_text(summary)
    if not settings.report_all:
        store.mark_reported([ad.id for ad in ads] + covered, run_timestamp())
    if not settings.discord_webhook_url:
        logger.warning("report.discord.skipped no webhook configured")
        return
    try:
        digest.send_discord(settings.discord_webhook_url, summary)
    except httpx.HTTPError as error:
        logger.error("report.discord.failed error=%s", type(error).__name__)
