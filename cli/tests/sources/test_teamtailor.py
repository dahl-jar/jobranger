from __future__ import annotations

import httpx
import pytest
from factories import FIXTURES, TEAMTAILOR_FEED, fetch_context

from jobranger.sources import teamtailor
from jobranger.sources.teamtailor import TeamtailorParseError, fetch_feed, parse_feed

FEED = TEAMTAILOR_FEED


def test_should_parse_norwegian_rss_jobs() -> None:
    xml_text = (FIXTURES / "teamtailor_jobs.xml").read_text()

    snapshot = parse_feed(xml_text, FEED)

    assert [ad.id for ad in snapshot.ads] == ["teamtailor:example:100"]
    assert snapshot.active_ids == frozenset({"teamtailor:example:100"})
    ad = snapshot.ads[0]
    assert ad.source == "teamtailor:example"
    assert ad.source_id == "100"
    assert ad.title == "Graduate Developer"
    assert ad.company == "Example AS"
    assert ad.location == "Oslo"
    assert ad.published == "2026-08-24T06:15:00+00:00"
    assert ad.url == "https://careers.example.test/jobs/100-graduate-developer"
    assert ad.description == "Build Java services."


def test_should_reject_malformed_rss() -> None:
    with pytest.raises(TeamtailorParseError):
        parse_feed("<rss><broken>", FEED)


def test_should_reject_item_without_stable_identity() -> None:
    xml_text = """<rss><channel><item><title>Developer</title></item></channel></rss>"""

    with pytest.raises(TeamtailorParseError):
        parse_feed(xml_text, FEED)


def test_should_reject_xml_entities() -> None:
    xml_text = """<!DOCTYPE rss [<!ENTITY company "Example">]>
    <rss><channel><title>&company;</title></channel></rss>"""

    with pytest.raises(TeamtailorParseError):
        parse_feed(xml_text, FEED)


def test_should_reject_invalid_publication_date() -> None:
    xml_text = """<rss xmlns:tt="https://teamtailor.com"><channel><item>
    <title>Developer</title><link>https://example.test/1</link><guid>1</guid>
    <pubDate>invalid</pubDate><tt:locations><tt:location>
    <tt:city>Oslo</tt:city><tt:country>Norway</tt:country>
    </tt:location></tt:locations></item></channel></rss>"""

    with pytest.raises(TeamtailorParseError):
        parse_feed(xml_text, FEED)


@pytest.mark.usefixtures("no_retry_wait")
def test_should_retry_teamtailor_feed_once_on_503() -> None:
    responses = iter(
        [
            httpx.Response(503),
            httpx.Response(200, text=(FIXTURES / "teamtailor_jobs.xml").read_text()),
        ]
    )
    client = httpx.Client(
        transport=httpx.MockTransport(lambda request: next(responses))
    )

    snapshot = fetch_feed(client, FEED)

    assert [ad.id for ad in snapshot.ads] == ["teamtailor:example:100"]


def test_should_list_feed_ids_for_active_sync() -> None:
    client = httpx.Client(
        transport=httpx.MockTransport(
            lambda request: httpx.Response(
                200, text=(FIXTURES / "teamtailor_jobs.xml").read_text()
            )
        )
    )

    batch = teamtailor.pull_feed(fetch_context(client), FEED)

    assert (batch.source, batch.listed_ids) == (
        "teamtailor:example",
        frozenset({"teamtailor:example:100"}),
    )
