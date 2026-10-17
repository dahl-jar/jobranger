from __future__ import annotations

from dataclasses import replace
from datetime import date, timedelta
from pathlib import Path
from typing import TYPE_CHECKING

import pytest
from factories import CALL_USAGE

from jobranger import budget
from jobranger.providers.prices import ModelSpec, load_specs
from jobranger.settings import (
    MAX_AMOUNT_USD,
    AppConfig,
    ModelConfig,
    PriceOverride,
    ProfileRef,
)
from jobranger.storage.store import Store, UsageDay

if TYPE_CHECKING:
    from collections.abc import Callable

TODAY = date(2026, 10, 15)
PRICE = PriceOverride(input_per_mtok=10, output_per_mtok=10)
PRICED_SPEC = ModelSpec(
    input=10,
    output=10,
    cache_read=1,
    cache_write_5m=12.5,
    cache_write_1h=20,
    supports_effort=False,
)
AD_ESTIMATE_USD = 0.026866
BATCH_AD_ESTIMATE_USD = 0.068685
CACHE_WRITE_USD = 0.11632
LIVE_COLD_BATCH_AD_USD = 0.001506 / 2
OUT_OF_RANGE_AMOUNTS = [-1.0, 1e303]


def _config(tmp_path: Path, limit: float | None, **model: object) -> AppConfig:
    settings = {"provider": "openrouter", "model": "any", "price": PRICE, **model}
    return AppConfig(
        profile=ProfileRef(path=tmp_path / "profile.yaml"),
        model=ModelConfig.model_validate({**settings, "monthly_limit_usd": limit}),
    )


def _unpriced(tmp_path: Path, limit: float | None) -> AppConfig:
    return _config(tmp_path, limit, price=None)


def _free(tmp_path: Path, limit: float | None) -> AppConfig:
    return _config(tmp_path, limit, provider="ollama", model="local", price=None)


def _spend(store: Store, day: date, cost: float, checked: int = 0) -> None:
    store.add_usage(
        day.isoformat(), replace(CALL_USAGE, cost_usd=cost), ads_checked=checked
    )


def test_should_stop_calls_when_limit_is_zero(tmp_path: Path, store: Store) -> None:
    config = _config(tmp_path, 0.0)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is False


def test_should_stop_calls_when_next_call_estimate_reaches_limit(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, 0.01 + AD_ESTIMATE_USD)

    allowed = budget.allowed(store, config, TODAY, reserved_usd=0.01)

    assert allowed is False


def test_should_reserve_cache_write_before_first_call(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, AD_ESTIMATE_USD + CACHE_WRITE_USD)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is False


def test_should_skip_cache_write_while_calls_are_in_flight(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, 0.01 + AD_ESTIMATE_USD + 0.000001)

    allowed = budget.allowed(store, config, TODAY, reserved_usd=0.01)

    assert allowed is True


def test_should_count_in_flight_reservation_toward_limit(
    tmp_path: Path, store: Store
) -> None:
    _spend(store, TODAY, 0.05)
    config = _config(tmp_path, 0.10)

    allowed = budget.allowed(store, config, TODAY, reserved_usd=0.05)

    assert allowed is False


@pytest.mark.parametrize("reserved", OUT_OF_RANGE_AMOUNTS)
def test_should_reject_reservation_outside_amount_range(
    tmp_path: Path, store: Store, reserved: float
) -> None:
    config = _config(tmp_path, 0.10)

    with pytest.raises(ValueError, match="^reserved_usd must be between"):
        budget.allowed(store, config, TODAY, reserved_usd=reserved)


def test_should_accept_reservation_at_max_amount(tmp_path: Path, store: Store) -> None:
    config = _config(tmp_path, 0.10)

    allowed = budget.allowed(store, config, TODAY, reserved_usd=MAX_AMOUNT_USD)

    assert allowed is False


def test_should_allow_free_model_when_limit_is_used_up(
    tmp_path: Path, store: Store
) -> None:
    _spend(store, TODAY, 1.0)
    config = _free(tmp_path, 1.0)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is True


@pytest.mark.parametrize("missing", [{"price": None}, {"provider": None}])
def test_should_stop_calls_when_limit_is_set_and_model_has_no_price(
    tmp_path: Path, store: Store, missing: dict[str, object]
) -> None:
    config = _config(tmp_path, 10.0, **missing)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is False


def test_should_afford_no_ads_when_limit_is_set_and_model_has_no_price(
    tmp_path: Path, store: Store
) -> None:
    config = _unpriced(tmp_path, 10.0)

    count = budget.affordable_count(store, config, TODAY, 0.02)

    assert count == 0


