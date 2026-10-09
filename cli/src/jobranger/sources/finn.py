from __future__ import annotations

import base64
import json
import logging
import re
from dataclasses import dataclass, replace
from datetime import UTC, datetime
from typing import TYPE_CHECKING, Any

from jobranger.ads.models import Ad, strip_html
from jobranger.ads.titles import looks_like_developer, title_is_senior
from jobranger.net.headers import BROWSER_HEADERS
from jobranger.net.retry import get_with_retry

if TYPE_CHECKING:
    import httpx

logger = logging.getLogger(__name__)

SEARCH_URL = "https://www.finn.no/job/search"
DOCS_PER_PAGE = 50
MAX_PAGES = 20
RECENT_MAX_PAGES = 50
RECENT_SORT = "PUBLISHED_DESC"
REPUBLISHED_ID_GAP = 2_000_000

APPLY_URL_PATTERN = re.compile(r'\\"applicationUrl\\",\\"(https?://[^\\"]+)')
STATE_TAG_PATTERN = re.compile(
    r"<script[^>]*data-react-query-state[^>]*>([^<]+)</script>", re.S
)
LD_JSON_PATTERN = re.compile(
    r'<script type="application/ld\+json">(.*?)</script>', re.S
)
IMPORT_DECORATION_PATTERN = re.compile(
    r'<div[^>]*class="[^"]*import-decoration[^"]*"[^>]*>(.*?)</section>', re.S
)


class FinnParseError(Exception):
    pass


@dataclass(frozen=True)
class FinnSearchPage:
    docs: list[Ad]
    match_count: int
    job_titles: dict[str, str]
    last_page: int


@dataclass(frozen=True)
class FinnRecentResult:
    new_ads: list[Ad]
    seen_ids: frozenset[str]
    window_start: str | None


@dataclass(frozen=True)
class FinnSearchResult:
    new_ads: list[Ad]
    active_ids: frozenset[str]
    complete: bool


@dataclass(frozen=True)
class FinnAdPage:
    description: str
    apply_url: str | None


def parse_search_html(html_text: str) -> FinnSearchPage:
    tag = STATE_TAG_PATTERN.search(html_text)
    if tag is None:
        msg = "data-react-query-state script tag not found"
        raise FinnParseError(msg)
    try:
        state = json.loads(base64.b64decode(tag.group(1).strip()))
        for query in state.get("queries", []):
            data = query.get("state", {}).get("data")
            if isinstance(data, dict) and "docs" in data:
                return _search_page_from(data)
    except (ValueError, KeyError, TypeError, OverflowError) as error:
        msg = f"malformed react-query state: {type(error).__name__}"
        raise FinnParseError(msg) from error
    msg = "no query with docs found in react-query state"
    raise FinnParseError(msg)


def _search_page_from(data: dict[str, Any]) -> FinnSearchPage:
    metadata = data["metadata"]
    return FinnSearchPage(
        docs=[_to_ad(doc) for doc in data["docs"]],
        match_count=metadata["result_size"]["match_count"],
        job_titles={
            f"finn:{doc['id']}": str(doc.get("job_title") or "") for doc in data["docs"]
        },
        last_page=int(metadata["paging"]["last"]),
    )


def fetch_new_ads(
    client: httpx.Client, occupations: str, known_ids: set[str]
) -> FinnSearchResult:
    new_ads: dict[str, Ad] = {}
    active_ids: set[str] = set()
    complete = False
    for page_number in range(1, MAX_PAGES + 1):
        page = _fetch_search_page(
            client, {"occupation": occupations, "page": page_number}
        )
        active_ids.update(ad.id for ad in page.docs)
        for ad in page.docs:
            if ad.id not in known_ids:
                new_ads[ad.id] = ad
        if page_number * DOCS_PER_PAGE >= page.match_count:
            complete = True
            break
    logger.info(
        "finn.search.done new=%d active=%d complete=%s",
        len(new_ads),
        len(active_ids),
        complete,
    )
    return FinnSearchResult(
        new_ads=_flag_republished(list(new_ads.values()), active_ids),
        active_ids=frozenset(active_ids),
        complete=complete,
    )


