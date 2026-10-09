from __future__ import annotations

import httpx
import pytest

from jobranger.sources.nav import (
    BASE_URL,
    fetch_entry,
    fetch_public_token,
    is_developer_ad,
    iter_new_entries,
    to_ad,
)


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

    assert is_developer_ad(ad) is expected


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

    assert is_developer_ad({**ad_content, **overrides}) is True


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

    assert is_developer_ad({**ad_content, "categoryList": category_list}) is False


def test_should_return_none_for_inactive_entry() -> None:
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
