from __future__ import annotations

from collections.abc import Callable

import httpx
import pytest
from factories import FIXTURES, make_search_doc, make_search_html

from jobranger.net.headers import USER_AGENT
from jobranger.sources import finn
from jobranger.sources.finn import (
    FinnParseError,
    fetch_ad_page,
    fetch_new_ads,
    fetch_recent_ads,
    parse_ad_html,
    parse_apply_url,
    parse_search_html,
)


def test_should_parse_search_results(finn_search_html: str) -> None:
    page = parse_search_html(finn_search_html)

    assert page.match_count == 542
    assert len(page.docs) == 50
    first = page.docs[0]
    assert first.source_id == "400000001"
    assert first.title == "Utvikler 1"
    assert first.company == "Org One AS"
    assert first.url == "https://www.finn.no/job/ad/400000001"


def test_should_raise_when_search_html_has_no_state_tag() -> None:
    with pytest.raises(FinnParseError):
        parse_search_html("<html><body>Ingen treff</body></html>")


def test_should_collect_complete_active_snapshot(
    finn_search_html: str,
) -> None:
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(str(request.url))
        return httpx.Response(200, text=finn_search_html)

    client = httpx.Client(transport=httpx.MockTransport(respond))
    page = parse_search_html(finn_search_html)
    known_ids = {ad.id for ad in page.docs}

    result = fetch_new_ads(client, "0.23", known_ids)

    assert result.new_ads == []
    assert result.active_ids == frozenset(known_ids)
    assert result.complete is True
    assert len(requests) == 11


def test_should_return_plain_text_from_ad_html(finn_ad_html: str) -> None:
    text = parse_ad_html(finn_ad_html)

    assert "Shopify" in text
    assert "<p" not in text
    assert len(text) > 4000


def test_should_fall_back_to_imported_ad_text() -> None:
    text = parse_ad_html((FIXTURES / "finn_ad_imported.html").read_text())

    assert "masteroppgave" in text
    assert "<p" not in text
    assert len(text) > 3000


@pytest.mark.usefixtures("no_retry_wait")
def test_should_retry_search_page_on_429(finn_search_html: str) -> None:
    statuses = iter((429,))
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(str(request.url))
        return httpx.Response(next(statuses, 200), text=finn_search_html)

    client = httpx.Client(transport=httpx.MockTransport(respond))
    known_ids = {ad.id for ad in parse_search_html(finn_search_html).docs}

    result = fetch_new_ads(client, "0.23", known_ids)

    assert result.complete is True
    assert len(requests) == 12


SINCE = "2026-10-06T00:00:00+00:00"
NEW_MS = 1791331200000
OLD_MS = 1791072000000


@pytest.mark.parametrize(
    "fetch",
    [
        lambda client: fetch_new_ads(client, "0.23", set()),
        lambda client: fetch_recent_ads(client, set(), SINCE),
    ],
    ids=["category scan", "recent scan"],
)
def test_should_flag_republished_ad_far_below_current_ids(
    fetch: Callable[[httpx.Client], finn.FinnSearchResult | finn.FinnRecentResult],
) -> None:
    docs = [
        make_search_doc(476500000),
        make_search_doc(476400000),
        make_search_doc(415573777),
    ]
    client, _ = _serve(lambda page: docs, last_page=1)

    result = fetch(client)

    republished = {ad.source_id for ad in result.new_ads if ad.republished}
    assert republished == {"415573777"}


def test_should_raise_parse_error_for_doc_without_id() -> None:
    html_text = make_search_html([{"heading": "Utvikler"}], 1)

    with pytest.raises(FinnParseError):
        parse_search_html(html_text)


def test_should_keep_only_unseen_developer_ads_from_recent_scan() -> None:
    docs = [
        make_search_doc(1, heading="Er du klar for å bytte rede?", job_title="Data"),
        make_search_doc(2, heading="Kokk", job_title="Kokk"),
        make_search_doc(3, heading="Backend-utvikler", job_title="Utvikler"),
    ]
    docs = [{**doc, "published": NEW_MS} for doc in docs]
    client, requests = _serve(lambda page: docs, last_page=1)

    result = fetch_recent_ads(client, {"finn:3"}, SINCE)

    assert [ad.id for ad in result.new_ads] == ["finn:1"]
    assert result.seen_ids == frozenset({"finn:1", "finn:2", "finn:3"})
    assert len(requests) == 1


def test_should_drop_senior_titles_from_recent_scan() -> None:
    docs = [
        make_search_doc(1, heading="Senior backend-utvikler", job_title="Utvikler"),
        make_search_doc(2, heading="Backend-utvikler", job_title="Utvikler"),
    ]
    docs = [{**doc, "published": NEW_MS} for doc in docs]
    client, _ = _serve(lambda page: docs, last_page=1)

    result = fetch_recent_ads(client, set(), SINCE)

    assert [ad.id for ad in result.new_ads] == ["finn:2"]


def test_should_stop_recent_scan_at_page_older_than_cutoff() -> None:
    published = {1: NEW_MS, 2: OLD_MS, 3: OLD_MS}
    client, requests = _serve(
        lambda page: [make_search_doc(page, published=published[page])], last_page=3
    )

    result = fetch_recent_ads(client, set(), SINCE)

    assert [request.url.params["sort"] for request in requests] == [
        "PUBLISHED_DESC",
        "PUBLISHED_DESC",
    ]
    assert result.window_start == "2026-10-04T00:00:00+00:00"


def test_should_send_browser_user_agent_with_recent_scan() -> None:
    client, requests = _serve(lambda page: [], last_page=1)

    fetch_recent_ads(client, set(), SINCE)

    assert requests[0].headers["User-Agent"] == USER_AGENT


def test_should_return_empty_recent_scan_for_page_without_ads() -> None:
    client, _ = _serve(lambda page: [], last_page=1)

    result = fetch_recent_ads(client, set(), SINCE)

    assert result.window_start is None
    assert result.new_ads == []


def test_should_stop_recent_scan_at_page_cap() -> None:
    client, requests = _serve(
        lambda page: [make_search_doc(page, published=NEW_MS)], last_page=999
    )

    fetch_recent_ads(client, set(), SINCE)

    assert len(requests) == 50


def _serve(
    docs_for_page: Callable[[int], list[dict[str, object]]], last_page: int
) -> tuple[httpx.Client, list[httpx.Request]]:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        docs = docs_for_page(int(request.url.params["page"]))
        return httpx.Response(
            200, text=make_search_html(docs, len(docs), last_page=last_page)
        )

    return httpx.Client(transport=httpx.MockTransport(respond)), requests


def test_should_parse_external_apply_url() -> None:
    flight = (
        'self.__next_f.push([1,"\\"applicationDeadline\\",null,'
        '\\"applicationUrl\\",\\"https://thehub.io/jobs/abc123\\",\\"company\\""])'
    )

    assert parse_apply_url(flight) == "https://thehub.io/jobs/abc123"


def test_should_return_none_when_page_has_no_apply_url() -> None:
    assert parse_apply_url("<html>no apply link</html>") is None


def test_should_return_description_and_apply_url_from_ad_page() -> None:
    page_html = (FIXTURES / "finn_ad_imported.html").read_text() + (
        '<script>\\"applicationUrl\\",\\"https://thehub.io/jobs/abc123\\"</script>'
    )

    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, text=page_html)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    page = fetch_ad_page(client, "https://www.finn.no/job/ad/1")

    assert "masteroppgave" in page.description
    assert page.apply_url == "https://thehub.io/jobs/abc123"
