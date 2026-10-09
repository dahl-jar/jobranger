from __future__ import annotations

from collections.abc import Callable

import httpx
import pytest
from factories import (
    FIXTURES,
    fetch_context,
    make_ad,
    make_search_doc,
    make_search_html,
    targets,
)

from jobranger.catalogue import source_plan
from jobranger.events import CancelToken
from jobranger.net.headers import USER_AGENT
from jobranger.sources import finn
from jobranger.sources.base import SourceCancelled, SourceError, checkpoint
from jobranger.sources.finn import (
    FINN_RECENT_KEY,
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

    result = fetch_new_ads(client, OCCUPATIONS, known_ids)

    assert result.new_ads == []
    assert result.active_ids == frozenset(known_ids)
    assert result.complete is True
    assert len(requests) == 9


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

    result = fetch_new_ads(client, OCCUPATIONS, known_ids)

    assert result.complete is True
    assert len(requests) == 10


SINCE = "2026-10-06T00:00:00+00:00"
OCCUPATIONS = ("0.23",)
PLAN = source_plan(targets())
NEW_MS = 1791331200000
OLD_MS = 1791072000000


@pytest.mark.parametrize(
    "fetch",
    [
        lambda client: fetch_new_ads(client, OCCUPATIONS, set()),
        lambda client: fetch_recent_ads(client, set(), SINCE, PLAN),
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

    result = fetch_recent_ads(client, {"finn:3"}, SINCE, PLAN)

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

    result = fetch_recent_ads(client, set(), SINCE, PLAN)

    assert [ad.id for ad in result.new_ads] == ["finn:2"]


def test_should_stop_recent_scan_at_page_older_than_cutoff() -> None:
    published = {1: NEW_MS, 2: OLD_MS, 3: OLD_MS}
    client, requests = _serve(
        lambda page: [make_search_doc(page, published=published[page])], last_page=3
    )

    result = fetch_recent_ads(client, set(), SINCE, PLAN)

    assert [request.url.params["sort"] for request in requests] == [
        "PUBLISHED_DESC",
        "PUBLISHED_DESC",
    ]
    assert result.window_start == "2026-10-04T00:00:00+00:00"


def test_should_send_browser_user_agent_with_recent_scan() -> None:
    client, requests = _serve(lambda page: [], last_page=1)

    fetch_recent_ads(client, set(), SINCE, PLAN)

    assert requests[0].headers["User-Agent"] == USER_AGENT


def test_should_return_empty_recent_scan_for_page_without_ads() -> None:
    client, _ = _serve(lambda page: [], last_page=1)

    result = fetch_recent_ads(client, set(), SINCE, PLAN)

    assert result.window_start is None
    assert result.new_ads == []


def test_should_stop_recent_scan_at_page_cap() -> None:
    client, requests = _serve(
        lambda page: [make_search_doc(page, published=NEW_MS)], last_page=999
    )

    fetch_recent_ads(client, set(), SINCE, PLAN)

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


def test_should_stop_finn_search_at_last_page_when_match_count_is_stale() -> None:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        docs = [make_search_doc(index) for index in range(1, 51)]
        return httpx.Response(200, text=make_search_html(docs, 500, last_page=1))

    client = httpx.Client(transport=httpx.MockTransport(respond))

    fetch_new_ads(client, OCCUPATIONS, set())

    assert len(requests) == 1


def test_should_request_every_configured_occupation_code() -> None:
    client, requests = _serve(lambda page: [], last_page=1)

    fetch_new_ads(client, ("0.23", "0.22"), set())

    assert requests[0].url.params.get_list("occupation") == ["0.23", "0.22"]


APPLY_URL = "https://jobs.example.test/apply/1"
AD_PAGE = (FIXTURES / "finn_ad_imported.html").read_text() + (
    f'<script>\\"applicationUrl\\",\\"{APPLY_URL}\\"</script>'
)


def _finn_site(
    recent_docs: list[dict[str, object]],
    category_docs: list[dict[str, object]],
    *,
    fail: str | None = None,
    recent_last: int = 1,
) -> tuple[httpx.Client, list[httpx.Request]]:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        if request.url.path.startswith("/job/ad/"):
            return httpx.Response(200, text=AD_PAGE)
        recent = "sort" in request.url.params
        if fail == ("recent" if recent else "category"):
            return httpx.Response(404)
        docs = recent_docs if recent else category_docs
        last = recent_last if recent else 1
        return httpx.Response(
            200, text=make_search_html(docs, len(docs), last_page=last)
        )

    return httpx.Client(transport=httpx.MockTransport(respond)), requests


@pytest.mark.parametrize(
    ("cursors", "recent_pages"),
    [({}, 2), ({FINN_RECENT_KEY: SINCE}, 1)],
    ids=["first run", "later run"],
)
def test_should_scan_recent_ads_from_stored_cursor(
    cursors: dict[str, str], recent_pages: int
) -> None:
    client, requests = _finn_site(
        [make_search_doc(1, published=OLD_MS)], [make_search_doc(2)], recent_last=2
    )

    batch = finn.pull(fetch_context(client, cursors=cursors))

    assert sum("sort" in request.url.params for request in requests) == recent_pages
    assert batch.cursors == {FINN_RECENT_KEY: "2026-10-06T03:00:00+00:00"}


def test_should_mark_recent_ads_category_unlisted() -> None:
    recent = make_search_doc(7, heading="Backend-utvikler", published=NEW_MS)
    client, _ = _finn_site([recent], [make_search_doc(2)])

    batch = finn.pull(fetch_context(client))

    assert batch.finn_sync is not None
    assert batch.finn_sync.unlisted_ids == ("finn:7",)


def test_should_skip_known_ids_in_category_scan() -> None:
    client, _ = _finn_site([], [make_search_doc(2), make_search_doc(3)])

    batch = finn.pull(fetch_context(client, known_ids=frozenset({"finn:2"})))

    assert [ad.id for ad in batch.ads] == ["finn:3"]


def test_should_keep_category_results_when_recent_scan_fails() -> None:
    client, _ = _finn_site([], [make_search_doc(2)], fail="recent")

    with pytest.raises(SourceError) as raised:
        finn.pull(fetch_context(client))

    partial = raised.value.partial
    assert partial is not None
    assert ([ad.id for ad in partial.ads], partial.cursors) == (["finn:2"], {})


def test_should_fail_without_sync_when_category_scan_is_empty() -> None:
    client, _ = _finn_site([], [])

    with pytest.raises(SourceError) as raised:
        finn.pull(fetch_context(client))

    partial = raised.value.partial
    assert partial is not None
    assert partial.finn_sync is not None
    assert partial.finn_sync.complete is False


def test_should_fetch_page_text_and_apply_url_for_new_ads() -> None:
    client, _ = _finn_site([], [make_search_doc(2)])

    batch = finn.pull(fetch_context(client))

    [page] = batch.pages
    assert (page.ad_id, page.apply_url) == ("finn:2", APPLY_URL)
    assert page.description is not None


def test_should_backfill_pages_for_pending_ads() -> None:
    client, _ = _finn_site([], [make_search_doc(2)])
    pending = make_ad(id="finn:9", source_id="9", url="https://www.finn.no/job/ad/9")

    batch = finn.pull(fetch_context(client, pending_pages=(pending,)))

    assert {page.ad_id for page in batch.pages} == {"finn:2", "finn:9"}


def test_should_skip_page_fetch_for_skipped_title_level() -> None:
    senior = make_search_doc(2, heading="Senior utvikler")
    client, requests = _finn_site([], [senior])

    batch = finn.pull(fetch_context(client))

    assert [(page.ad_id, page.label) for page in batch.pages] == [("finn:2", "senior")]
    assert not any(request.url.path.startswith("/job/ad/") for request in requests)


def test_should_keep_recent_ads_when_category_scan_fails() -> None:
    recent = make_search_doc(7, heading="Backend-utvikler", published=NEW_MS)
    client, _ = _finn_site([recent], [], fail="category")

    with pytest.raises(SourceError) as raised:
        finn.pull(fetch_context(client))

    partial = raised.value.partial
    assert partial is not None
    assert ([ad.id for ad in partial.ads], partial.source, partial.read) == (
        ["finn:7"],
        "finn",
        1,
    )


def test_should_count_recent_and_category_ads_read() -> None:
    recent = make_search_doc(7, heading="Backend-utvikler", published=NEW_MS)
    client, _ = _finn_site([recent], [make_search_doc(2), make_search_doc(3)])

    batch = finn.pull(fetch_context(client))

    assert (batch.source, batch.read) == ("finn", 3)


def test_should_mark_scan_incomplete_at_page_cap() -> None:
    client, requests = _serve_count(match_count=10_000, last_page=999)

    result = fetch_new_ads(client, OCCUPATIONS, set())

    assert (result.complete, len(requests)) == (False, 20)


def test_should_stop_when_page_reaches_match_count() -> None:
    client, requests = _serve_count(match_count=50, last_page=5)

    result = fetch_new_ads(client, OCCUPATIONS, set())

    assert (result.complete, len(requests)) == (True, 1)


@pytest.mark.parametrize("scan", ["recent", "category"])
def test_should_send_no_request_when_cancelled_before_scan(scan: str) -> None:
    client, requests = _finn_site([], [make_search_doc(2)])
    cancel = CancelToken()
    cancel.cancel()
    ctx = fetch_context(client, cancel=cancel)
    started = (
        (lambda: fetch_recent_ads(client, set(), SINCE, PLAN, checkpoint(ctx)))
        if scan == "recent"
        else (lambda: fetch_new_ads(client, OCCUPATIONS, set(), checkpoint(ctx)))
    )

    with pytest.raises(SourceCancelled):
        started()

    assert requests == []


def _serve_count(
    match_count: int, last_page: int
) -> tuple[httpx.Client, list[httpx.Request]]:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        docs = [make_search_doc(len(requests) * 100 + index) for index in range(50)]
        return httpx.Response(
            200, text=make_search_html(docs, match_count, last_page=last_page)
        )

    return httpx.Client(transport=httpx.MockTransport(respond)), requests
