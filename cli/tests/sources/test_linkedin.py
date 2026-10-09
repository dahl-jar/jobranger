from __future__ import annotations

from typing import TYPE_CHECKING

import httpx
import pytest
from factories import FIXTURES

from jobranger.sources import linkedin
from jobranger.sources.linkedin import (
    LinkedInCard,
    LinkedInParseError,
    LinkedInPosting,
    fetch_new_ads,
    parse_posting,
    parse_search,
)

if TYPE_CHECKING:
    from collections.abc import Callable

SEARCH_HTML = (FIXTURES / "linkedin_search.html").read_text()
POSTING_HTML = (FIXTURES / "linkedin_posting.html").read_text()
EMPTY_PAGE = "<!DOCTYPE html>\n\n<!---->  "
WEEK_SECONDS = 604_800


pytestmark = pytest.mark.usefixtures("no_retry_wait")


def test_should_parse_cards_from_search_page() -> None:
    cards = parse_search(SEARCH_HTML)

    assert cards == [
        LinkedInCard(
            "4000000001",
            "Software Engineer & Platform",
            "Org One AS",
            "Oslo, Oslo, Norway",
            "2026-10-08",
        ),
        LinkedInCard(
            "4000000002",
            "Sales Assistant",
            "Org Two AS",
            "Bergen, Vestland, Norway",
            "2026-10-06",
        ),
        LinkedInCard("4000000003", "Utvikler", "Org Two AS", "Norway", None),
    ]


def test_should_return_no_cards_for_empty_page() -> None:
    assert parse_search(EMPTY_PAGE) == []


def test_should_raise_parse_error_for_page_without_cards_markup() -> None:
    with pytest.raises(LinkedInParseError):
        parse_search("<html><body>Sign in to see more jobs</body></html>")


def test_should_parse_posting_description_with_employment_type() -> None:
    posting = parse_posting(POSTING_HTML)

    assert posting == LinkedInPosting(
        "About Org One: Build backend services in Java & Kotlin. "
        "Bachelor's degree in computer science 0-2 years of experience",
        "Full-time",
    )


def test_should_raise_parse_error_for_posting_without_description() -> None:
    with pytest.raises(LinkedInParseError):
        parse_posting("<html><body>Sign in</body></html>")


def test_should_build_ads_for_new_developer_cards() -> None:
    client, _ = _serve(_one_page, lambda job_id: POSTING_HTML)

    ads = fetch_new_ads(client, set(), WEEK_SECONDS)

    assert [ad.id for ad in ads] == ["linkedin:4000000001", "linkedin:4000000003"]
    first = ads[0]
    assert first.title == "Software Engineer & Platform"
    assert first.source == "linkedin"
    assert first.source_id == "4000000001"
    assert first.company == "Org One AS"
    assert first.location == "Oslo, Oslo, Norway"
    assert first.published == "2026-10-08"
    assert first.url == "https://www.linkedin.com/jobs/view/4000000001"
    assert first.apply_url == first.url
    assert first.description.startswith("About Org One")


def test_should_send_search_filters_for_every_keyword() -> None:
    client, requests = _serve(_one_page, lambda job_id: POSTING_HTML)

    fetch_new_ads(client, set(), 172_800)

    searches = [r for r in requests if r.url.path.endswith("/search")]
    first = searches[0].url.params
    assert first["geoId"] == "103819153"
    assert first["f_TPR"] == "r172800"
    assert first["start"] == "0"
    assert {r.url.params["keywords"] for r in searches} == set(linkedin.SEARCH_KEYWORDS)


def test_should_stop_keyword_at_empty_page() -> None:
    client, requests = _serve(
        lambda keyword, start: SEARCH_HTML if start < 20 else EMPTY_PAGE,
        lambda job_id: POSTING_HTML,
    )

    fetch_new_ads(client, set(), WEEK_SECONDS)

    keyword = linkedin.SEARCH_KEYWORDS[0]
    starts = [
        r.url.params["start"]
        for r in requests
        if r.url.path.endswith("/search") and r.url.params["keywords"] == keyword
    ]
    assert starts == ["0", "10", "20"]


