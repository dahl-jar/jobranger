from __future__ import annotations

import logging
import re
from dataclasses import dataclass
from datetime import datetime
from typing import TYPE_CHECKING

import httpx

from jobranger.ads.models import Ad, strip_html
from jobranger.ads.titles import title_level
from jobranger.net.headers import BROWSER_HEADERS
from jobranger.net.retry import get_with_retry
from jobranger.sources.base import (
    FetchContext,
    SourceBatch,
    SourceError,
    advanced_cursor,
    checkpoint,
    cursor_start,
    no_checkpoint,
)

if TYPE_CHECKING:
    from collections.abc import Callable, Iterable

    from jobranger.catalogue import SourcePlan

logger = logging.getLogger(__name__)

SEARCH_URL = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search"
POSTING_URL = "https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{job_id}"
VIEW_URL = "https://www.linkedin.com/jobs/view/{job_id}"
NORWAY_GEO_ID = "103819153"
PAGE_SIZE = 10
MAX_PAGES = 10
RETRY_DELAYS = (10.0, 30.0, 60.0)
KEPT_EMPLOYMENT_TYPES = frozenset({"Full-time"})
SOURCE = "linkedin"
LINKEDIN_SINCE_KEY = "linkedin_since"
SECONDS_PER_HOUR = 3600

CARD_PATTERN = re.compile(
    r'data-entity-urn="urn:li:jobPosting:(\d+)"(.*?)(?=data-entity-urn=|\Z)', re.S
)
TITLE_PATTERN = re.compile(r'base-search-card__title">(.*?)</h3>', re.S)
COMPANY_PATTERN = re.compile(r'base-search-card__subtitle">(.*?)</h4>', re.S)
LOCATION_PATTERN = re.compile(r'job-search-card__location">(.*?)</span>', re.S)
DATE_PATTERN = re.compile(
    r'<time class="job-search-card__listdate[^"]*" datetime="([^"]+)"'
)
DESCRIPTION_PATTERN = re.compile(r"show-more-less-html__markup[^>]*>(.*?)</div>", re.S)
CRITERIA_PATTERN = re.compile(
    r'description__job-criteria-subheader">(.*?)</h3>\s*'
    r'<span class="description__job-criteria-text[^"]*">(.*?)</span>',
    re.S,
)


class LinkedInParseError(ValueError):
    pass


@dataclass(frozen=True)
class LinkedInCard:
    job_id: str
    title: str
    company: str
    location: str
    published: str | None


@dataclass(frozen=True)
class LinkedInPosting:
    description: str
    employment_type: str | None


def parse_search(page: str) -> list[LinkedInCard]:
    cards = [_card(job_id, body) for job_id, body in CARD_PATTERN.findall(page)]
    if not cards and strip_html(page):
        raise LinkedInParseError("LinkedIn search page has no job cards")
    return cards


def parse_posting(page: str) -> LinkedInPosting:
    description = DESCRIPTION_PATTERN.search(page)
    if description is None:
        raise LinkedInParseError("LinkedIn posting has no description")
    criteria = {
        strip_html(name): strip_html(value)
        for name, value in CRITERIA_PATTERN.findall(page)
    }
    return LinkedInPosting(
        strip_html(description.group(1)), criteria.get("Employment type")
    )


def fetch_new_ads(
    client: httpx.Client,
    known_ids: Iterable[str],
    window_seconds: int,
    plan: SourcePlan,
    before_request: Callable[[], None] = no_checkpoint,
) -> list[Ad]:
    known = frozenset(known_ids)
    cards = _search_all(client, window_seconds, plan, before_request)
    ads: list[Ad] = []
    for card in cards:
        ad_id = f"{SOURCE}:{card.job_id}"
        if (
            ad_id in known
            or not plan.title_matches(card.title)
            or title_level(card.title) in plan.skip_title_levels
        ):
            continue
        before_request()
        posting = _fetch_posting(client, card.job_id)
        if posting is None:
            continue
        if posting.employment_type and (
            posting.employment_type not in KEPT_EMPLOYMENT_TYPES
        ):
            logger.info(
                "linkedin.skipped id=%s employment=%s",
                card.job_id,
                posting.employment_type,
            )
            continue
        ads.append(_ad(card, posting))
    return ads