def fetch_recent_ads(
    client: httpx.Client, known_ids: set[str], since: str
) -> FinnRecentResult:
    ads: list[Ad] = []
    job_titles: dict[str, str] = {}
    for page_number in range(1, RECENT_MAX_PAGES + 1):
        page = _fetch_search_page(client, {"sort": RECENT_SORT, "page": page_number})
        ads.extend(page.docs)
        job_titles.update(page.job_titles)
        oldest = _oldest_published(ads)
        if (oldest is not None and oldest < since) or page_number >= page.last_page:
            break
    else:
        logger.warning("finn.recent.capped pages=%d since=%s", RECENT_MAX_PAGES, since)
    seen_ids = {ad.id for ad in ads}
    new_ads = _developer_ads(ads, job_titles, known_ids)
    logger.info("finn.recent.done new=%d seen=%d", len(new_ads), len(seen_ids))
    return FinnRecentResult(
        new_ads=_flag_republished(new_ads, seen_ids),
        seen_ids=frozenset(seen_ids),
        window_start=_oldest_published(ads),
    )


def _oldest_published(ads: list[Ad]) -> str | None:
    return min((ad.published for ad in ads if ad.published), default=None)


def _developer_ads(
    ads: list[Ad], job_titles: dict[str, str], known_ids: set[str]
) -> list[Ad]:
    unseen = {ad.id: ad for ad in ads if ad.id not in known_ids}
    return [
        ad
        for ad in unseen.values()
        if looks_like_developer(ad.title, job_titles[ad.id])
        and not title_is_senior(ad.title)
    ]


def _fetch_search_page(
    client: httpx.Client, params: dict[str, str | int]
) -> FinnSearchPage:
    response = get_with_retry(
        client, SEARCH_URL, params=params, headers=BROWSER_HEADERS
    )
    return parse_search_html(response.text)


def fetch_ad_page(client: httpx.Client, url: str) -> FinnAdPage:
    response = get_with_retry(client, url, headers=BROWSER_HEADERS)
    return FinnAdPage(
        description=parse_ad_html(response.text),
        apply_url=parse_apply_url(response.text),
    )


def parse_apply_url(html_text: str) -> str | None:
    match = APPLY_URL_PATTERN.search(html_text)
    return match.group(1) if match else None


def parse_ad_html(html_text: str) -> str:
    for block in LD_JSON_PATTERN.findall(html_text):
        outer = json.loads(block)
        inner = outer.get("script:ld+json") if isinstance(outer, dict) else outer
        items = inner if isinstance(inner, list) else [inner]
        for item in items:
            if isinstance(item, dict) and item.get("@type") == "JobPosting":
                description = item.get("description")
                if description:
                    return strip_html(description)
    imported = IMPORT_DECORATION_PATTERN.search(html_text)
    if imported:
        text = strip_html(imported.group(1))
        if text:
            return text
    msg = "no JobPosting ld+json or import-decoration text found"
    raise FinnParseError(msg)


def _flag_republished(ads: list[Ad], active_ids: set[str]) -> list[Ad]:
    numeric_ids = [
        int(ad_id.removeprefix("finn:"))
        for ad_id in active_ids
        if ad_id.removeprefix("finn:").isdigit()
    ]
    if not numeric_ids:
        return ads
    cutoff = max(numeric_ids) - REPUBLISHED_ID_GAP
    return [
        replace(ad, republished=True)
        if ad.source_id.isdigit() and int(ad.source_id) < cutoff
        else ad
        for ad in ads
    ]


def _to_ad(doc: dict[str, object]) -> Ad:
    source_id = str(doc["id"])
    return Ad(
        id=f"finn:{source_id}",
        source="finn",
        source_id=source_id,
        title=str(doc.get("heading", "")),
        company=str(doc.get("company_name", "")),
        location=str(doc.get("location", "")),
        published=_epoch_ms_to_iso(doc.get("published")),
        deadline=_epoch_ms_to_iso(doc.get("deadline")),
        url=str(doc.get("canonical_url", "")),
    )


def _epoch_ms_to_iso(epoch_ms: object) -> str | None:
    if not isinstance(epoch_ms, (int, float)):
        return None
    return datetime.fromtimestamp(epoch_ms / 1000, tz=UTC).isoformat()
