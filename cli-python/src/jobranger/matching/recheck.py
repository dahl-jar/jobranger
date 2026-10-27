from __future__ import annotations

from dataclasses import dataclass
from typing import TYPE_CHECKING

from jobranger.providers.prices import AD_TOKENS_HIGH, OUTPUT_TOKENS_HIGH, cost_usd

if TYPE_CHECKING:
    from jobranger.providers.prices import ModelSpec
    from jobranger.storage.store import Store

CHARS_PER_TOKEN = 2


@dataclass(frozen=True)
class RecheckEstimate:
    ads: int
    cost_usd: float | None


def estimate_recheck(
    store: Store, version: str, system_chars: int, spec: ModelSpec | None
) -> RecheckEstimate:
    if system_chars < 0:
        msg = f"system_chars must be zero or more, got {system_chars}"
        raise ValueError(msg)
    ads = store.count_outdated(version)
    if ads == 0:
        return RecheckEstimate(ads=0, cost_usd=0.0)
    if spec is None:
        return RecheckEstimate(ads=ads, cost_usd=None)
    system_tokens = system_chars // CHARS_PER_TOKEN
    cost = cost_usd(
        spec,
        input_tokens=AD_TOKENS_HIGH * ads,
        cache_write_5m=system_tokens,
        cache_write_1h=0,
        cache_read=system_tokens * (ads - 1),
        output_tokens=OUTPUT_TOKENS_HIGH * ads,
        batch=False,
    )
    return RecheckEstimate(ads=ads, cost_usd=cost)
