from __future__ import annotations

import sys
import threading
from typing import TYPE_CHECKING

import httpx
import pytest
from factories import FakeClock

from jobranger.net.rate_limit import (
    DEFAULT_PENALTY_SECONDS,
    MAX_RETRY_AFTER_SECONDS,
    HostLimiter,
    HostRule,
    event_hooks,
    retry_after_seconds,
)

if TYPE_CHECKING:
    from collections.abc import Callable, Iterator

HOST = "www.example.test"
OTHER = "api.example.test"


def _limiter(rule: HostRule) -> tuple[HostLimiter, FakeClock]:
    clock = FakeClock()
    return HostLimiter({HOST: rule}, rule, clock=clock, sleep=clock.sleep), clock


def _client(
    respond: Callable[[httpx.Request], httpx.Response], rule: HostRule
) -> tuple[httpx.Client, FakeClock]:
    clock = FakeClock()
    limiter = HostLimiter({}, rule, clock=clock, sleep=clock.sleep)
    client = httpx.Client(
        transport=httpx.MockTransport(respond), event_hooks=event_hooks(limiter)
    )
    return client, clock


def test_should_let_burst_through_without_sleeping() -> None:
    limiter, clock = _limiter(HostRule(rate=1.0, burst=2))

    limiter.acquire(HOST)
    limiter.acquire(HOST)

    assert clock.sleeps == []


def test_should_space_requests_after_burst_by_interval() -> None:
    limiter, clock = _limiter(HostRule(rate=0.5, burst=1))

    limiter.acquire(HOST)
    limiter.acquire(HOST)
    limiter.acquire(HOST)

    assert clock.sleeps == [pytest.approx(2.0), pytest.approx(2.0)]


def test_should_not_accumulate_credit_while_idle() -> None:
    limiter, clock = _limiter(HostRule(rate=1.0, burst=2))
    limiter.acquire(HOST)
    clock.now += 100.0

    for _ in range(3):
        limiter.acquire(HOST)

    assert clock.sleeps == [pytest.approx(1.0)]


def test_should_keep_hosts_independent() -> None:
    limiter, clock = _limiter(HostRule(rate=0.5, burst=1))
    limiter.acquire(HOST)

    limiter.acquire(OTHER)

    assert clock.sleeps == []


def test_should_apply_host_rule_over_default() -> None:
    clock = FakeClock()
    limiter = HostLimiter(
        {HOST: HostRule(rate=0.5, burst=1)},
        HostRule(rate=1.0, burst=2),
        clock=clock,
        sleep=clock.sleep,
    )

    limiter.acquire(HOST)
    limiter.acquire(HOST)
    limiter.acquire(OTHER)
    limiter.acquire(OTHER)

    assert clock.sleeps == [pytest.approx(2.0)]


def test_should_wait_out_retry_after_on_429() -> None:
    limiter, clock = _limiter(HostRule(rate=1.0, burst=2))
    limiter.acquire(HOST)

    limiter.penalize(HOST, 10.0)
    limiter.acquire(HOST)

    assert clock.sleeps == [pytest.approx(10.0)]


def test_should_space_same_host_requests_through_client_hooks() -> None:
    client, clock = _client(lambda request: httpx.Response(200), HostRule(0.5, 1))

    with client:
        client.get("https://jobs.example.test/1")
        client.get("https://jobs.example.test/2")

    assert clock.sleeps == [pytest.approx(2.0)]


def test_should_wait_out_retry_after_from_429_response() -> None:
    responses = iter(
        [httpx.Response(429, headers={"Retry-After": "7"}), httpx.Response(200)]
    )
    client, clock = _client(lambda request: next(responses), HostRule(1.0, 2))

    with client:
        client.get("https://jobs.example.test/1")
        client.get("https://jobs.example.test/2")

    assert clock.sleeps == [pytest.approx(7.0)]


def test_should_back_off_default_penalty_when_429_has_no_retry_after() -> None:
    responses = iter([httpx.Response(429), httpx.Response(200)])
    client, clock = _client(lambda request: next(responses), HostRule(1.0, 2))

    with client:
        client.get("https://jobs.example.test/1")
        client.get("https://jobs.example.test/2")

    assert clock.sleeps == [pytest.approx(DEFAULT_PENALTY_SECONDS)]


