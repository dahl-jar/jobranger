from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, date, datetime
from typing import TYPE_CHECKING

from jobranger.ads.apply_link import STATUS_EXPIRED
from jobranger.ads.duplicates import DIRECT_SOURCE_PREFIXES, group_duplicates
from jobranger.matching.merge import preferred_ad
from jobranger.matching.profile import load_profile, profile_hash
from jobranger.matching.rules import (
    build_rules,
    is_reportable,
    max_years,
    parse_date,
)

if TYPE_CHECKING:
    from jobranger.ads.models import Ad
    from jobranger.matching.rules import HiddenKind
    from jobranger.pipeline import RunResult
    from jobranger.settings import AppConfig, Targets
    from jobranger.storage.store import Store

APPLY_SOON_DAYS = 7
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


def build_digest(
    store: Store, result: RunResult, config: AppConfig, today: date
) -> DigestData:
    targets = config.targets
    version = profile_hash(
        load_profile(config.profile.path), build_rules(targets.limits)
    )
    wanted = _wanted_ads(
        store.ads_by_id(list(result.reported_ids)), today, version, targets
    )
    apply_soon = sorted(
        (ad for ad in wanted if _due_soon(ad, today)), key=lambda ad: ad.deadline or ""
    )
    new_today = sorted(
        (ad for ad in wanted if not _due_soon(ad, today)), key=_new_today_order
    )
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
    )


def _wanted_ads(
    candidates: list[Ad], today: date, version: str, targets: Targets
) -> list[Ad]:
    preferred = (preferred_ad(group, version) for group in group_duplicates(candidates))
    return [
        ad
        for ad in preferred
        if not _is_expired(ad, today) and is_reportable(ad, version, targets)
    ]


def _is_expired(ad: Ad, today: date) -> bool:
    if ad.apply_status == STATUS_EXPIRED:
        return True
    deadline = parse_date(ad.deadline)
    return deadline is not None and deadline < today


def _due_soon(ad: Ad, today: date) -> bool:
    deadline = parse_date(ad.deadline)
    return deadline is not None and (deadline - today).days <= APPLY_SOON_DAYS


def _new_today_order(ad: Ad) -> tuple[bool, bool, str]:
    strong = ad.match is not None and ad.match.status == "strong"
    return (not strong, ad.deadline is None, ad.deadline or "")


def _closed_since_last(store: Store, run_id: int | None) -> int:
    previous = [run for run in store.runs(RECENT_RUNS) if run.id != run_id]
    if not previous:
        return 0
    return store.closed_since(previous[0].started_at)
