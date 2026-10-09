from __future__ import annotations

import httpx

from jobranger.ads.apply_link import (
    STATUS_ACTIVE,
    STATUS_EXPIRED,
    STATUS_SKIPPED,
    STATUS_UNKNOWN,
    check_apply_link,
    is_checkable,
)

HUB_URL = "https://thehub.io/jobs/abc123"
FINN_APPLY_URL = "https://www.finn.no/job-apply/1234/apply"


def _client(status: int, text: str = "") -> httpx.Client:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status, text=text)

    return httpx.Client(transport=httpx.MockTransport(respond))


def test_should_mark_404_as_expired() -> None:
    assert check_apply_link(_client(404), HUB_URL) == STATUS_EXPIRED


def test_should_mark_hub_expired_marker_as_expired() -> None:
    body = '{jobs:[{title:"Software Engineer",status:"EXPIRED",pumpedAt:"x"}]}'

    assert check_apply_link(_client(200, body), HUB_URL) == STATUS_EXPIRED


def test_should_mark_plain_200_as_active() -> None:
    body = '{jobs:[{title:"Software Engineer",status:"ACTIVE"}]}'

    assert check_apply_link(_client(200, body), HUB_URL) == STATUS_ACTIVE


def test_should_ignore_hub_marker_on_other_hosts() -> None:
    body = 'status:"EXPIRED"'

    assert check_apply_link(_client(200, body), "https://jobs.example.test/1") == (
        STATUS_ACTIVE
    )


def test_should_mark_transport_error_as_unknown() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refused", request=request)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL) == STATUS_UNKNOWN


def test_should_mark_server_error_as_unknown() -> None:
    assert check_apply_link(_client(503), HUB_URL) == STATUS_UNKNOWN


def test_should_check_external_apply_urls() -> None:
    assert is_checkable(HUB_URL) is True


def _redirecting_client(final_url: str, status: int) -> httpx.Client:
    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.host == "www.finn.no":
            return httpx.Response(302, headers={"Location": final_url})
        return httpx.Response(status)

    return httpx.Client(transport=httpx.MockTransport(respond), follow_redirects=True)


def test_should_check_finn_apply_links() -> None:
    assert is_checkable(FINN_APPLY_URL) is True


def test_should_skip_finn_ad_pages() -> None:
    assert is_checkable("https://www.finn.no/job/ad/1234") is False


def test_should_skip_apply_url_without_web_scheme() -> None:
    assert is_checkable("mailto:jobs@example.test") is False


def test_should_expire_finn_apply_link_redirecting_to_closed_job() -> None:
    client = _redirecting_client("https://example.teamtailor.com/jobs/1-developer", 410)

    assert check_apply_link(client, FINN_APPLY_URL) == STATUS_EXPIRED


def test_should_skip_finn_apply_link_redirecting_to_login() -> None:
    client = _redirecting_client("https://login.vend.no/authn/email-login", 200)

    assert check_apply_link(client, FINN_APPLY_URL) == STATUS_SKIPPED


def test_should_skip_finn_apply_link_staying_on_finn() -> None:
    assert check_apply_link(_client(200), FINN_APPLY_URL) == STATUS_SKIPPED
