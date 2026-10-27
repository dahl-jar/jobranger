from __future__ import annotations

import re
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from jobranger.catalogue import Level

LEAD_TITLE_PATTERN = re.compile(
    r"\b(?:lead|principal|staff|architect|arkitekt|tech lead|teamleder)\b", re.I
)
SENIOR_TITLE_PATTERN = re.compile(
    r"\b(?:senior(?:utvikler|ingeniør|rådgiver)?|sr\.?|erfaren|experienced)\b", re.I
)
JUNIOR_TITLE_PATTERN = re.compile(
    r"\b(?:junior|graduate|nyutdannet|trainee|intern|praktikant|sommerjobb)\b", re.I
)


def title_level(title: str) -> Level | None:
    if JUNIOR_TITLE_PATTERN.search(title):
        return None
    if LEAD_TITLE_PATTERN.search(title):
        return "lead"
    if SENIOR_TITLE_PATTERN.search(title):
        return "senior"
    return None
