from __future__ import annotations

import logging
import re
from typing import TYPE_CHECKING

import httpx

from jobranger.ads.models import Ad, strip_html
from jobranger.net.retry import get_with_retry
from jobranger.sources.base import (
    FetchContext,
    SourceBatch,
    SourceError,
    check_cancel,
    checkpoint,
    no_checkpoint,
)

if TYPE_CHECKING:
    from collections.abc import Callable, Iterator

    from jobranger.catalogue import SourcePlan

logger = logging.getLogger(__name__)

BASE_URL = "https://pam-stilling-feed.nav.no"
FEED_PATH = "/api/v1/feed"
LAST_PAGE_URL = f"{BASE_URL}{FEED_PATH}?last=true"
JWT_PATTERN = re.compile(r"eyJ[A-Za-z0-9_.-]+")
STYRK_TYPE = "STYRK08"
SOURCE = "nav"
NAV_POSITION_KEY = "nav_position"
ACTIVE_STATUS = "ACTIVE"


class NavTokenError(Exception):
    pass


def fetch_public_token(client: httpx.Client) -> str:
    response = get_with_retry(client, f"{BASE_URL}/api/publicToken")
    match = JWT_PATTERN.search(response.text)
    if match is None:
        msg = "no JWT in publicToken response"
        raise NavTokenError(msg)
    return match.group(0)


def iter_new_entries(
    client: httpx.Client,
    token: str,
    start_url: str | None,
    before_request: Callable[[], None] = no_checkpoint,
) -> Iterator[tuple[dict[str, object], str]]:
    url = _absolute(start_url) if start_url else LAST_PAGE_URL
    while url:
        before_request()
        response = get_with_retry(
            client, url, headers={"Authorization": f"Bearer {token}"}
        )
        page = response.json()
        page_url = _absolute(page.get("feed_url") or url)
        for item in page.get("items", []):
            yield item, page_url
        next_url = page.get("next_url")
        url = _absolute(next_url) if next_url else ""


def fetch_entry(
    client: httpx.Client, token: str, uuid: str
) -> dict[str, object] | None:
    response = get_with_retry(
        client,
        f"{BASE_URL}/api/v1/feedentry/{uuid}",
        headers={"Authorization": f"Bearer {token}"},
    )
    entry = response.json()
    content = entry.get("ad_content")
    return content if isinstance(content, dict) else None


def is_developer_ad(ad_content: dict[str, object], plan: SourcePlan) -> bool:
    return (
        _has_development_category(ad_content, plan)
        or _has_developer_styrk(ad_content, plan)
        or plan.title_matches(
            str(ad_content.get("title") or ""), str(ad_content.get("jobtitle") or "")
        )
    )


def _has_development_category(ad_content: dict[str, object], plan: SourcePlan) -> bool:
    categories = ad_content.get("occupationCategories") or []
    if not isinstance(categories, list):
        return False
    return any(
        (
            str(category.get("level1", "")).strip().lower(),
            str(category.get("level2", "")).strip().lower(),
        )
        in plan.nav_categories
        for category in categories
        if isinstance(category, dict)
    )


def _has_developer_styrk(ad_content: dict[str, object], plan: SourcePlan) -> bool:
    categories = ad_content.get("categoryList") or []
    if not isinstance(categories, list):
        return False
    return any(
        category.get("categoryType") == STYRK_TYPE
        and str(category.get("code", "")).startswith(plan.nav_styrk)
        for category in categories
        if isinstance(category, dict)
    )


def to_ad(uuid: str, ad_content: dict[str, object]) -> Ad:
    employer = ad_content.get("employer")
    company = ""
    if isinstance(employer, dict):
        company = str(employer.get("name", ""))
    locations = ad_content.get("workLocations")
    location = ""
    if isinstance(locations, list) and locations and isinstance(locations[0], dict):
        location = str(locations[0].get("municipal") or locations[0].get("city") or "")
    url = str(ad_content.get("link") or ad_content.get("sourceurl") or "")
    description = strip_html(str(ad_content.get("description") or ""))
    return Ad(
        id=f"nav:{uuid}",
        source="nav",
        source_id=uuid,
        title=str(ad_content.get("title", "")),
        company=company,
        location=location,
        published=_str_or_none(ad_content.get("published")),
        deadline=_str_or_none(ad_content.get("expires")),
        url=url,
        description=description or None,
        apply_url=_str_or_none(ad_content.get("applicationUrl")),
    )


def _absolute(url: object) -> str:
    text = str(url)
    return text if text.startswith("http") else f"{BASE_URL}{text}"


def _str_or_none(value: object) -> str | None:
    return str(value) if value else None


def pull(ctx: FetchContext) -> SourceBatch:
    try:
        token = fetch_public_token(ctx.http)
    except (httpx.HTTPError, NavTokenError) as error:
        logger.exception("nav.token.failed public token request failed")
        raise SourceError("nav token failed") from error
    known = set(ctx.known_ids)
    ads: list[Ad] = []
    active: dict[str, bool] = {}
    read = 0
    current_page = ctx.cursors.get(NAV_POSITION_KEY)
    try:
        for item, page_url in iter_new_entries(
            ctx.http, token, current_page, checkpoint(ctx)
        ):
            current_page = page_url
            read += 1
            ad = _new_ad(ctx, token, item, known, active)
            if ad is not None:
                ads.append(ad)
                known.add(ad.id)
    except httpx.HTTPError as error:
        logger.exception("nav.feed.failed, position saved at last completed page")
        partial = _batch(ads, active, read, current_page)
        raise SourceError("nav feed failed", partial) from error
    logger.info("nav.feed.done new=%d", len(ads))
    return _batch(ads, active, read, current_page)


def _new_ad(
    ctx: FetchContext,
    token: str,
    item: dict[str, object],
    known: set[str],
    active: dict[str, bool],
) -> Ad | None:
    feed_entry = item.get("_feed_entry")
    if not isinstance(feed_entry, dict):
        return None
    uuid = str(feed_entry.get("uuid", ""))
    if not uuid:
        return None
    ad_id = f"{SOURCE}:{uuid}"
    is_active = feed_entry.get("status") == ACTIVE_STATUS
    active[ad_id] = is_active
    if not is_active or ad_id in known:
        return None
    check_cancel(ctx)
    content = _fetch_nav_entry(ctx.http, token, uuid)
    if content is None or not is_developer_ad(content, ctx.plan):
        return None
    return to_ad(uuid, content)


def _fetch_nav_entry(
    client: httpx.Client, token: str, uuid: str
) -> dict[str, object] | None:
    try:
        return fetch_entry(client, token, uuid)
    except httpx.HTTPError:
        logger.warning("nav.entry.failed uuid=%s, skipped", uuid)
        return None


def _batch(
    ads: list[Ad], active: dict[str, bool], read: int, current_page: str | None
) -> SourceBatch:
    return SourceBatch(
        source=SOURCE,
        ads=tuple(ads),
        read=read,
        cursors={NAV_POSITION_KEY: current_page} if current_page else {},
        active=dict(active),
    )
