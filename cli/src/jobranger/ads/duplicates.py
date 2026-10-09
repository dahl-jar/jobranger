from __future__ import annotations

import re
from difflib import SequenceMatcher
from urllib.parse import urlsplit, urlunsplit

from jobranger.ads.models import COMPANY_SUFFIXES, Ad, dedup_key

DESCRIPTION_MATCH_MIN_LENGTH = 120
TITLE_SIMILARITY_THRESHOLD = 0.86
DESCRIPTION_TITLE_SIMILARITY_THRESHOLD = 0.5
DESCRIPTION_SIMILARITY_THRESHOLD = 0.92
DIRECT_SOURCE_PREFIXES = ("ashby:", "teamtailor:")
MIN_COMPANY_TOKEN_LENGTH = 3
VACANCY_JACCARD_THRESHOLD = 0.9
SHINGLE_SIZE = 5
COMPANY_NOISE = frozenset(
    {*COMPANY_SUFFIXES, "hf", "norge", "norway", "group", "services"}
)
COMPANY_CUT_WORDS = ("avd", "via")


def group_duplicates(ads: list[Ad]) -> list[list[Ad]]:
    groups: list[list[Ad]] = []
    for ad in ads:
        group = next(
            (candidate for candidate in groups if _is_duplicate(ad, candidate[0])),
            None,
        )
        if group is None:
            groups.append([ad])
        else:
            group.append(ad)
    return groups


def is_same_vacancy(left: Ad, right: Ad) -> bool:
    if dedup_key(left.company, left.title) == dedup_key(right.company, right.title):
        return True
    if not companies_match(left.company, right.company):
        return False
    if not _locations_overlap(left.location, right.location):
        return False
    if _normalized(left.title) == _normalized(right.title):
        return True
    return (
        shingle_jaccard(left.description or "", right.description or "")
        >= VACANCY_JACCARD_THRESHOLD
    )


def shingle_jaccard(left: str, right: str) -> float:
    left_shingles = _shingles(left)
    right_shingles = _shingles(right)
    if not left_shingles or not right_shingles:
        return 0.0
    return len(left_shingles & right_shingles) / len(left_shingles | right_shingles)


def split_reported(ads: list[Ad], reported: list[Ad]) -> tuple[list[Ad], list[str]]:
    remaining: list[Ad] = []
    covered: list[str] = []
    for ad in ads:
        if any(is_same_vacancy(ad, other) for other in reported):
            covered.append(ad.id)
        else:
            remaining.append(ad)
    return remaining, covered


def _locations_overlap(left: str, right: str) -> bool:
    left_words = set(_normalized(left).split())
    right_words = set(_normalized(right).split())
    return not left_words or not right_words or bool(left_words & right_words)


def _shingles(text: str) -> frozenset[tuple[str, ...]]:
    normalized = _normalized(text)
    if len(normalized) < DESCRIPTION_MATCH_MIN_LENGTH:
        return frozenset()
    words = normalized.split()
    return frozenset(
        tuple(words[start : start + SHINGLE_SIZE])
        for start in range(len(words) - SHINGLE_SIZE + 1)
    )


def _is_duplicate(left: Ad, right: Ad) -> bool:
    if _canonical_url(left.url) == _canonical_url(right.url):
        return True
    if is_same_vacancy(left, right):
        return True
    if not companies_match(left.company, right.company):
        return False
    if _normalized(left.location) != _normalized(right.location):
        return False
    title_similarity = SequenceMatcher(
        None, _normalized(left.title), _normalized(right.title)
    ).ratio()
    if title_similarity >= TITLE_SIMILARITY_THRESHOLD:
        return True
    left_description = _normalized(left.description or "")
    right_description = _normalized(right.description or "")
    return _descriptions_match(left_description, right_description, title_similarity)


def _descriptions_match(left: str, right: str, title_similarity: float) -> bool:
    if min(len(left), len(right)) < DESCRIPTION_MATCH_MIN_LENGTH:
        return False
    if left == right:
        return True
    if title_similarity < DESCRIPTION_TITLE_SIMILARITY_THRESHOLD:
        return False
    return (
        SequenceMatcher(None, left, right).ratio() >= DESCRIPTION_SIMILARITY_THRESHOLD
    )


def is_direct_source(ad: Ad) -> bool:
    return ad.source.startswith(DIRECT_SOURCE_PREFIXES)


def companies_match(left: str, right: str) -> bool:
    left_tokens = _company_tokens(left)
    right_tokens = _company_tokens(right)
    if not left_tokens or not right_tokens:
        return False
    if left_tokens == right_tokens:
        return True
    smaller, larger = sorted((left_tokens, right_tokens), key=len)
    return smaller <= larger and any(
        len(token) >= MIN_COMPANY_TOKEN_LENGTH for token in smaller
    )


def _company_tokens(value: str) -> frozenset[str]:
    words = _normalized(value).split()
    for cut in COMPANY_CUT_WORDS:
        if cut in words:
            words = words[: words.index(cut)]
    return frozenset(word for word in words if word not in COMPANY_NOISE)


def _canonical_url(url: str) -> str:
    parsed = urlsplit(url)
    return urlunsplit(
        (parsed.scheme.lower(), parsed.netloc.lower(), parsed.path.rstrip("/"), "", "")
    )


def _normalized(value: str) -> str:
    return re.sub(r"[^\w+#]+", " ", value.casefold()).strip()
