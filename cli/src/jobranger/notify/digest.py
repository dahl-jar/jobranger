from __future__ import annotations

from dataclasses import dataclass, field
from datetime import UTC, date, datetime
from typing import TYPE_CHECKING

from jobranger.ads.apply_link import STATUS_EXPIRED
from jobranger.ads.duplicates import DIRECT_SOURCE_PREFIXES, group_duplicates
from jobranger.matching.classify import StopReason
from jobranger.matching.merge import preferred_ad
from jobranger.matching.rules import (
    max_years,
    parse_date,
)

if TYPE_CHECKING:
    from jobranger.ads.models import Ad
    from jobranger.matching.rules import HiddenKind
    from jobranger.pipeline import RunResult
    from jobranger.settings import AppConfig
    from jobranger.storage.store import Store

APPLY_SOON_DAYS = 7
NOTICE_STOPS = frozenset({StopReason.auth, StopReason.provider_down})
RECENT_RUNS = 2
OTHER_KIND = "other"


@dataclass(frozen=True)
class DigestData:
    day: date
    ran_at: datetime
    apply_soon: list[Ad]
    new_today: list[Ad]
    hidden: dict[HiddenKind, int]
    ads_read: int
    new_ads: int
    sources_read: list[str]
    employer_pages: int
    failed_sources: list[str]
    closed_since_last: int
    budget_paused: bool
    pending: int
    max_years: float | None
    carried: list[Ad] = field(default_factory=list)
    model_stopped: StopReason | None = None


def build_digest(
    store: Store, result: RunResult, config: AppConfig, today: date
) -> DigestData:
    targets = config.targets
    wanted = (
        []
        if result.version is None
        else _wanted_ads(
            store.keep_reportable(
                store.ads_by_id(list(result.reported_ids)), result.version, targets
            ),
            today,
            result.version,
        )
    )
    apply_soon, new_today, carried = _groups(wanted, set(result.carried_ids), today)
    return DigestData(
        day=today,
        ran_at=datetime.now(tz=UTC),
        apply_soon=apply_soon,
        new_today=new_today,
        hidden={
            kind: count
            for kind, count in result.hidden_counts.items()
            if kind != OTHER_KIND
        },
        ads_read=result.ads_read,
        new_ads=result.new_ads,
        sources_read=list(result.sources_read),
        employer_pages=sum(
            source.startswith(DIRECT_SOURCE_PREFIXES) for source in result.sources_read
        ),
        failed_sources=list(result.failed_sources),
        closed_since_last=_closed_since_last(store, result.run_id),
        budget_paused=result.budget_paused,
        pending=result.pending,
        max_years=max_years(targets.limits),
        carried=carried,
        model_stopped=_notice_stop(result),
    )


def _groups(
    wanted: list[Ad], carried_ids: set[str], today: date
) -> tuple[list[Ad], list[Ad], list[Ad]]:
    fresh = [ad for ad in wanted if ad.id not in carried_ids]
    apply_soon = sorted(
        (ad for ad in fresh if _due_soon(ad, today)), key=_deadline_order
    )
    new_today = sorted(
        (ad for ad in fresh if not _due_soon(ad, today)), key=_new_today_order
    )
    carried = sorted((ad for ad in wanted if ad.id in carried_ids), key=_deadline_order)
    return apply_soon, new_today, carried


def _notice_stop(result: RunResult) -> StopReason | None:
    if result.model_stopped in NOTICE_STOPS and result.pending:
        return result.model_stopped
    return None


def _wanted_ads(candidates: list[Ad], today: date, version: str) -> list[Ad]:
    preferred = (preferred_ad(group, version) for group in group_duplicates(candidates))
    return [ad for ad in preferred if not _is_expired(ad, today)]


def _is_expired(ad: Ad, today: date) -> bool:
    if ad.apply_status == STATUS_EXPIRED:
        return True
    deadline = parse_date(ad.deadline)
    return deadline is not None and deadline < today


def _due_soon(ad: Ad, today: date) -> bool:
    deadline = parse_date(ad.deadline)
    return deadline is not None and (deadline - today).days <= APPLY_SOON_DAYS


def _deadline_order(ad: Ad) -> tuple[bool, str]:
    return (ad.deadline is None, ad.deadline or "")


def _new_today_order(ad: Ad) -> tuple[bool, bool, str]:
    strong = ad.match is not None and ad.match.status == "strong"
    return (not strong, *_deadline_order(ad))


def _closed_since_last(store: Store, run_id: int | None) -> int:
    previous = [run for run in store.runs(RECENT_RUNS) if run.id != run_id]
    if not previous:
        return 0
    return store.closed_since(previous[0].started_at)
