from __future__ import annotations

import re

DEVELOPER_WORDS = re.compile(
    r"utvikl|develop|software|programvare|programm|engineer|ingeniør|\bdata"
    r"|frontend|backend|full.?stack|devops|plattform|platform|c\+\+|kotlin|java"
    r"|python|\.net|react|graduate|trainee|nyutdann|junior|system|kode|koding"
    r"|\bapp|tech|teknolog|cloud|sky|machine learning|maskinlæring|embedded"
    r"|integrasjon|integration|konsulent|consultant",
    re.IGNORECASE,
)
DEVELOPER_ACRONYMS = re.compile(r"\b(?:IT|IKT|AI|KI)\b")
SENIOR_TITLE_PATTERN = re.compile(
    r"\b(?:senior(?:utvikler|ingeniør|rådgiver)?|sr\.?|principal|staff|lead|"
    r"architect|arkitekt|erfaren|experienced)\b",
    re.I,
)
JUNIOR_TITLE_PATTERN = re.compile(r"\b(?:junior|graduate|nyutdannet)\b", re.I)


def looks_like_developer(*titles: str) -> bool:
    return any(
        DEVELOPER_WORDS.search(title) or DEVELOPER_ACRONYMS.search(title)
        for title in titles
    )


def title_is_senior(title: str) -> bool:
    return bool(SENIOR_TITLE_PATTERN.search(title)) and not bool(
        JUNIOR_TITLE_PATTERN.search(title)
    )
