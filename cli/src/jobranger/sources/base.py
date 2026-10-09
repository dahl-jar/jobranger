from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Callable, Mapping

    import httpx

    from jobranger.ads.models import Ad, SourceSnapshot
    from jobranger.catalogue import Level, SourcePlan
    from jobranger.events import CancelToken

RECENT_FIRST_RUN_DAYS = 7
RECENT_OVERLAP_DAYS = 1
NORWAY_NAMES = frozenset({"norway", "norge"})


@dataclass(frozen=True)
class FetchContext:
    http: httpx.Client
    plan: SourcePlan
    run_started: str
    known_ids: frozenset[str]
    cursors: Mapping[str, str]
    cancel: CancelToken
    pending_pages: tuple[Ad, ...] = ()


@dataclass(frozen=True)
class FinnSync:
    category_ids: frozenset[str]
    recent_ids: frozenset[str]
    window_start: str | None
    unlisted_ids: tuple[str, ...]
    complete: bool


@dataclass(frozen=True)
class PageResult:
    ad_id: str
    description: str | None
    apply_url: str | None
    label: Level | None = None


@dataclass(frozen=True)
class SourceBatch:
    source: str
    ads: tuple[Ad, ...]
    read: int
    listed_ids: frozenset[str] | None = None
    cursors: Mapping[str, str] = field(default_factory=dict)
    active: Mapping[str, bool] = field(default_factory=dict)
    finn_sync: FinnSync | None = None
    pages: tuple[PageResult, ...] = ()


@dataclass(frozen=True)
class SourceOutcome:
    source: str
    batch: SourceBatch | None
    error: str | None
    cancelled: bool


@dataclass(frozen=True)
class PullCount:
    read: int
    new: int


class SourceError(Exception):
    def __init__(self, message: str, partial: SourceBatch | None = None) -> None:
        super().__init__(message)
        self.partial = partial


class SourceCancelled(Exception):
    pass


@dataclass(frozen=True)
class Source:
    name: str
    fetch: Callable[[FetchContext], SourceBatch]


def check_cancel(ctx: FetchContext) -> None:
    if ctx.cancel.cancelled:
        raise SourceCancelled


def checkpoint(ctx: FetchContext) -> Callable[[], None]:
    def check() -> None:
        check_cancel(ctx)

    return check


def no_checkpoint() -> None:
    return None


def feed_batch(source: str, snapshot: SourceSnapshot) -> SourceBatch:
    return SourceBatch(
        source=source,
        ads=tuple(snapshot.ads),
        read=len(snapshot.ads),
        listed_ids=snapshot.active_ids,
    )


def cursor_start(cursors: Mapping[str, str], key: str, started: datetime) -> datetime:
    since = cursors.get(key)
    if since:
        return datetime.fromisoformat(since)
    return started - timedelta(days=RECENT_FIRST_RUN_DAYS)


def advanced_cursor(started: datetime) -> str:
    return (started - timedelta(days=RECENT_OVERLAP_DAYS)).isoformat()
