from __future__ import annotations

from factories import FakeClock

from jobranger.providers.backoff import SharedBackoff


def _pause_after_failures(backoff: SharedBackoff, count: int) -> None:
    for _ in range(count):
        backoff.penalize(None)
        backoff.wait()


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


def test_should_pause_for_retry_after_when_given() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    backoff.penalize(30.0)
    backoff.wait()

    assert clock.sleeps == [30.0]


def test_should_keep_longer_pause_when_shorter_penalty_follows() -> None:
    clock = FakeClock()
    backoff = SharedBackoff(clock, clock.sleep)

    backoff.penalize(60.0)
    backoff.penalize(10.0)
    backoff.wait()

    assert clock.sleeps == [60.0]
