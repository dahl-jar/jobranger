from __future__ import annotations

import pytest
from factories import FakeClock

from jobranger.providers.backoff import SharedBackoff


def _pause_after_failures(backoff: SharedBackoff, count: int) -> None:
    for _ in range(count):
        backoff.penalize(None)
        backoff.wait()


def test_should_not_sleep_before_any_failure() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    backoff.wait()

    assert clock.sleeps == []


def test_should_double_pause_up_to_cap_without_retry_after() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    _pause_after_failures(backoff, 5)

    assert clock.sleeps == [15, 30, 60, 120, 120]


def test_should_reset_pause_after_success() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)
    _pause_after_failures(backoff, 2)

    backoff.reset()
    _pause_after_failures(backoff, 1)

    assert clock.sleeps[-1] == 15


@pytest.mark.parametrize("retry_after", [0.5, 30.0], ids=["under a second", "seconds"])
def test_should_pause_for_retry_after_when_given(retry_after: float) -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    backoff.penalize(retry_after)
    backoff.wait()

    assert clock.sleeps == [retry_after]


def test_should_keep_longer_pause_when_shorter_penalty_follows() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    backoff.penalize(60.0)
    backoff.penalize(10.0)
    backoff.wait()

    assert clock.sleeps == [60.0]
