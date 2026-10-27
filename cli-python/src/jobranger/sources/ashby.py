from __future__ import annotations

import logging
from typing import TYPE_CHECKING
from urllib.parse import quote

import httpx

from jobranger.ads.models import Ad, SourceSnapshot, strip_html
from jobranger.net.retry import get_with_retry
from jobranger.sources.base import (
    NORWAY_NAMES,
    FetchContext,
    SourceBatch,
    SourceError,
    feed_batch,
)

if TYPE_CHECKING:
    from jobranger.catalogue import EmployerFeed

logger = logging.getLogger(__name__)

ASHBY_BOARD_URL = "https://api.ashbyhq.com/posting-api/job-board"


class AshbyParseError(ValueError):
    pass


def fetch_board(client: httpx.Client, board: EmployerFeed) -> SourceSnapshot:
    board_name = quote(board.board, safe="")
    response = get_with_retry(client, f"{ASHBY_BOARD_URL}/{board_name}")
    try:
        payload = response.json()
    except ValueError as error:
        raise AshbyParseError("Ashby returned invalid JSON") from error
    return parse_board(payload, board)


def parse_board(payload: object, board: EmployerFeed) -> SourceSnapshot:
    if not isinstance(payload, dict) or not isinstance(payload.get("jobs"), list):
        raise AshbyParseError("Ashby payload is missing its jobs list")
    ads: list[Ad] = []
    for value in payload["jobs"]:
        ad = _parse_job(value, board)
        if ad is not None:
            ads.append(ad)
    return SourceSnapshot(ads=ads, active_ids=frozenset(ad.id for ad in ads))


def _parse_job(value: object, board: EmployerFeed) -> Ad | None:
    if not isinstance(value, dict):
        raise AshbyParseError("Ashby job is not an object")
    source_id = _required_text(value, "id")
    title = _required_text(value, "title")
    url = _required_text(value, "jobUrl")
    if not isinstance(value.get("isListed"), bool):
        raise AshbyParseError(f"Ashby job {source_id} has no listing state")
    if value["isListed"] is False:
        return None
    locations = _norwegian_locations(value)
    if not locations:
        return None
    description_html = value.get("descriptionHtml")
    published = value.get("publishedAt")
    return Ad(
        id=f"{board.source}:{source_id}",
        source=board.source,
        source_id=source_id,
        title=title,
        company=board.company,
        location=", ".join(locations),
        published=published if isinstance(published, str) else None,
        deadline=None,
        url=url,
        description=(
            strip_html(description_html) if isinstance(description_html, str) else ""
        ),
    )


def _required_text(value: dict[object, object], key: str) -> str:
    candidate = value.get(key)
    if not isinstance(candidate, str) or not candidate.strip():
        raise AshbyParseError(f"Ashby job is missing {key}")
    return candidate.strip()


def _norwegian_locations(value: dict[object, object]) -> list[str]:
    locations: list[str] = []
    _append_norwegian_location(locations, value.get("address"))
    secondary = value.get("secondaryLocations")
    if isinstance(secondary, list):
        for item in secondary:
            if isinstance(item, dict):
                _append_norwegian_location(locations, item.get("address"))
    if not locations and _address_country(value.get("address")) in NORWAY_NAMES:
        fallback = value.get("location")
        if isinstance(fallback, str) and fallback.strip():
            locations.append(fallback.strip())
    return list(dict.fromkeys(locations))


def _append_norwegian_location(locations: list[str], address_value: object) -> None:
    if _address_country(address_value) not in NORWAY_NAMES:
        return
    locality = _address_locality(address_value)
    if locality:
        locations.append(locality)


def _postal_address(address_value: object) -> dict[object, object] | None:
    if not isinstance(address_value, dict):
        return None
    postal = address_value.get("postalAddress")
    return postal if isinstance(postal, dict) else None


def _address_country(address_value: object) -> str:
    postal = _postal_address(address_value)
    if postal is None:
        return ""
    country = postal.get("addressCountry")
    return country.strip().casefold() if isinstance(country, str) else ""


def _address_locality(address_value: object) -> str:
    postal = _postal_address(address_value)
    if postal is None:
        return ""
    locality = postal.get("addressLocality")
    return locality.strip() if isinstance(locality, str) else ""


def pull_feed(ctx: FetchContext, feed: EmployerFeed) -> SourceBatch:
    try:
        snapshot = fetch_board(ctx.http, feed)
    except (httpx.HTTPError, AshbyParseError) as error:
        logger.exception("ashby.board.failed key=%s", feed.key)
        raise SourceError(f"{feed.source} failed") from error
    logger.info("ashby.board.done key=%s active=%d", feed.key, len(snapshot.ads))
    return feed_batch(feed.source, snapshot)
