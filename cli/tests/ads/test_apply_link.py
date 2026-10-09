from __future__ import annotations

from typing import TYPE_CHECKING

import httpx
import pytest
from factories import FakeClock

from jobranger.ads.apply_link import (
    APPLY_CHECK_DEADLINE_SECONDS,
    MAX_BODY_BYTES,
    STATUS_ACTIVE,
    STATUS_BLOCKED,
    STATUS_EXPIRED,
    STATUS_SKIPPED,
    STATUS_UNKNOWN,
    check_apply_link,
    is_checkable,
    is_public_address,
    is_public_url,
    resolve_host,
)
from jobranger.net.headers import USER_AGENT

if TYPE_CHECKING:
    from collections.abc import Callable, Iterator

HUB_URL = "https://thehub.io/jobs/abc123"
FINN_APPLY_URL = "https://www.finn.no/job-apply/1234/apply"
PUBLIC_ADDRESS = "93.184.215.14"
CHUNK_BYTES = 64 * 1024
OVERSIZED_CHUNKS = 200
HALF_SECOND = 0.5


def _public(host: str) -> list[str]:
    return [PUBLIC_ADDRESS]


def _client(status: int, text: str = "") -> httpx.Client:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status, text=text)

    return httpx.Client(transport=httpx.MockTransport(respond))


def test_should_mark_404_as_expired() -> None:
    assert check_apply_link(_client(404), HUB_URL, _public) == STATUS_EXPIRED


def test_should_mark_hub_expired_marker_as_expired() -> None:
    body = '{jobs:[{title:"Software Engineer",status:"EXPIRED",pumpedAt:"x"}]}'

    assert check_apply_link(_client(200, body), HUB_URL, _public) == STATUS_EXPIRED


def test_should_mark_plain_200_as_active() -> None:
    body = '{jobs:[{title:"Software Engineer",status:"ACTIVE"}]}'

    assert check_apply_link(_client(200, body), HUB_URL, _public) == STATUS_ACTIVE


def test_should_ignore_hub_marker_on_other_hosts() -> None:
    body = 'status:"EXPIRED"'

    assert check_apply_link(
        _client(200, body), "https://jobs.example.test/1", _public
    ) == (STATUS_ACTIVE)


def test_should_mark_transport_error_as_unknown() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refused", request=request)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL, _public) == STATUS_UNKNOWN


def test_should_mark_server_error_as_unknown() -> None:
    assert check_apply_link(_client(503), HUB_URL, _public) == STATUS_UNKNOWN


def test_should_check_external_apply_urls() -> None:
    assert is_checkable(HUB_URL) is True


def _redirecting_client(final_url: str, status: int) -> httpx.Client:
    def respond(request: httpx.Request) -> httpx.Response:
        if request.url.host == "www.finn.no":
            return httpx.Response(302, headers={"Location": final_url})
        return httpx.Response(status)

    return httpx.Client(transport=httpx.MockTransport(respond))


def test_should_check_finn_apply_links() -> None:
    assert is_checkable(FINN_APPLY_URL) is True


def test_should_skip_finn_ad_pages() -> None:
    assert is_checkable("https://www.finn.no/job/ad/1234") is False


def test_should_skip_apply_url_without_web_scheme() -> None:
    assert is_checkable("mailto:jobs@example.test") is False


def test_should_expire_finn_apply_link_redirecting_to_closed_job() -> None:
    client = _redirecting_client("https://example.teamtailor.com/jobs/1-developer", 410)

    assert check_apply_link(client, FINN_APPLY_URL, _public) == STATUS_EXPIRED


def test_should_skip_finn_apply_link_redirecting_to_login() -> None:
    client = _redirecting_client("https://login.vend.no/authn/email-login", 200)

    assert check_apply_link(client, FINN_APPLY_URL, _public) == STATUS_SKIPPED


def test_should_skip_finn_apply_link_staying_on_finn() -> None:
    assert check_apply_link(_client(200), FINN_APPLY_URL, _public) == (STATUS_SKIPPED)


def test_should_block_apply_link_resolving_to_loopback() -> None:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        return httpx.Response(200)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, lambda host: ["127.0.0.1"])

    assert status == STATUS_BLOCKED
    assert requests == []


def test_should_block_redirect_to_link_local_address() -> None:
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(str(request.url))
        return httpx.Response(302, headers={"Location": "http://169.254.169.254/"})

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public)

    assert status == STATUS_BLOCKED
    assert requests == [HUB_URL]


def test_should_block_ipv4_mapped_private_address() -> None:
    assert is_public_url(HUB_URL, lambda host: ["::ffff:10.0.0.1"]) is False


def test_should_stop_after_five_redirects() -> None:
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        hop = len(requests)
        return httpx.Response(302, headers={"Location": f"https://thehub.io/{hop}"})

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public)

    assert status == STATUS_UNKNOWN
    assert len(requests) == 6


