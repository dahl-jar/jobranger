from __future__ import annotations

import httpx
import pytest

from jobranger.net.retry import get_with_retry

NO_DELAYS = (0.0, 0.0)


def test_should_retry_on_429_then_succeed() -> None:
    statuses = iter((429, 429, 200))
    requests: list[str] = []

    def respond(request: httpx.Request) -> httpx.Response:
        requests.append(str(request.url))
        return httpx.Response(next(statuses), text="ok")

    client = httpx.Client(transport=httpx.MockTransport(respond))

    response = get_with_retry(
        client, "https://example.test/search", params={"page": 1}, delays=NO_DELAYS
    )

    assert response.status_code == 200
    assert requests == ["https://example.test/search?page=1"] * 3


def test_should_retry_on_transport_error_then_succeed() -> None:
    attempts: list[int] = []

    def respond(request: httpx.Request) -> httpx.Response:
        attempts.append(1)
        if len(attempts) == 1:
            raise httpx.ConnectError("refused", request=request)
        return httpx.Response(200, text="ok")

    client = httpx.Client(transport=httpx.MockTransport(respond))

    response = get_with_retry(client, "https://example.test/feed", delays=NO_DELAYS)

    assert response.status_code == 200
    assert len(attempts) == 2


def test_should_raise_after_final_attempt() -> None:
    attempts: list[int] = []

    def respond(request: httpx.Request) -> httpx.Response:
        attempts.append(1)
        return httpx.Response(503)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    with pytest.raises(httpx.HTTPStatusError):
        get_with_retry(client, "https://example.test/feed", delays=NO_DELAYS)

    assert len(attempts) == 3


def test_should_not_retry_on_404() -> None:
    attempts: list[int] = []

    def respond(request: httpx.Request) -> httpx.Response:
        attempts.append(1)
        return httpx.Response(404)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    with pytest.raises(httpx.HTTPStatusError):
        get_with_retry(client, "https://example.test/missing", delays=NO_DELAYS)

    assert len(attempts) == 1
