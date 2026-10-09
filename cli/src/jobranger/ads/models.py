from __future__ import annotations

import html
import re
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from jobranger.matching.rules import Match

COMPANY_SUFFIXES = ("as", "asa", "sf", "ans")


@dataclass(frozen=True)
class Ad:
    id: str
    source: str
    source_id: str
    title: str
    company: str
    location: str
    published: str | None
    deadline: str | None
    url: str
    description: str | None = None
    label: str | None = None
    summary: str | None = None
    requires_master: bool = False
    is_programming_job: bool | None = None
    display_title: str | None = None
    role_type: str | None = None
    technologies: tuple[str, ...] = ()
    requirements: tuple[str, ...] = ()
    match: Match | None = None
    apply_url: str | None = None
    apply_status: str | None = None
    apply_checked_at: str | None = None
    republished: bool = False


@dataclass(frozen=True)
class SourceSnapshot:
    ads: list[Ad]
    active_ids: frozenset[str]


def dedup_key(company: str, title: str) -> str:
    return f"{_normalize(company)}:{_normalize(title)}"


def strip_html(html_text: str) -> str:
    without_tags = re.sub(r"<[^>]+>", " ", html_text)
    return re.sub(r"\s+", " ", html.unescape(without_tags)).strip()


def _normalize(value: str) -> str:
    words = re.sub(r"\s+", " ", value.lower()).strip().split(" ")
    while words and words[-1] in COMPANY_SUFFIXES:
        words.pop()
    return " ".join(words)