def test_should_run_unpriced_model_when_limit_is_none(
    tmp_path: Path, store: Store
) -> None:
    config = _unpriced(tmp_path, None)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is True


def test_should_stop_calls_when_summed_cents_reach_limit_exactly(
    tmp_path: Path, store: Store
) -> None:
    for _ in range(10):
        _spend(store, TODAY, 0.01)
    config = _config(tmp_path, 0.10 + AD_ESTIMATE_USD + CACHE_WRITE_USD)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is False


def test_should_stop_calls_when_stored_cost_is_huge(
    tmp_path: Path, store: Store
) -> None:
    _spend(store, TODAY, 1e308)
    config = _config(tmp_path, 10.0)

    allowed = budget.allowed(store, config, TODAY)

    assert allowed is False


def test_should_ignore_last_month_spend_on_first_day_of_month(
    tmp_path: Path, store: Store
) -> None:
    first = date(2026, 10, 1)
    _spend(store, first - timedelta(days=1), 5.0)
    config = _config(tmp_path, 1.0)

    allowed = budget.allowed(store, config, first)

    assert allowed is True


def test_should_leave_next_month_rows_out_of_month_spend(store: Store) -> None:
    _spend(store, date(2026, 12, 31), 0.90)
    _spend(store, date(2027, 1, 1), 0.05)

    spend = budget.month_spend(store, date(2026, 12, 31))

    assert spend == pytest.approx(0.90)


def test_should_cap_batch_size_to_remaining_budget(
    tmp_path: Path, store: Store
) -> None:
    _spend(store, TODAY, 0.04)
    config = _config(tmp_path, 0.10 + CACHE_WRITE_USD)

    count = budget.affordable_count(store, config, TODAY, 0.02)

    assert count == 2


def test_should_reserve_one_cache_write_before_batch_ads(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, CACHE_WRITE_USD)

    count = budget.affordable_count(store, config, TODAY, 0.02)

    assert count == 0


def test_should_afford_no_ads_when_limit_is_spent(tmp_path: Path, store: Store) -> None:
    _spend(store, TODAY, 0.30)
    config = _config(tmp_path, 0.10)

    count = budget.affordable_count(store, config, TODAY, 0.02)

    assert count == 0


@pytest.mark.parametrize(
    ("left_micros", "per_ad", "count"), [(3, 0.0, 2), (3, 4e-7, 2), (4, 1.4e-6, 1)]
)
def test_should_round_per_ad_cost_up_to_whole_micro(
    tmp_path: Path, store: Store, left_micros: int, per_ad: float, count: int
) -> None:
    config = _config(tmp_path, CACHE_WRITE_USD + left_micros / 1_000_000)

    affordable = budget.affordable_count(store, config, TODAY, per_ad)

    assert affordable == count


@pytest.mark.parametrize("per_ad", OUT_OF_RANGE_AMOUNTS)
def test_should_reject_per_ad_cost_outside_amount_range(
    tmp_path: Path, store: Store, per_ad: float
) -> None:
    config = _config(tmp_path, 0.10)

    with pytest.raises(ValueError, match="^per_ad_usd must be between"):
        budget.affordable_count(store, config, TODAY, per_ad)


def test_should_keep_first_batch_within_budget_when_cache_misses(
    tmp_path: Path, store: Store
) -> None:
    config = _config(
        tmp_path, 0.50, provider="anthropic", model="claude-haiku-5-5", price=None
    )
    spec = load_specs()["anthropic:claude-haiku-5-5"]
    per_ad = budget.estimate_ad_cost(store, spec, TODAY, batch=True)

    count = budget.affordable_count(store, config, TODAY, per_ad)

    assert count * LIVE_COLD_BATCH_AD_USD <= 0.50


@pytest.mark.parametrize(("config", "limit"), [(_config, None), (_free, 0.10)])
def test_should_not_cap_batch_when_limit_is_none_or_model_is_free(
    tmp_path: Path,
    store: Store,
    config: Callable[[Path, float | None], AppConfig],
    limit: float | None,
) -> None:
    _spend(store, TODAY, 1.0)
    app_config = config(tmp_path, limit)

    count = budget.affordable_count(store, app_config, TODAY, 0.0)

    assert count is None


@pytest.mark.parametrize(
    ("batch", "expected"), [(False, AD_ESTIMATE_USD), (True, BATCH_AD_ESTIMATE_USD)]
)
def test_should_estimate_ad_cost_from_prefix_ad_and_reply_tokens(
    store: Store, batch: bool, expected: float
) -> None:
    estimate = budget.estimate_ad_cost(store, PRICED_SPEC, TODAY, batch=batch)

    assert estimate == pytest.approx(expected)


