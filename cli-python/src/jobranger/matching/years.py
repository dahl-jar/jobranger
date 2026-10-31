from __future__ import annotations

import re
from dataclasses import dataclass, replace
from typing import TYPE_CHECKING

from jobranger.matching.rules import (
    MATCH_ITEM_LIMIT,
    Match,
    clean_text,
    max_years,
    year_count,
)

if TYPE_CHECKING:
    from jobranger.settings import Limits

WRITTEN_NUMBERS: dict[str, int] = {
    "en": 1,
    "ett": 1,
    "to": 2,
    "tre": 3,
    "fire": 4,
    "fem": 5,
    "seks": 6,
    "sju": 7,
    "syv": 7,
    "åtte": 8,
    "ni": 9,
    "ti": 10,
    "one": 1,
    "two": 2,
    "three": 3,
    "four": 4,
    "five": 5,
    "six": 6,
    "seven": 7,
    "eight": 8,
    "nine": 9,
    "ten": 10,
}
PYTHON_ONLY_FOLDS = str.maketrans({"İ": "ɪ", "ı": "ɪ", "ſ": "ʃ"})
YEAR_PHRASE = re.compile(
    r"\b(?:(?:minimum(?: of)?|minst|at least) "
    r"((?:[0-9]{1,2}|en|ett|to|tre|fire|fem|seks|sju|syv|åtte|ni|ti|one|two|three"
    r"|four|five|six|seven|eight|nine|ten))"
    r"(?: ?[-–] ?(?:[0-9]{1,2}|en|ett|to|tre|fire|fem|seks|sju|syv|åtte|ni|ti|one"
    r"|two|three|four|five|six|seven|eight|nine|ten))?\+?"
    r"|([0-9]{1,2}) ?(?:\+|[-–] ?[0-9]{1,2})) (?:års|år|years|year|yrs)(?:['’]|\b)"
    r"(?: \S+){0,3}? (?:\S*erfaring|experience)",
    re.IGNORECASE,
)
ITEM_SPLIT = re.compile(r"[\r\n•·▪●◦]|[.!?;](?=[ \t])")
SOFT_HEADING = re.compile(r"\bvi ser gjerne at du(?: har)?\b", re.IGNORECASE)
PREFERENCE_WORD = re.compile(
    r"\b(?:fordel|ønskelig|preferred|desirable|advantage|a plus|nice to have)\b",
    re.IGNORECASE,
)
PREFERENCE_ADVERB = re.compile(r"\b(?:gjerne|helst|preferably)\b", re.IGNORECASE)
WINDOW_END = re.compile(
    r"(?:erfaring|kompetanse|kunnskap|kjennskap|experience|knowledge)"
    r"|, (?:og|and|men|but)\b",
    re.IGNORECASE,
)
GRADUATE_ROUTE = re.compile(
    r"\b(?:nyutdannede?|new graduates?|recent graduates?)\b", re.IGNORECASE
)
EQUIVALENT_ROUTE = re.compile(
    r"\b(?:eller tilsvarende|or equivalent|kompensere|compensate)\b", re.IGNORECASE
)
GUARD_REASON = (
    'The ad asks for at least {years} of experience: "{phrase}". '
    "The search accepts at most {cap}."
)
GUARD_BLOCKER = "Candidate does not have {years} of experience"


@dataclass(frozen=True)
class StatedYears:
    years: int
    phrase: str


def stated_years(text: str) -> StatedYears | None:
    if GRADUATE_ROUTE.search(_unfold(text)):
        return None
    best: StatedYears | None = None
    for segment in ITEM_SPLIT.split(text):
        item = " ".join(segment.split())
        unfolded = _unfold(item)
        for found in YEAR_PHRASE.finditer(unfolded):
            if _is_optional(unfolded, found):
                continue
            years = _years(found)
            if best is None or years < best.years:
                best = StatedYears(years, item[found.start() : found.end()])
    return best


def apply_years_guard(
    match: Match, title: str, description: str | None, limits: Limits
) -> Match:
    found = stated_years(f"{title}\n{description or ''}")
    if found is None:
        return match
    model = match.required_experience_years
    years = float(found.years) if model is None or model < found.years else model
    cap = max_years(limits)
    if cap is None or found.years <= cap:
        return replace(match, required_experience_years=years)
    stated = year_count(found.years)
    blocker = clean_text(GUARD_BLOCKER.format(years=stated))
    reason = clean_text(
        GUARD_REASON.format(years=stated, phrase=found.phrase, cap=year_count(cap))
    )
    blockers = (blocker, *(item for item in match.blockers if item != blocker))
    return replace(
        match,
        status="unsuitable",
        reason=reason,
        blockers=blockers[:MATCH_ITEM_LIMIT],
        required_experience_years=years,
    )


def _is_optional(item: str, found: re.Match[str]) -> bool:
    before = item[: found.start()]
    after = item[found.end() :]
    start = max((end.end() for end in WINDOW_END.finditer(before)), default=0)
    stop = WINDOW_END.search(after)
    lead = SOFT_HEADING.sub(" ", before[start:])
    tail = after if stop is None else after[: stop.start()]
    return bool(
        PREFERENCE_WORD.search(lead)
        or PREFERENCE_ADVERB.search(lead)
        or PREFERENCE_WORD.search(tail)
        or EQUIVALENT_ROUTE.search(tail)
    )


def _unfold(text: str) -> str:
    return text.translate(PYTHON_ONLY_FOLDS)


def _years(found: re.Match[str]) -> int:
    raw = found.group(1) or found.group(2)
    if raw[0].isascii() and raw[0].isdigit():
        return int(raw)
    return WRITTEN_NUMBERS[raw.lower()]
