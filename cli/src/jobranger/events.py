from __future__ import annotations

import threading
from dataclasses import dataclass
from enum import StrEnum
from typing import Literal, Protocol

type Stage = Literal[
    "source_started", "source_done", "source_failed", "model", "apply_checks", "report"
]
type RunStatus = Literal["running", "completed", "cancelled", "failed", "locked"]


class RunMode(StrEnum):
    scheduled = "scheduled"
    manual = "manual"
    dry_run = "dry_run"


class CancelToken:
    def __init__(self) -> None:
        self._event = threading.Event()

    def cancel(self) -> None:
        self._event.set()

    @property
    def cancelled(self) -> bool:
        return self._event.is_set()


@dataclass(frozen=True)
class RunEvent:
    stage: Stage
    source: str | None = None
    found_new: int | None = None
    checked: int | None = None
    total: int | None = None


class EventSink(Protocol):
    def emit(self, event: RunEvent) -> None: ...


class NullSink:
    def emit(self, event: RunEvent) -> None:
        return None
