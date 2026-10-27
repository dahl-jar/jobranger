from __future__ import annotations

from typing import TYPE_CHECKING

import pytest
from factories import FAKE_VERSION, OUTDATED_VERSION, make_ad, make_match

from jobranger.matching.recheck import RecheckEstimate, estimate_recheck
from jobranger.providers.prices import ModelSpec
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from pathlib import Path

SPEC = ModelSpec(
    input=1.0,
    output=2.0,
    cache_read=0.1,
    cache_write_5m=1.25,
    cache_write_1h=2.0,
    supports_effort=False,
)
SYSTEM_CHARS = 4_001


def _store(tmp_path: Path, outdated: int) -> Store:
    store = Store(tmp_path / "ads.db")
    store.upsert_ad(make_ad(id="finn:current", source_id="current"))
    for index in range(outdated):
        store.upsert_ad(
            make_ad(
                id=f"finn:{index}",
                source_id=str(index),
                match=make_match(version=OUTDATED_VERSION),
            )
        )
    return store


def test_should_price_the_recheck_from_token_estimates(tmp_path: Path) -> None:
    store = _store(tmp_path, outdated=3)

    estimate = estimate_recheck(store, FAKE_VERSION, SYSTEM_CHARS, SPEC)

    assert estimate.ads == 3
    assert estimate.cost_usd == pytest.approx(0.0185, rel=1e-9)


def test_should_return_count_without_cost_when_price_unknown(tmp_path: Path) -> None:
    store = _store(tmp_path, outdated=1)

    estimate = estimate_recheck(store, FAKE_VERSION, SYSTEM_CHARS, None)

    assert estimate == RecheckEstimate(ads=1, cost_usd=None)


@pytest.mark.parametrize("spec", [SPEC, None])
def test_should_return_zero_when_nothing_outdated(
    tmp_path: Path, spec: ModelSpec | None
) -> None:
    store = _store(tmp_path, outdated=0)

    estimate = estimate_recheck(store, FAKE_VERSION, SYSTEM_CHARS, spec)

    assert estimate == RecheckEstimate(ads=0, cost_usd=0.0)


def test_should_reject_negative_system_length(tmp_path: Path) -> None:
    store = _store(tmp_path, outdated=1)

    with pytest.raises(ValueError, match="system_chars must be zero or more, got -1"):
        estimate_recheck(store, FAKE_VERSION, -1, SPEC)


def test_should_return_count_when_system_prompt_empty(tmp_path: Path) -> None:
    store = _store(tmp_path, outdated=1)

    estimate = estimate_recheck(store, FAKE_VERSION, 0, None)

    assert estimate == RecheckEstimate(ads=1, cost_usd=None)
