from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from datetime import date

    from jobranger.providers.base import Usage
    from jobranger.storage.store import Store

ADS_PER_CALL = 1


def record(store: Store, usage: Usage, day: date) -> None:
    store.add_usage(day.isoformat(), usage, ads_checked=ADS_PER_CALL)
