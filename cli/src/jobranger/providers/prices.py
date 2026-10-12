from __future__ import annotations

import functools
import re
from importlib import resources
from typing import TYPE_CHECKING

from pydantic import BaseModel, ConfigDict, TypeAdapter

if TYPE_CHECKING:
    from jobranger.providers.base import ProviderName
    from jobranger.settings import PriceOverride

PRICES_FILE = "prices.json"
KEY_SEPARATOR = ":"
DATE_SUFFIX = re.compile(r"-\d{8}$")
TOKENS_PER_MTOK = 1_000_000
BATCH_MULTIPLIER = 0.5
DEFAULT_CACHE_READ_RATIO = 0.1
DEFAULT_CACHE_WRITE_5M_RATIO = 1.25
DEFAULT_CACHE_WRITE_1H_RATIO = 2.0


class ModelSpec(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    input: float
    output: float
    cache_read: float
    cache_write_5m: float
    cache_write_1h: float
    supports_effort: bool


FREE = ModelSpec(
    input=0.0,
    output=0.0,
    cache_read=0.0,
    cache_write_5m=0.0,
    cache_write_1h=0.0,
    supports_effort=False,
)


@functools.cache
def load_specs() -> dict[str, ModelSpec]:
    text = resources.files(__package__).joinpath(PRICES_FILE).read_text("utf-8")
    return TypeAdapter(dict[str, ModelSpec]).validate_json(text)


def spec_for(
    provider: ProviderName, model_id: str, override: PriceOverride | None
) -> ModelSpec | None:
    if provider == "ollama":
        return FREE
    table = _table_spec(provider, model_id)
    if override is None:
        return table
    if table is None:
        return ModelSpec(
            input=override.input_per_mtok,
            output=override.output_per_mtok,
            cache_read=override.input_per_mtok * DEFAULT_CACHE_READ_RATIO,
            cache_write_5m=override.input_per_mtok * DEFAULT_CACHE_WRITE_5M_RATIO,
            cache_write_1h=override.input_per_mtok * DEFAULT_CACHE_WRITE_1H_RATIO,
            supports_effort=False,
        )
    scale = override.input_per_mtok / table.input
    return table.model_copy(
        update={
            "input": override.input_per_mtok,
            "output": override.output_per_mtok,
            "cache_read": table.cache_read * scale,
            "cache_write_5m": table.cache_write_5m * scale,
            "cache_write_1h": table.cache_write_1h * scale,
        }
    )


def _table_spec(provider: ProviderName, model_id: str) -> ModelSpec | None:
    specs = load_specs()
    exact = specs.get(f"{provider}{KEY_SEPARATOR}{model_id}")
    if exact is not None:
        return exact
    return specs.get(f"{provider}{KEY_SEPARATOR}{DATE_SUFFIX.sub('', model_id)}")


def cost_usd(
    spec: ModelSpec,
    *,
    input_tokens: int,
    cache_write_5m: int,
    cache_write_1h: int,
    cache_read: int,
    output_tokens: int,
    batch: bool,
) -> float:
    total = (
        input_tokens * spec.input
        + cache_write_5m * spec.cache_write_5m
        + cache_write_1h * spec.cache_write_1h
        + cache_read * spec.cache_read
        + output_tokens * spec.output
    ) / TOKENS_PER_MTOK
    return total * BATCH_MULTIPLIER if batch else total