def test_should_stop_reading_apply_response_over_size_cap() -> None:
    consumed: list[int] = []

    def body() -> Iterator[bytes]:
        for index in range(OVERSIZED_CHUNKS):
            consumed.append(index)
            yield b"x" * CHUNK_BYTES

    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, content=body())

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public)

    assert (status, len(consumed)) == (STATUS_ACTIVE, MAX_BODY_BYTES // CHUNK_BYTES)


def test_should_give_unknown_without_next_request_once_deadline_is_reached() -> None:
    clock = FakeClock()
    requests: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        clock.now += APPLY_CHECK_DEADLINE_SECONDS
        return httpx.Response(302, headers={"Location": "https://thehub.io/next"})

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public, monotonic=clock)

    assert (status, len(requests)) == (STATUS_UNKNOWN, 1)


def test_should_stop_reading_slow_body_once_deadline_is_reached() -> None:
    clock = FakeClock()
    consumed: list[int] = []

    def body() -> Iterator[bytes]:
        for index in range(OVERSIZED_CHUNKS):
            consumed.append(index)
            clock.now += APPLY_CHECK_DEADLINE_SECONDS / 2
            yield b"x"

    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, content=body())

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public, monotonic=clock)

    assert (status, len(consumed)) == (STATUS_UNKNOWN, 2)


@pytest.mark.parametrize(
    ("address", "public"),
    [
        ("93.184.215.14", True),
        ("2606:2800:220:1::1", True),
        ("10.0.0.1", False),
        ("100.64.0.1", False),
        ("224.0.0.1", False),
        ("not-an-address", False),
    ],
)
def test_should_classify_address_as_public(address: str, public: bool) -> None:
    assert is_public_address(address) is public


@pytest.mark.parametrize(
    "url",
    ["ftp://thehub.io/jobs/1", "https:///jobs/1", "http://[::1"],
    ids=["other scheme", "no host", "malformed"],
)
def test_should_reject_url_that_is_not_plain_web(url: str) -> None:
    assert is_public_url(url, _public) is False


def test_should_check_literal_public_ip_without_dns() -> None:
    def fail(host: str) -> list[str]:
        raise AssertionError(host)

    assert is_public_url("http://93.184.215.14/jobs", fail) is True


@pytest.mark.parametrize(
    "resolve",
    [lambda host: [], lambda host: (_ for _ in ()).throw(OSError(host))],
    ids=["no addresses", "dns failure"],
)
def test_should_reject_url_that_does_not_resolve(
    resolve: Callable[[str], list[str]],
) -> None:
    assert is_public_url(HUB_URL, resolve) is False


def test_should_resolve_requested_host() -> None:
    asked: list[str] = []

    def resolve(host: str) -> list[str]:
        asked.append(host)
        return [PUBLIC_ADDRESS]

    is_public_url(HUB_URL, resolve)

    assert asked == ["thehub.io"]


def test_should_send_browser_user_agent_to_apply_page() -> None:
    agents: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        agents.append(request.headers["User-Agent"])
        return httpx.Response(200)

    check_apply_link(
        httpx.Client(transport=httpx.MockTransport(respond)), HUB_URL, _public
    )

    assert agents == [USER_AGENT]


def test_should_follow_redirect_while_time_remains() -> None:
    clock = FakeClock()
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(request.url.path)
        clock.now += APPLY_CHECK_DEADLINE_SECONDS - HALF_SECOND
        if request.url.path == "/next":
            return httpx.Response(200)
        return httpx.Response(302, headers={"Location": "https://thehub.io/next"})

    client = httpx.Client(transport=httpx.MockTransport(respond))

    status = check_apply_link(client, HUB_URL, _public, monotonic=clock)

    assert (status, requests) == (STATUS_ACTIVE, ["/jobs/abc123", "/next"])


def test_should_read_body_with_invalid_bytes() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, content=b'\xff\xfe status:"EXPIRED"')

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL, _public) == STATUS_EXPIRED


def test_should_block_redirect_even_when_client_follows_redirects() -> None:
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(str(request.url))
        return httpx.Response(302, headers={"Location": "http://169.254.169.254/"})

    client = httpx.Client(transport=httpx.MockTransport(respond), follow_redirects=True)

    status = check_apply_link(client, HUB_URL, _public)

    assert (status, requests) == (STATUS_BLOCKED, [HUB_URL])


def test_should_check_apply_page_with_get() -> None:
    methods: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        methods.append(request.method)
        return httpx.Response(200)

    check_apply_link(
        httpx.Client(transport=httpx.MockTransport(respond)), HUB_URL, _public
    )

    assert methods == ["GET"]


def test_should_resolve_localhost_to_loopback() -> None:
    assert "127.0.0.1" in resolve_host("localhost")


def test_should_read_body_declared_in_non_text_charset() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            content=b'status:"EXPIRED" \xff',
            headers={"Content-Type": "text/html; charset=base64"},
        )

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL, _public) == STATUS_EXPIRED


def test_should_give_unknown_when_body_does_not_match_content_encoding() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200, content=b"not gzip", headers={"Content-Encoding": "gzip"}
        )

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL, _public) == STATUS_UNKNOWN


def test_should_reject_host_with_label_too_long_to_resolve() -> None:
    assert is_public_url(f"https://{'a' * 64}.test/jobs") is False


def test_should_read_body_in_declared_charset() -> None:
    def respond(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            content='status:"EXPIRED"'.encode("utf-16"),
            headers={"Content-Type": "text/html; charset=utf-16"},
        )

    client = httpx.Client(transport=httpx.MockTransport(respond))

    assert check_apply_link(client, HUB_URL, _public) == STATUS_EXPIRED