def test_should_stop_keyword_at_page_cap(caplog: pytest.LogCaptureFixture) -> None:
    client, requests = _serve(
        lambda keyword, start: SEARCH_HTML, lambda job_id: POSTING_HTML
    )

    fetch_new_ads(client, set(), WEEK_SECONDS)

    keyword = linkedin.SEARCH_KEYWORDS[0]
    pages = [
        r
        for r in requests
        if r.url.path.endswith("/search") and r.url.params["keywords"] == keyword
    ]
    assert len(pages) == linkedin.MAX_PAGES
    assert f"linkedin.search.capped keyword={keyword}" in caplog.text


def test_should_skip_posting_fetch_when_id_is_known() -> None:
    client, requests = _serve(_one_page, lambda job_id: POSTING_HTML)

    ads = fetch_new_ads(client, {"linkedin:4000000001"}, WEEK_SECONDS)

    assert [ad.id for ad in ads] == ["linkedin:4000000003"]
    assert "4000000001" not in _posting_ids(requests)


def test_should_skip_posting_fetch_when_title_is_not_developer() -> None:
    client, requests = _serve(_one_page, lambda job_id: POSTING_HTML)

    fetch_new_ads(client, set(), WEEK_SECONDS)

    assert "4000000002" not in _posting_ids(requests)


def test_should_skip_posting_fetch_when_title_is_senior() -> None:
    page = SEARCH_HTML.replace("Utvikler", "Senior utvikler")
    client, requests = _serve(
        lambda keyword, start: page if start == 0 else EMPTY_PAGE,
        lambda job_id: POSTING_HTML,
    )

    fetch_new_ads(client, set(), WEEK_SECONDS)

    assert "4000000003" not in _posting_ids(requests)


def test_should_drop_posting_when_employment_type_is_contract() -> None:
    posting = POSTING_HTML.replace("Full-time", "Contract")
    client, _ = _serve(_one_page, lambda job_id: posting)

    ads = fetch_new_ads(client, set(), WEEK_SECONDS)

    assert ads == []


def test_should_keep_posting_without_employment_type() -> None:
    posting = POSTING_HTML.replace("Employment type", "Job function")
    client, _ = _serve(_one_page, lambda job_id: posting)

    ads = fetch_new_ads(client, set(), WEEK_SECONDS)

    assert len(ads) == 2


def test_should_skip_posting_when_fetch_returns_404() -> None:
    def respond(job_id: str) -> str | int:
        return 404 if job_id == "4000000001" else POSTING_HTML

    client, _ = _serve(_one_page, respond)

    ads = fetch_new_ads(client, set(), WEEK_SECONDS)

    assert [ad.id for ad in ads] == ["linkedin:4000000003"]


def test_should_send_browser_user_agent() -> None:
    client, requests = _serve(_one_page, lambda job_id: POSTING_HTML)

    fetch_new_ads(client, set(), WEEK_SECONDS)

    assert all("Mozilla" in r.headers["User-Agent"] for r in requests)


def test_should_skip_posting_when_page_has_no_description() -> None:
    def respond(job_id: str) -> str:
        return (
            "<html><body>Sign in</body></html>"
            if job_id == "4000000001"
            else POSTING_HTML
        )

    client, _ = _serve(_one_page, respond)

    ads = fetch_new_ads(client, set(), WEEK_SECONDS)

    assert [ad.id for ad in ads] == ["linkedin:4000000003"]


def test_should_fail_search_when_posting_is_rate_limited() -> None:
    client, _ = _serve(_one_page, lambda job_id: 429)

    with pytest.raises(httpx.HTTPStatusError):
        fetch_new_ads(client, set(), WEEK_SECONDS)


def _one_page(keyword: str, start: int) -> str:
    return SEARCH_HTML if start == 0 else EMPTY_PAGE


def _posting_ids(requests: list[httpx.Request]) -> list[str]:
    return [
        r.url.path.rsplit("/", 1)[-1] for r in requests if "/jobPosting/" in r.url.path
    ]


def _serve(
    search: Callable[[str, int], str],
    posting: Callable[[str], str | int],
) -> tuple[httpx.Client, list[httpx.Request]]:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        if request.url.path.endswith("/search"):
            params = request.url.params
            return httpx.Response(
                200, text=search(params["keywords"], int(params["start"]))
            )
        result = posting(request.url.path.rsplit("/", 1)[-1])
        if isinstance(result, int):
            return httpx.Response(result)
        return httpx.Response(200, text=result)

    return httpx.Client(transport=httpx.MockTransport(respond)), requests
