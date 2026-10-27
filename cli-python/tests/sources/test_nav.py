from __future__ import annotations

import httpx
import pytest
from factories import fetch_context, targets

from jobranger.catalogue import source_plan
from jobranger.sources import nav
from jobranger.sources.base import SourceError
from jobranger.sources.nav import (
    BASE_URL,
    fetch_entry,
    fetch_public_token,
    is_developer_ad,
    iter_new_entries,
    to_ad,
)
from jobranger.sources.nav import (
    LAST_PAGE_URL as LAST_PAGE,
)

BASE_PLAN = source_plan(targets(subfields=()))


@pytest.mark.parametrize(
    ("categories", "expected"),
    [
        (None, False),
        ([{"level1": "IT", "level2": "Utvikling"}], True),
        ([{"level1": "IT", "level2": "Drift"}], False),
    ],
    ids=["childcare ad", "it development", "it operations"],
)
def test_should_classify_ad_by_occupation_category(
    nav_entry: dict[str, object], categories: object, expected: bool
) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)
    ad = (
        ad_content
        if categories is None
        else {
            **ad_content,
            "occupationCategories": categories,
        }
    )

    assert is_developer_ad(ad, BASE_PLAN) is expected


@pytest.mark.parametrize(
    "overrides",
    [
        {"title": "Systemutvikler", "jobtitle": ""},
        {"title": "Vi søker deg", "jobtitle": "Systemutvikler"},
        {
            "title": "Kokk",
            "categoryList": [{"categoryType": "STYRK08", "code": "2512", "name": "x"}],
        },
    ],
    ids=["headline", "job title", "styrk code"],
)
def test_should_accept_developer_filed_outside_development_category(
    nav_entry: dict[str, object], overrides: dict[str, object]
) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)

    assert is_developer_ad({**ad_content, **overrides}, BASE_PLAN) is True


@pytest.mark.parametrize(
    "category_list",
    [
        [{"categoryType": "STYRK08", "code": "2521"}],
        [{"categoryType": "JANZZ", "code": "2512"}],
        "2512",
    ],
    ids=["database code", "non-STYRK code", "not a list"],
)
def test_should_reject_codes_outside_styrk_software_developers(
    nav_entry: dict[str, object], category_list: object
) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)

    assert (
        is_developer_ad({**ad_content, "categoryList": category_list}, BASE_PLAN)
        is False
    )


def test_should_return_none_when_entry_has_no_ad_content() -> None:
    inactive = {"uuid": "abc", "status": "INACTIVE", "sistEndret": "2023-06-14"}

    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=inactive)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert fetch_entry(client, "token", "abc") is None


def test_should_normalize_relative_cursor_urls() -> None:
    requested_urls: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requested_urls.append(str(request.url))
        return httpx.Response(
            200,
            json={
                "feed_url": "/api/v1/feed/current",
                "next_url": None,
                "items": [{"_feed_entry": {"uuid": "abc", "status": "ACTIVE"}}],
            },
        )

    client = httpx.Client(transport=httpx.MockTransport(respond))

    entries = list(iter_new_entries(client, "token", "/api/v1/feed/stored"))

    assert requested_urls == [f"{BASE_URL}/api/v1/feed/stored"]
    assert entries[0][1] == f"{BASE_URL}/api/v1/feed/current"


def test_should_keep_application_url(nav_entry: dict[str, object]) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)

    ad = to_ad("00000000", ad_content)

    assert ad.apply_url == "https://jobs.example.com/apply/1001?channel=NavRest&iso=no"


@pytest.mark.usefixtures("no_retry_wait")
def test_should_retry_public_token_on_503() -> None:
    statuses = iter((503,))
    attempts: list[int] = []

    def respond(request: httpx.Request) -> httpx.Response:
        attempts.append(1)
        return httpx.Response(next(statuses, 200), text='{"publicToken": "eyJabc.def"}')

    client = httpx.Client(transport=httpx.MockTransport(respond))

    token = fetch_public_token(client)

    assert token == "eyJabc.def"
    assert len(attempts) == 2


@pytest.mark.usefixtures("no_retry_wait")
def test_should_retry_nav_entry_once_on_503(nav_entry: dict[str, object]) -> None:
    responses = iter([httpx.Response(503), httpx.Response(200, json=nav_entry)])
    client = httpx.Client(
        transport=httpx.MockTransport(lambda request: next(responses))
    )

    content = fetch_entry(client, "token", "u1")

    assert content == nav_entry["ad_content"]


def test_should_accept_nav_ad_with_styrk_from_plan(
    nav_entry: dict[str, object],
) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)
    frontend = {
        **ad_content,
        "title": "Kokk",
        "categoryList": [{"categoryType": "STYRK08", "code": "2513"}],
    }

    plan = source_plan(targets(subfields=("frontend",)))

    assert is_developer_ad(frontend, plan) is True


def _feed(
    entries: list[dict[str, object]],
    content: dict[str, object],
    fail_entry: bool = False,
) -> tuple[httpx.Client, list[str]]:
    fetched: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        path = request.url.path
        if path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        if "/feedentry/" in path:
            fetched.append(path.rsplit("/", 1)[-1])
            return httpx.Response(200, json={"ad_content": content})
        if fail_entry:
            return httpx.Response(404)
        return httpx.Response(
            200, json={"feed_url": "/api/v1/feed/page-1", "items": entries}
        )

    return httpx.Client(transport=httpx.MockTransport(respond)), fetched


