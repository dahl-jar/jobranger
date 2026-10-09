from __future__ import annotations

import math
import threading
import time
from dataclasses import dataclass
from datetime import UTC, datetime
from email.utils import parsedate_to_datetime
from typing import TYPE_CHECKING

import httpx

if TYPE_CHECKING:
    from collections.abc import Callable, Mapping

DEFAULT_PENALTY_SECONDS = 30.0
MAX_RETRY_AFTER_SECONDS = 300.0


@dataclass(frozen=True)
class HostRule:
    rate: float
    burst: int

    @property
    def interval(self) -> float:
        return 1.0 / self.rate

    @property
    def tolerance(self) -> float:
        return (self.burst - 1) * self.interval


HOST_RULES: dict[str, HostRule] = {
    "www.finn.no": HostRule(rate=0.5, burst=1),
    "www.linkedin.com": HostRule(rate=1 / 3, burst=1),
    "pam-stilling-feed.nav.no": HostRule(rate=2.0, burst=4),
}
DEFAULT_RULE = HostRule(rate=1.0, burst=2)


class HostLimiter:
    def __init__(
        self,
        rules: Mapping[str, HostRule],
        default: HostRule,
        clock: Callable[[], float] = time.monotonic,
        sleep: Callable[[float], None] = time.sleep,
    ) -> None:
        self._rules = rules
        self._default = default
        self._clock = clock
        self._sleep = sleep
        self._arrivals: dict[str, float] = {}
        self._lock = threading.Lock()

    def acquire(self, host: str) -> None:
        rule = self._rules.get(host, self._default)
        with self._lock:
            now = self._clock()
            arrival = max(self._arrivals.get(host, now), now)
            wait = max(0.0, arrival - rule.tolerance - now)
            self._arrivals[host] = arrival + rule.interval
        if wait > 0:
            self._sleep(wait)

    def penalize(self, host: str, retry_after: float) -> None:
        rule = self._rules.get(host, self._default)
        with self._lock:
            pushed = self._clock() + retry_after + rule.tolerance
            self._arrivals[host] = max(self._arrivals.get(host, pushed), pushed)


def event_hooks(limiter: HostLimiter) -> dict[str, list[Callable[..., None]]]:
    def limit_request(request: httpx.Request) -> None:
        limiter.acquire(request.url.host)

    def penalize_on_429(response: httpx.Response) -> None:
        if response.status_code == httpx.codes.TOO_MANY_REQUESTS:
            retry_after = retry_after_seconds(response)
            limiter.penalize(
                response.request.url.host,
                DEFAULT_PENALTY_SECONDS if retry_after is None else retry_after,
            )

    return {"request": [limit_request], "response": [penalize_on_429]}


def retry_after_seconds(response: httpx.Response) -> float | None:
    value = response.headers.get("Retry-After", "").strip()
    if not value:
        return None
    if value.isascii() and value.isdigit():
        return min(float(value), MAX_RETRY_AFTER_SECONDS)
    try:
        moment = parsedate_to_datetime(value)
    except (TypeError, ValueError):
        return None
    if moment.tzinfo is None:
        moment = moment.replace(tzinfo=UTC)
    seconds = (moment - datetime.now(tz=UTC)).total_seconds()
    if not math.isfinite(seconds):
        return None
    return min(max(0.0, seconds), MAX_RETRY_AFTER_SECONDS)