def test_should_parse_retry_after_seconds() -> None:
    response = httpx.Response(429, headers={"Retry-After": "12"})

    assert retry_after_seconds(response) == 12.0


def test_should_read_retry_after_date_without_zone_as_utc() -> None:
    response = httpx.Response(
        429, headers={"Retry-After": "Thu, 01 Jan 2099 00:00:10 -0000"}
    )

    assert retry_after_seconds(response) == MAX_RETRY_AFTER_SECONDS


def test_should_clamp_past_retry_after_date_to_zero() -> None:
    response = httpx.Response(
        429, headers={"Retry-After": "Sat, 01 Jan 2000 00:00:00 GMT"}
    )

    assert retry_after_seconds(response) == 0.0


@pytest.mark.parametrize("header", ["1000000000", "Fri, 31 Dec 9999 23:59:59 GMT"])
def test_should_cap_retry_after(header: str) -> None:
    response = httpx.Response(429, headers={"Retry-After": header})

    assert retry_after_seconds(response) == MAX_RETRY_AFTER_SECONDS


@pytest.mark.parametrize("header", ["soon", "\u00b2"])
def test_should_ignore_unparseable_retry_after(header: str) -> None:
    response = httpx.Response(429, headers={"Retry-After": header.encode("latin-1")})

    assert retry_after_seconds(response) is None


def test_should_return_none_without_retry_after_header() -> None:
    assert retry_after_seconds(httpx.Response(429)) is None


CONCURRENT_THREADS = 32
TINY_SWITCH_INTERVAL = 1e-6


@pytest.fixture
def fast_thread_switching() -> Iterator[None]:
    previous = sys.getswitchinterval()
    sys.setswitchinterval(TINY_SWITCH_INTERVAL)
    yield
    sys.setswitchinterval(previous)


@pytest.mark.usefixtures("fast_thread_switching")
def test_should_hand_out_distinct_slots_to_concurrent_acquires() -> None:
    rule = HostRule(rate=1.0, burst=1)
    booked: list[float] = []
    booked_lock = threading.Lock()

    def record_sleep(seconds: float) -> None:
        with booked_lock:
            booked.append(seconds)

    limiter = HostLimiter({}, rule, clock=lambda: 0.0, sleep=record_sleep)
    start = threading.Barrier(CONCURRENT_THREADS)

    def acquire() -> None:
        start.wait()
        limiter.acquire(HOST)

    threads = [threading.Thread(target=acquire) for _ in range(CONCURRENT_THREADS)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert sorted(booked) == [float(slot) for slot in range(1, CONCURRENT_THREADS)]


def test_should_not_block_other_host_while_one_host_waits() -> None:
    rule = HostRule(rate=0.5, burst=1)
    waiting = threading.Event()
    release = threading.Event()

    def blocking_sleep(seconds: float) -> None:
        waiting.set()
        release.wait()

    limiter = HostLimiter({}, rule, clock=lambda: 0.0, sleep=blocking_sleep)
    limiter.acquire(HOST)
    sleeper = threading.Thread(target=limiter.acquire, args=(HOST,))
    sleeper.start()
    waiting.wait()
    other = threading.Thread(target=limiter.acquire, args=(OTHER,))

    other.start()
    other.join(timeout=1.0)
    finished = not other.is_alive()
    release.set()
    sleeper.join()

    assert finished


def test_should_push_back_with_host_rule_tolerance() -> None:
    clock = FakeClock()
    limiter = HostLimiter(
        {HOST: HostRule(rate=1.0, burst=3)},
        HostRule(rate=1.0, burst=1),
        clock=clock,
        sleep=clock.sleep,
    )

    limiter.penalize(HOST, 10.0)
    limiter.acquire(HOST)

    assert clock.sleeps == [pytest.approx(10.0)]


def test_should_keep_later_booked_slot_when_penalty_is_shorter() -> None:
    limiter, clock = _limiter(HostRule(rate=0.1, burst=1))
    limiter.acquire(HOST)

    limiter.penalize(HOST, 1.0)
    limiter.acquire(HOST)

    assert clock.sleeps == [pytest.approx(10.0)]
