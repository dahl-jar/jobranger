from __future__ import annotations

import re
from dataclasses import dataclass, replace
from typing import TYPE_CHECKING

from jobranger.matching.rules import MATCH_ITEM_LIMIT, Match, clean_text, max_years

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
    r"\b(?:gjerne|helst|fordel|ønskelig|preferred|preferably|desirable|advantage"
    r"|a plus|nice to have)\b",
    re.IGNORECASE,
)
GRADUATE_ROUTE = re.compile(
    r"\b(?:nyutdannede?|new graduates?|recent graduates?)\b", re.IGNORECASE
)
EQUIVALENT_ROUTE = re.compile(r"\b(?:eller tilsvarende|or equivalent)\b", re.IGNORECASE)
GUARD_REASON = (
    'The ad asks for at least {years} of experience: "{phrase}". '
    "The search accepts at most {cap}."
)
GUARD_BLOCKER = "Candidate does not have {years} of experience"
ONE_YEAR = 1
WINDOW_WORDS = 6


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
    stated = _year_count(found.years)
    blocker = clean_text(GUARD_BLOCKER.format(years=stated))
    reason = clean_text(
        GUARD_REASON.format(years=stated, phrase=found.phrase, cap=_year_count(cap))
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
    before = " ".join(_words(item[: found.start()])[-WINDOW_WORDS:])
    after = " ".join(_words(item[found.end() :])[:WINDOW_WORDS])
    return bool(
        PREFERENCE_WORD.search(before)
        or PREFERENCE_WORD.search(after)
        or EQUIVALENT_ROUTE.search(after)
    )


def _words(text: str) -> list[str]:
    return SOFT_HEADING.sub(" ", text).split()


def _year_count(years: float) -> str:
    unit = "year" if years == ONE_YEAR else "years"
    return f"{years:g} {unit}"


def _unfold(text: str) -> str:
    return text.translate(PYTHON_ONLY_FOLDS)


def _years(found: re.Match[str]) -> int:
    raw = found.group(1) or found.group(2)
    if raw[0].isascii() and raw[0].isdigit():
        return int(raw)
    return WRITTEN_NUMBERS[raw.lower()]
