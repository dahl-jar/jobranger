from __future__ import annotations

ELLIPSIS = "…"
TRAILING_PUNCTUATION = " ,;:-"


def truncate_at_word(value: str, limit: int) -> str:
    if len(value) <= limit:
        return value
    shortened = value[: limit - 1].rsplit(maxsplit=1)[0].rstrip(TRAILING_PUNCTUATION)
    return f"{shortened or value[: limit - 1]}{ELLIPSIS}"