@pytest.mark.parametrize(
    ("cost", "checked", "expected"),
    [(0.01, 10, AD_ESTIMATE_USD), (0.10, 2, 0.05)],
)
def test_should_estimate_ad_cost_from_higher_of_token_estimate_and_history(
    store: Store, cost: float, checked: int, expected: float
) -> None:
    _spend(store, TODAY - timedelta(days=3), cost, checked=checked)

    estimate = budget.estimate_ad_cost(store, PRICED_SPEC, TODAY, batch=False)

    assert estimate == pytest.approx(expected)


@pytest.mark.parametrize(
    ("batch", "per_ad"), [(False, AD_ESTIMATE_USD), (True, BATCH_AD_ESTIMATE_USD)]
)
def test_should_estimate_daily_cost_from_fourteen_day_mean(
    store: Store, batch: bool, per_ad: float
) -> None:
    _spend(store, TODAY - timedelta(days=14), 0.0, checked=100)
    _spend(store, TODAY - timedelta(days=13), 0.0, checked=20)
    _spend(store, TODAY - timedelta(days=5), 0.0, checked=0)
    _spend(store, TODAY, 0.0, checked=4)
    daily_ads = budget.ads_per_day(store, TODAY)

    cost = budget.estimate_daily_cost(PRICED_SPEC, daily_ads, batch=batch)

    assert cost == pytest.approx(12 * per_ad)


def test_should_assume_nine_ads_per_day_without_history(store: Store) -> None:
    daily_ads = budget.ads_per_day(store, TODAY)

    assert daily_ads == 9


def test_should_not_estimate_daily_cost_without_price() -> None:
    cost = budget.estimate_daily_cost(None, 9, batch=False)

    assert cost is None


def test_should_total_month_from_this_months_rows(tmp_path: Path, store: Store) -> None:
    _spend(store, date(2026, 9, 30), 9.0, checked=1)
    _spend(store, date(2026, 10, 1), 0.5, checked=1)
    _spend(store, TODAY, 2.5, checked=1)
    config = _config(tmp_path, 10.0)

    summary = budget.usage_summary(store, config, TODAY)

    assert (summary.month_usd, summary.month_tokens) == (pytest.approx(3.0), 240)
    assert (summary.limit_usd, summary.used_pct) == (10.0, pytest.approx(30.0))


def test_should_summarize_the_fourteen_day_window(tmp_path: Path, store: Store) -> None:
    _spend(store, date(2026, 10, 1), 9.0, checked=1)
    _spend(store, TODAY - timedelta(days=1), 1.5, checked=1)
    _spend(store, TODAY, 0.5, checked=1)
    _spend(store, TODAY, 0.5, checked=1)
    config = _config(tmp_path, 10.0)

    summary = budget.usage_summary(store, config, TODAY)

    assert (summary.today_usd, summary.today_tokens) == (1.0, 240)
    assert (summary.avg_usd, summary.peak_usd) == (pytest.approx(1.25), 1.5)
    assert [day.day for day in summary.days][::13] == ["2026-10-02", "2026-10-15"]
    assert summary.days[0] == UsageDay("2026-10-02", 0, 0, 0, 0.0, 0, 0)


def test_should_report_zero_daily_costs_without_checked_days(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, 10.0)

    summary = budget.usage_summary(store, config, TODAY)

    assert (summary.avg_usd, summary.peak_usd) == (0.0, 0.0)


def test_should_report_month_spend_as_float_zero_without_usage(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, 10.0)

    summary = budget.usage_summary(store, config, TODAY)

    assert (type(summary.month_usd), summary.month_usd) == (float, 0.0)


def test_should_leave_next_month_tokens_out_of_summary(
    tmp_path: Path, store: Store
) -> None:
    _spend(store, date(2026, 12, 31), 0.90)
    _spend(store, date(2027, 1, 1), 0.05)
    config = _config(tmp_path, 1.0)

    summary = budget.usage_summary(store, config, date(2026, 12, 31))

    assert summary.month_tokens == 120


@pytest.mark.parametrize(("limit", "used_pct"), [(None, None), (0.0, 100.0)])
def test_should_report_used_share_without_positive_limit(
    tmp_path: Path, store: Store, limit: float | None, used_pct: float | None
) -> None:
    config = _config(tmp_path, limit)

    summary = budget.usage_summary(store, config, TODAY)

    assert summary.used_pct == used_pct


def test_should_reset_on_january_first_in_december(
    tmp_path: Path, store: Store
) -> None:
    config = _config(tmp_path, 1.0)

    summary = budget.usage_summary(store, config, date(2026, 12, 20))

    assert summary.resets_on == date(2027, 1, 1)