def _search_all(
    client: httpx.Client,
    window_seconds: int,
    plan: SourcePlan,
    before_request: Callable[[], None],
) -> list[LinkedInCard]:
    cards: dict[str, LinkedInCard] = {}
    for keyword in plan.linkedin_keywords:
        for page_number in range(MAX_PAGES):
            before_request()
            found = _search_page(
                client, keyword, page_number * PAGE_SIZE, window_seconds
            )
            if not found:
                break
            for card in found:
                cards.setdefault(card.job_id, card)
        else:
            logger.warning("linkedin.search.capped keyword=%s", keyword)
    return list(cards.values())


def _search_page(
    client: httpx.Client,
    keyword: str,
    start: int,
    window_seconds: int,
) -> list[LinkedInCard]:
    response = _get(
        client,
        SEARCH_URL,
        {
            "keywords": keyword,
            "geoId": NORWAY_GEO_ID,
            "f_TPR": f"r{window_seconds}",
            "start": start,
        },
    )
    return parse_search(response.text)


def _fetch_posting(client: httpx.Client, job_id: str) -> LinkedInPosting | None:
    try:
        response = _get(client, POSTING_URL.format(job_id=job_id))
        return parse_posting(response.text)
    except httpx.HTTPStatusError as error:
        if error.response.status_code == httpx.codes.TOO_MANY_REQUESTS:
            raise
        logger.warning("linkedin.posting.failed id=%s", job_id)
    except (httpx.HTTPError, LinkedInParseError):
        logger.warning("linkedin.posting.failed id=%s", job_id)
    return None


def _get(
    client: httpx.Client, url: str, params: dict[str, str | int] | None = None
) -> httpx.Response:
    return get_with_retry(
        client,
        url,
        params=params,
        headers=BROWSER_HEADERS,
        delays=RETRY_DELAYS,
    )


def _card(job_id: str, body: str) -> LinkedInCard:
    date = DATE_PATTERN.search(body)
    return LinkedInCard(
        job_id=job_id,
        title=_field(TITLE_PATTERN, body),
        company=_field(COMPANY_PATTERN, body),
        location=_field(LOCATION_PATTERN, body),
        published=date.group(1) if date else None,
    )


def _ad(card: LinkedInCard, posting: LinkedInPosting) -> Ad:
    url = VIEW_URL.format(job_id=card.job_id)
    return Ad(
        id=f"{SOURCE}:{card.job_id}",
        source=SOURCE,
        source_id=card.job_id,
        title=card.title,
        company=card.company,
        location=card.location,
        published=card.published,
        deadline=None,
        url=url,
        description=posting.description,
        apply_url=url,
    )


def _field(pattern: re.Pattern[str], body: str) -> str:
    match = pattern.search(body)
    return strip_html(match.group(1)) if match else ""


def pull(ctx: FetchContext) -> SourceBatch:
    started = datetime.fromisoformat(ctx.run_started)
    window_start = cursor_start(ctx.cursors, LINKEDIN_SINCE_KEY, started)
    window = int((started - window_start).total_seconds())
    try:
        ads = fetch_new_ads(ctx.http, ctx.known_ids, window, ctx.plan, checkpoint(ctx))
    except (httpx.HTTPError, LinkedInParseError) as error:
        logger.exception("linkedin.failed search failed")
        raise SourceError("linkedin search failed") from error
    logger.info(
        "linkedin.done new=%d window_hours=%d", len(ads), window // SECONDS_PER_HOUR
    )
    return SourceBatch(
        source=SOURCE,
        ads=tuple(ads),
        read=len(ads),
        cursors={LINKEDIN_SINCE_KEY: advanced_cursor(started)},
    )
