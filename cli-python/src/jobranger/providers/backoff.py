from __future__ import annotations

import threading
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Callable

BACKOFF_BASE_SECONDS = 15.0
BACKOFF_MAX_SECONDS = 120.0
BACKOFF_FACTOR = 2


class SharedBackoff:
    def __init__(
        self, monotonic: Callable[[], float], sleep: Callable[[float], None]
    ) -> None:
        self._monotonic = monotonic
        self._sleep = sleep
        self._lock = threading.Lock()
        self._until = 0.0
        self._step = BACKOFF_BASE_SECONDS

    def wait(self) -> None:
        with self._lock:
            remaining = self._until - self._monotonic()
        if remaining > 0:
            self._sleep(remaining)

    def penalize(self, retry_after: float | None) -> None:
        with self._lock:
            pause = retry_after or self._step
            self._until = max(self._until, self._monotonic() + pause)
            self._step = min(self._step * BACKOFF_FACTOR, BACKOFF_MAX_SECONDS)

    def reset(self) -> None:
        with self._lock:
            self._step = BACKOFF_BASE_SECONDS
