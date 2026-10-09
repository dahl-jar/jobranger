from __future__ import annotations

import logging
import re
from typing import TYPE_CHECKING

from jobranger.ads.models import Ad, strip_html
from jobranger.ads.titles import looks_like_developer
from jobranger.net.retry import get_with_retry

if TYPE_CHECKING:
    from collections.abc import Iterator

    import httpx

logger = logging.getLogger(__name__)

BASE_URL = "https://pam-stilling-feed.nav.no"
FEED_PATH = "/api/v1/feed"
LAST_PAGE_URL = f"{BASE_URL}{FEED_PATH}?last=true"
JWT_PATTERN = re.compile(r"eyJ[A-Za-z0-9_.-]+")
IT_LEVEL1 = "it"
IT_LEVEL2 = "utvikling"
STYRK_TYPE = "STYRK08"
STYRK_DEVELOPER_PREFIX = "251"


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
    client: httpx.Client, token: str, start_url: str | None
) -> Iterator[tuple[dict[str, object], str]]:
    url = _absolute(start_url) if start_url else LAST_PAGE_URL
    while url:
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


def is_developer_ad(ad_content: dict[str, object]) -> bool:
    return (
        _has_development_category(ad_content)
        or _has_developer_styrk(ad_content)
        or looks_like_developer(
            str(ad_content.get("title") or ""), str(ad_content.get("jobtitle") or "")
        )
    )


def _has_development_category(ad_content: dict[str, object]) -> bool:
    categories = ad_content.get("occupationCategories") or []
    if not isinstance(categories, list):
        return False
    return any(
        str(category.get("level1", "")).strip().lower() == IT_LEVEL1
        and str(category.get("level2", "")).strip().lower() == IT_LEVEL2
        for category in categories
        if isinstance(category, dict)
    )


def _has_developer_styrk(ad_content: dict[str, object]) -> bool:
    categories = ad_content.get("categoryList") or []
    if not isinstance(categories, list):
        return False
    return any(
        category.get("categoryType") == STYRK_TYPE
        and str(category.get("code", "")).startswith(STYRK_DEVELOPER_PREFIX)
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
