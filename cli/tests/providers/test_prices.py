from __future__ import annotations

import pytest

from jobranger.providers.prices import ModelSpec, cost_usd, spec_for
from jobranger.settings import PriceOverride

SONNET = "claude-sonnet-5-5"
UNKNOWN_MODEL = "vendor/unknown-model"
MTOK = 1_000_000


def _sonnet() -> ModelSpec:
    spec = spec_for("anthropic", SONNET, None)
    assert spec is not None
    return spec


def _cost(spec: ModelSpec, **tokens: int) -> float:
    counts = {
        "input_tokens": 0,
        "cache_write_5m": 0,
        "cache_write_1h": 0,
        "cache_read": 0,
        "output_tokens": 0,
        "batch": False,
    }
    return cost_usd(spec, **{**counts, **tokens})


def test_should_price_sonnet_cache_reads_at_five_percent_of_input() -> None:
    assert _cost(_sonnet(), cache_read=MTOK) == pytest.approx(0.10)


def test_should_halve_cost_for_batch_requests() -> None:
    assert _cost(_sonnet(), output_tokens=MTOK, batch=True) == pytest.approx(5.0)


def test_should_price_one_hour_cache_writes_at_double_input() -> None:
    assert _cost(_sonnet(), cache_write_1h=MTOK) == pytest.approx(4.0)


def test_should_leave_unknown_model_unpriced_without_override() -> None:
    assert spec_for("openrouter", UNKNOWN_MODEL, None) is None


def test_should_scale_table_cache_prices_to_overridden_input_price() -> None:
    override = PriceOverride(input_per_mtok=4.0, output_per_mtok=20.0)

    spec = spec_for("anthropic", SONNET, override)

    assert spec is not None
    assert spec.model_dump() == pytest.approx(
        {
            "input": 4.0,
            "output": 20.0,
            "cache_read": 0.2,
            "cache_write_5m": 5.0,
            "cache_write_1h": 8.0,
            "supports_effort": True,
        }
    )


def test_should_derive_cache_prices_for_unknown_model_from_override() -> None:
    override = PriceOverride(input_per_mtok=2.0, output_per_mtok=8.0)

    spec = spec_for("openrouter", UNKNOWN_MODEL, override)

    assert spec == ModelSpec(
        input=2.0,
        output=8.0,
        cache_read=0.2,
        cache_write_5m=2.5,
        cache_write_1h=4.0,
        supports_effort=False,
    )


def test_should_price_ollama_models_at_zero() -> None:
    spec = spec_for("ollama", "llama3.2", None)

    assert spec is not None
    assert _cost(spec, input_tokens=MTOK, output_tokens=MTOK) == 0.0


def test_should_price_dated_model_id_from_undated_entry() -> None:
    spec = spec_for("anthropic", "claude-haiku-4-5-20251001", None)

    assert spec is not None
    assert (spec.input, spec.output) == (1.0, 5.0)
