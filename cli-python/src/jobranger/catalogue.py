from __future__ import annotations

import functools
import re
from dataclasses import dataclass, replace
from importlib import resources
from typing import TYPE_CHECKING, Literal

from pydantic import BaseModel, ConfigDict

if TYPE_CHECKING:
    from collections.abc import Iterable, Sequence

    from jobranger.settings import Targets

type RoleId = Literal[
    "backend",
    "frontend",
    "fullstack",
    "mobile",
    "data_engineering",
    "devops_platform",
    "ml_ai",
    "embedded",
    "game",
]
type RoleType = Literal[
    "backend",
    "frontend",
    "fullstack",
    "mobile",
    "data_engineering",
    "devops_platform",
    "ml_ai",
    "embedded",
    "game",
    "general_software",
]
type Level = Literal["junior", "mid", "senior", "lead", "none"]
type YearBand = Literal["0-1", "1-3", "3-6", "6+"]

CATALOGUE_FILE = "catalogue.json"
GENERAL_SOFTWARE = "general_software"
OTHER_ROLE_LABEL = "Other"
SENIOR_LEVELS: frozenset[Level] = frozenset({"senior", "lead"})


class EmployerFeed(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    kind: Literal["ashby", "teamtailor"]
    key: str
    company: str
    board: str

    @property
    def source(self) -> str:
        return f"{self.kind}:{self.key}"


class RoleEntry(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    label: str
    linkedin_keywords: tuple[str, ...]
    nav_styrk: tuple[str, ...]
    nav_categories: tuple[tuple[str, str], ...]
    finn_occupations: tuple[str, ...]
    title_words: tuple[str, ...]


class Catalogue(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    base: RoleEntry
    roles: dict[RoleId, RoleEntry]
    acronyms: tuple[str, ...]
    employers: tuple[EmployerFeed, ...]


@dataclass(frozen=True)
class SourcePlan:
    linkedin_keywords: tuple[str, ...]
    nav_styrk: tuple[str, ...]
    nav_categories: tuple[tuple[str, str], ...]
    finn_occupations: tuple[str, ...]
    title_pattern: re.Pattern[str]
    skip_title_levels: frozenset[Level]

    def title_matches(self, *titles: str) -> bool:
        return any(self.title_pattern.search(title) for title in titles)


@functools.cache
def load_catalogue() -> Catalogue:
    text = resources.files("jobranger").joinpath(CATALOGUE_FILE).read_text("utf-8")
    return Catalogue.model_validate_json(text)


def catalogue_for(subfields: Sequence[RoleId]) -> SourcePlan:
    catalogue = load_catalogue()
    entries = [catalogue.base, *(catalogue.roles[role] for role in subfields)]
    words = _union(entry.title_words for entry in entries)
    return SourcePlan(
        linkedin_keywords=_union(entry.linkedin_keywords for entry in entries),
        nav_styrk=_union(entry.nav_styrk for entry in entries),
        nav_categories=_union(entry.nav_categories for entry in entries),
        finn_occupations=_union(entry.finn_occupations for entry in entries),
        title_pattern=_title_pattern(words, catalogue.acronyms),
        skip_title_levels=SENIOR_LEVELS,
    )


def source_plan(targets: Targets) -> SourcePlan:
    return replace(
        catalogue_for(targets.subfields),
        skip_title_levels=SENIOR_LEVELS - targets.limits.levels,
    )


def role_label(role: str | None) -> str:
    catalogue = load_catalogue()
    if role == GENERAL_SOFTWARE:
        return catalogue.base.label
    for role_id, entry in catalogue.roles.items():
        if role_id == role:
            return entry.label
    return OTHER_ROLE_LABEL


def role_list() -> list[tuple[RoleId, str]]:
    return [(role, entry.label) for role, entry in load_catalogue().roles.items()]


def default_employer_feeds() -> tuple[EmployerFeed, ...]:
    return load_catalogue().employers


def _union[T](groups: Iterable[tuple[T, ...]]) -> tuple[T, ...]:
    seen: dict[T, None] = {}
    for group in groups:
        for item in group:
            seen.setdefault(item, None)
    return tuple(seen)


def _title_pattern(words: Sequence[str], acronyms: Sequence[str]) -> re.Pattern[str]:
    escaped = "|".join(re.escape(word.lower()) for word in words)
    exact = "|".join(re.escape(acronym) for acronym in acronyms)
    return re.compile(rf"(?i:{escaped})|\b(?:{exact})\b")
