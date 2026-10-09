from __future__ import annotations

from datetime import UTC
from email.utils import parsedate_to_datetime
from xml.etree.ElementTree import Element, ParseError

import httpx
from defusedxml import ElementTree
from defusedxml.common import DefusedXmlException

from jobranger.ads.models import Ad, SourceSnapshot, strip_html
from jobranger.net.retry import get_with_retry
from jobranger.sources.registry import TeamtailorFeed

NORWAY_NAMES = frozenset({"norway", "norge"})


class TeamtailorParseError(ValueError):
    pass


def fetch_feed(client: httpx.Client, feed: TeamtailorFeed) -> SourceSnapshot:
    response = get_with_retry(client, feed.url)
    return parse_feed(response.text, feed)


def parse_feed(xml_text: str, feed: TeamtailorFeed) -> SourceSnapshot:
    try:
        root = ElementTree.fromstring(xml_text)
    except (ParseError, DefusedXmlException) as error:
        raise TeamtailorParseError("Teamtailor returned malformed XML") from error
    channel = root.find("channel")
    if _local_name(root.tag) != "rss" or channel is None:
        raise TeamtailorParseError("Teamtailor feed has no RSS channel")
    ads: list[Ad] = []
    for item in channel.findall("item"):
        ad = _parse_item(item, feed)
        if ad is not None:
            ads.append(ad)
    return SourceSnapshot(ads=ads, active_ids=frozenset(ad.id for ad in ads))


def _parse_item(item: Element, feed: TeamtailorFeed) -> Ad | None:
    source_id = _required_child_text(item, "guid")
    title = _required_child_text(item, "title")
    url = _required_child_text(item, "link")
    locations = _norwegian_locations(item)
    if not locations:
        return None
    description = _child_text(item, "description") or ""
    published = _published_at(_child_text(item, "pubDate"), source_id)
    return Ad(
        id=f"{feed.source}:{source_id}",
        source=feed.source,
        source_id=source_id,
        title=title,
        company=feed.company,
        location=", ".join(locations),
        published=published,
        deadline=None,
        url=url,
        description=strip_html(description),
    )


def _norwegian_locations(item: Element) -> list[str]:
    locations: list[str] = []
    for location in item.findall(".//{*}location"):
        country = _child_text(location, "country")
        if country is None or country.casefold() not in NORWAY_NAMES:
            continue
        city = _child_text(location, "city") or _child_text(location, "name")
        if city:
            locations.append(city)
    return list(dict.fromkeys(locations))


def _published_at(value: str | None, source_id: str) -> str | None:
    if value is None:
        return None
    try:
        parsed = parsedate_to_datetime(value)
    except (TypeError, ValueError) as error:
        raise TeamtailorParseError(
            f"Teamtailor job {source_id} has an invalid publication date"
        ) from error
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC).isoformat()


def _required_child_text(element: Element, name: str) -> str:
    value = _child_text(element, name)
    if value is None:
        raise TeamtailorParseError(f"Teamtailor job is missing {name}")
    return value


def _child_text(element: Element, name: str) -> str | None:
    for child in element:
        if _local_name(child.tag) != name or child.text is None:
            continue
        value = child.text.strip()
        return value or None
    return None


def _local_name(tag: str) -> str:
    return tag.rsplit("}", maxsplit=1)[-1]
