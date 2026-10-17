from __future__ import annotations

import math
import statistics
from dataclasses import dataclass
from datetime import date, timedelta
from typing import TYPE_CHECKING

from jobranger.providers.prices import (
    AD_TOKENS_MEAN,
    OUTPUT_TOKENS_MEAN,
    PREFIX_TOKENS_MEAN,
    cost_usd,
    spec_for,
)
from jobranger.settings import MAX_AMOUNT_USD
from jobranger.storage.store import UsageDay

if TYPE_CHECKING:
    from jobranger.providers.base import Usage
    from jobranger.providers.prices import ModelSpec
    from jobranger.settings import AppConfig, ModelConfig
    from jobranger.storage.store import Store

ADS_PER_CALL = 1
DEFAULT_ADS_PER_DAY = 9
RECENT_DAYS = 14
MICRO_USD_PER_USD = 1_000_000
FULL_PCT = 100.0
MONTHS_PER_YEAR = 12


@dataclass(frozen=True)
class UsageSummary:
    month_usd: float
    month_tokens: int
    today_usd: float
    today_tokens: int
    avg_usd: float
    peak_usd: float
    limit_usd: float | None
    used_pct: float | None
    resets_on: date
    days: list[UsageDay]


def record(store: Store, usage: Usage, day: date) -> None:
    store.add_usage(day.isoformat(), usage, ads_checked=ADS_PER_CALL)


def month_start(today: date) -> date:
    return today.replace(day=1)


def month_spend(store: Store, today: date) -> float:
    return store.spend_between(
        month_start(today).isoformat(), _next_month_start(today).isoformat()
    )


def allowed(
    store: Store, config: AppConfig, today: date, reserved_usd: float = 0.0
) -> bool:
    _require_amount("reserved_usd", reserved_usd)
    limit = config.model.monthly_limit_usd
    if limit is None:
        return True
    spec = _model_spec(config.model)
    if spec is None:
        return False
    if _is_free(spec):
        return True
    none_in_flight = reserved_usd == 0
    needed = (
        _micros(month_spend(store, today))
        + _micros(reserved_usd)
        + _micros(estimate_ad_cost(store, spec, today, batch=False))
        + (_micros(_cache_write_cost(spec)) if none_in_flight else 0)
    )
    return needed < _micros(limit)


def affordable_count(
    store: Store, config: AppConfig, today: date, per_ad_usd: float
) -> int | None:
    _require_amount("per_ad_usd", per_ad_usd)
    limit = config.model.monthly_limit_usd
    if limit is None:
        return None
    spec = _model_spec(config.model)
    if spec is None:
        return 0
    if _is_free(spec):
        return None
    remaining = (
        _micros(limit)
        - _micros(month_spend(store, today))
        - _micros(_cache_write_cost(spec))
    )
    per_ad = max(1, math.ceil(per_ad_usd * MICRO_USD_PER_USD))
    return max(0, (remaining - 1) // per_ad)


def estimate_ad_cost(store: Store, spec: ModelSpec, today: date, batch: bool) -> float:
    return max(_constant_ad_cost(spec, batch), _history_ad_cost(store, today))


def estimate_daily_cost(
    spec: ModelSpec | None, daily_ads: float, batch: bool
) -> float | None:
    if spec is None:
        return None
    return _constant_ad_cost(spec, batch) * daily_ads


def _constant_ad_cost(spec: ModelSpec, batch: bool) -> float:
    return cost_usd(
        spec,
        input_tokens=AD_TOKENS_MEAN,
        cache_write_5m=0,
        cache_write_1h=PREFIX_TOKENS_MEAN if batch else 0,
        cache_read=0 if batch else PREFIX_TOKENS_MEAN,
        output_tokens=OUTPUT_TOKENS_MEAN,
        batch=batch,
    )


def ads_per_day(store: Store, today: date) -> float:
    checked = [day.ads_checked for day in _checked_days(_recent_days(store, today))]
    return statistics.fmean(checked) if checked else DEFAULT_ADS_PER_DAY


def usage_summary(store: Store, config: AppConfig, today: date) -> UsageSummary:
    resets = _next_month_start(today)
    month_days = [
        day
        for day in store.usage_days(month_start(today).isoformat())
        if day.day < resets.isoformat()
    ]
    days = _recent_days(store, today)
    costs = [day.cost_usd for day in _checked_days(days)]
    month_usd = sum((day.cost_usd for day in month_days), 0.0)
    limit = config.model.monthly_limit_usd
    return UsageSummary(
        month_usd=month_usd,
        month_tokens=sum(_tokens(day) for day in month_days),
        today_usd=days[-1].cost_usd,
        today_tokens=_tokens(days[-1]),
        avg_usd=statistics.fmean(costs) if costs else 0.0,
        peak_usd=max(costs, default=0.0),
        limit_usd=limit,
        used_pct=_used_pct(month_usd, limit),
        resets_on=resets,
        days=days,
    )


def _history_ad_cost(store: Store, today: date) -> float:
    days = _checked_days(_recent_days(store, today))
    checked = sum(day.ads_checked for day in days)
    return sum(day.cost_usd for day in days) / checked if checked else 0.0


def _cache_write_cost(spec: ModelSpec) -> float:
    return cost_usd(
        spec,
        input_tokens=0,
        cache_write_5m=0,
        cache_write_1h=PREFIX_TOKENS_MEAN,
        cache_read=0,
        output_tokens=0,
        batch=False,
    )


def _is_free(spec: ModelSpec) -> bool:
    prices = (
        spec.input,
        spec.output,
        spec.cache_read,
        spec.cache_write_5m,
        spec.cache_write_1h,
    )
    return not any(prices)


def _require_amount(name: str, usd: float) -> None:
    if not 0 <= usd <= MAX_AMOUNT_USD:
        msg = f"{name} must be between 0 and {MAX_AMOUNT_USD} USD, got {usd}"
        raise ValueError(msg)


def _model_spec(model: ModelConfig) -> ModelSpec | None:
    if model.provider is None or model.model is None:
        return None
    return spec_for(model.provider, model.model, model.price)


def _recent_days(store: Store, today: date) -> list[UsageDay]:
    first = today - timedelta(days=RECENT_DAYS - 1)
    stored = {day.day: day for day in store.usage_days(first.isoformat())}
    keys = [
        (first + timedelta(days=offset)).isoformat() for offset in range(RECENT_DAYS)
    ]
    return [stored.get(key, UsageDay(key, 0, 0, 0, 0.0, 0, 0)) for key in keys]


def _checked_days(days: list[UsageDay]) -> list[UsageDay]:
    return [day for day in days if day.ads_checked > 0]


def _tokens(day: UsageDay) -> int:
    return day.input_tokens + day.output_tokens


def _used_pct(spent_usd: float, limit: float | None) -> float | None:
    if limit is None:
        return None
    if _micros(limit) == 0:
        return FULL_PCT
    return spent_usd / limit * FULL_PCT


def _next_month_start(today: date) -> date:
    return date(
        today.year + today.month // MONTHS_PER_YEAR,
        today.month % MONTHS_PER_YEAR + 1,
        1,
    )


def _micros(usd: float) -> int:
    return round(usd * MICRO_USD_PER_USD)
