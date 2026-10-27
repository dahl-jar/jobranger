from __future__ import annotations

import argparse
import json
import tempfile
from datetime import date
from pathlib import Path

from jobranger import budget
from jobranger.matching.recheck import estimate_recheck
from jobranger.providers.base import Usage
from jobranger.providers.prices import ModelSpec, spec_for
from jobranger.settings import AppConfig, ModelConfig, PriceOverride, ProfileRef
from jobranger.storage.store import Store

TODAY = date(2026, 10, 15)
PROVIDER = "openrouter"
MODEL = "any"
PRICE = PriceOverride(input_per_mtok=10, output_per_mtok=10)
USD_PLACES = 12
LIMIT_USD = 0.10
PER_AD_USD = 0.02
MICRO_USD = 0.000001
RECHECK_ADS = 3
RECHECK_SYSTEM_CHARS = 4_001
RECHECK_SPEC = ModelSpec(
    input=1.0,
    output=2.0,
    cache_read=0.1,
    cache_write_5m=1.25,
    cache_write_1h=2.0,
    supports_effort=False,
)


class OutdatedCount:
    def __init__(self, ads: int) -> None:
        self.ads = ads

    def count_outdated(self, version: str) -> int:
        return self.ads


def usd(value: float) -> float:
    return round(value, USD_PLACES)


def prices(spec: ModelSpec) -> dict[str, float]:
    return spec.model_dump(exclude={"supports_effort"})


def config(limit: float) -> AppConfig:
    return AppConfig(
        profile=ProfileRef(path=Path("profile.yaml")),
        model=ModelConfig(
            provider=PROVIDER, model=MODEL, price=PRICE, monthly_limit_usd=limit
        ),
    )


def affordable_row(
    folder: Path, index: int, spent: float, limit: float, per_ad: float
) -> dict[str, float | int | None]:
    spent, limit, per_ad = usd(spent), usd(limit), usd(per_ad)
    store = Store(folder / f"usage-{index}.db")
    store.add_usage(TODAY.isoformat(), Usage(calls=1, cost_usd=spent))
    count = budget.affordable_count(store, config(limit), TODAY, per_ad)
    return {
        "spent_usd": spent,
        "limit_usd": limit,
        "per_ad_usd": per_ad,
        "count": count,
    }


def estimates(folder: Path) -> dict[str, object]:
    spec = spec_for(PROVIDER, MODEL, PRICE)
    if spec is None:
        msg = "price override gave no model spec"
        raise ValueError(msg)
    store = Store(folder / "empty.db")
    cache_write = budget._cache_write_cost(spec)
    cases = [
        (PER_AD_USD * 2, LIMIT_USD + cache_write, PER_AD_USD),
        (0.0, cache_write + 3 * MICRO_USD, 0.0),
        (0.0, cache_write + 4 * MICRO_USD, 1.4 * MICRO_USD),
        (LIMIT_USD * 3, LIMIT_USD, PER_AD_USD),
    ]
    recheck = estimate_recheck(
        OutdatedCount(RECHECK_ADS), "golden", RECHECK_SYSTEM_CHARS, RECHECK_SPEC
    )
    return {
        "prices": prices(spec),
        "cache_write_usd": usd(cache_write),
        "ad_cost_usd": {
            "single": usd(budget.estimate_ad_cost(store, spec, TODAY, batch=False)),
            "batch": usd(budget.estimate_ad_cost(store, spec, TODAY, batch=True)),
        },
        "affordable": [
            affordable_row(folder, index, *case) for index, case in enumerate(cases)
        ],
        "recheck": {
            "prices": prices(RECHECK_SPEC),
            "ads": recheck.ads,
            "system_chars": RECHECK_SYSTEM_CHARS,
            "cost_usd": usd(recheck.cost_usd or 0.0),
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, required=True)
    arguments = parser.parse_args()
    out: Path = arguments.out
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="jobranger-budget-") as temp:
        value = estimates(Path(temp))
    out.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
