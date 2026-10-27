from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Example:
    action: str
    title: str
    company: str
    summary: str
    requirements: tuple[str, ...]
    distance: float
    decision_id: int
