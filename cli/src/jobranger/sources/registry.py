from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class AshbyBoard:
    key: str
    company: str
    board_name: str

    @property
    def source(self) -> str:
        return f"ashby:{self.key}"


@dataclass(frozen=True)
class TeamtailorFeed:
    key: str
    company: str
    url: str

    @property
    def source(self) -> str:
        return f"teamtailor:{self.key}"


ASHBY_BOARDS: tuple[AshbyBoard, ...] = ()
TEAMTAILOR_FEEDS: tuple[TeamtailorFeed, ...] = ()