def _entry(uuid: str, status: str = "ACTIVE") -> dict[str, object]:
    return {"_feed_entry": {"uuid": uuid, "status": status}}


def test_should_fetch_repeated_nav_uuid_once(nav_entry: dict[str, object]) -> None:
    content = {**nav_entry["ad_content"], "title": "Utvikler"}
    client, fetched = _feed([_entry("u1"), _entry("u1")], content)

    nav.pull(fetch_context(client))

    assert fetched == ["u1"]


def test_should_record_inactive_and_active_entries(
    nav_entry: dict[str, object],
) -> None:
    client, _ = _feed(
        [_entry("u1", "INACTIVE"), _entry("u2")], dict(nav_entry["ad_content"])
    )

    batch = nav.pull(fetch_context(client, known_ids=frozenset({"nav:u2"})))

    assert (dict(batch.active), batch.cursors) == (
        {"nav:u1": False, "nav:u2": True},
        {nav.NAV_POSITION_KEY: f"{BASE_URL}/api/v1/feed/page-1"},
    )


def test_should_keep_stored_cursor_when_feed_fails(
    nav_entry: dict[str, object],
) -> None:
    client, _ = _feed([], dict(nav_entry["ad_content"]), fail_entry=True)
    cursor = f"{BASE_URL}/api/v1/feed/stored"

    with pytest.raises(SourceError) as raised:
        nav.pull(fetch_context(client, cursors={nav.NAV_POSITION_KEY: cursor}))

    partial = raised.value.partial
    assert partial is not None
    assert partial.cursors == {nav.NAV_POSITION_KEY: cursor}


def test_should_return_new_developer_ads_with_entries_read(
    nav_entry: dict[str, object],
) -> None:
    content = {**nav_entry["ad_content"], "title": "Utvikler"}
    client, _ = _feed([_entry("u1"), _entry("u2", "INACTIVE")], content)

    batch = nav.pull(fetch_context(client))

    assert (batch.source, [ad.id for ad in batch.ads], batch.read) == (
        "nav",
        ["nav:u1"],
        2,
    )


def test_should_skip_entry_without_feed_entry_or_uuid(
    nav_entry: dict[str, object],
) -> None:
    content = {**nav_entry["ad_content"], "title": "Utvikler"}
    entries = [{"other": 1}, {"_feed_entry": {"status": "ACTIVE"}}]
    client, fetched = _feed(entries, content)

    batch = nav.pull(fetch_context(client))

    assert (fetched, batch.ads, dict(batch.active)) == ([], (), {})


def test_should_send_bearer_token_to_feed_and_entries(
    nav_entry: dict[str, object],
) -> None:
    headers: list[str | None] = []

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        headers.append(request.headers.get("Authorization"))
        if "/feedentry/" in request.url.path:
            return httpx.Response(200, json={"ad_content": nav_entry["ad_content"]})
        return httpx.Response(200, json={"items": [_entry("u1")]})

    nav.pull(fetch_context(httpx.Client(transport=httpx.MockTransport(respond))))

    assert headers == ["Bearer eyJabc.def", "Bearer eyJabc.def"]


def test_should_follow_next_url_to_following_page() -> None:
    requested: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requested.append(str(request.url))
        if request.url.path.endswith("/next"):
            return httpx.Response(200, json={"items": []})
        return httpx.Response(200, json={"items": [], "next_url": "/api/v1/feed/next"})

    client = httpx.Client(transport=httpx.MockTransport(respond))

    list(iter_new_entries(client, "token", None))

    assert requested == [LAST_PAGE, f"{BASE_URL}/api/v1/feed/next"]


def test_should_skip_new_entry_that_is_not_developer_work(
    nav_entry: dict[str, object],
) -> None:
    content = {**nav_entry["ad_content"], "title": "Kokk", "jobtitle": "Kokk"}
    client, fetched = _feed([_entry("u1")], content)

    batch = nav.pull(fetch_context(client, plan=BASE_PLAN))

    assert (fetched, batch.ads) == (["u1"], ())


def test_should_start_feed_at_stored_cursor() -> None:
    requested: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.path.endswith("publicToken"):
            return httpx.Response(200, text="eyJabc.def")
        requested.append(str(request.url))
        return httpx.Response(200, json={"items": []})

    cursor = f"{BASE_URL}/api/v1/feed/stored"

    nav.pull(
        fetch_context(
            httpx.Client(transport=httpx.MockTransport(respond)),
            cursors={nav.NAV_POSITION_KEY: cursor},
        )
    )

    assert requested == [cursor]


def test_should_ignore_category_field_that_is_not_a_list(
    nav_entry: dict[str, object],
) -> None:
    ad_content = nav_entry["ad_content"]
    assert isinstance(ad_content, dict)

    assert (
        is_developer_ad({**ad_content, "occupationCategories": "IT"}, BASE_PLAN)
        is False
    )
