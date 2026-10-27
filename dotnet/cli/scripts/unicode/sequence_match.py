from __future__ import annotations

import itertools
import json
import random
import sys
from difflib import SequenceMatcher
from pathlib import Path

PYTHON_VERSION = (3, 12)
REPO_ROOT = Path(__file__).resolve().parents[4]
TABLE = REPO_ROOT / "dotnet/cli/tests/Jobranger.Cli.Tests/TestData/Text/sequence-match.json"
SEED = 2026
EDITS_PER_TEXT = 6
TRUNCATED_EDITS = 2
AUTOJUNK_LENGTH = 200
EMOJI = "\U0001f600"
WORD_EDIT_SHARE = 0.7
REPLACEMENT_CHARACTERS = "æøåxyz "
INSERTED_WORDS = ("ny", "senior", EMOJI, "kotlin", "og")

TEXTS = [
    (
        "vi søker en utvikler som vil bygge tjenester for kundene våre i et team med erfarne kolleger der du får "
        "ansvar for backend og integrasjoner mot andre systemer og jobber tett med produkt og design"
    ),
    (
        "du blir en del av et lite team som lager mobilapper for android og ios med kotlin og swift og du deltar i "
        "hele livsløpet fra ide til drift og videre utvikling sammen med brukerne våre"
    ),
    (
        "we are looking for a developer who enjoys clean code automated tests and continuous delivery you will work "
        "with cloud platforms containers and observability and help the team ship small changes often"
    ),
    (
        "stillingen passer for deg som har bachelorgrad i informatikk og noen års erfaring med java eller c# og som "
        "liker å lære nye verktøy søknadsfrist er satt men vi behandler søknader fortløpende"
    ),
    (
        "org one as er et selskap i vekst med kontorer i bergen og oslo vi tilbyr fleksibel arbeidstid gode "
        "pensjonsordninger og et faglig sterkt miljø med mye kunnskapsdeling på tvers av teamene"
    ),
]

PAIRS = [
    ("", ""),
    ("", "a"),
    ("a", ""),
    ("abc", "abc"),
    ("abc", "abd"),
    ("abcd", "bcda"),
    ("private thread", "private volatile thread"),
    ("utvikler", "utviklere"),
    ("backend utvikler", "frontend utvikler"),
    ("a" * 199, "a" * 199),
    ("a" * 200, "a" * 200),
    ("a" * 201, "b" + "a" * 200),
    ("ab" * 150, "ba" * 150),
    ("xy" + "ab" * 99, "ab" * 100),
    ("xy" + "ab" * 99, "ab" * 99 + "a"),
    (EMOJI * 3 + "abc", "abc" + EMOJI * 3),
    ("søk" + EMOJI + "nå", "søk nå" + EMOJI),
]


def word_edit(rng: random.Random, text: str) -> str:
    words = text.split(" ")
    index = rng.randrange(len(words))
    kind = rng.choice(("replace", "delete", "insert", "swap"))
    if kind == "replace":
        words[index] = rng.choice(words)[::-1]
    elif kind == "delete":
        del words[index]
    elif kind == "insert":
        words.insert(index, rng.choice(INSERTED_WORDS))
    else:
        other = rng.randrange(len(words))
        words[index], words[other] = words[other], words[index]
    return " ".join(words)


def char_edit(rng: random.Random, text: str) -> str:
    index = rng.randrange(len(text))
    return text[:index] + rng.choice(REPLACEMENT_CHARACTERS) + text[index + 1 :]


def edited(rng: random.Random, text: str, count: int) -> str:
    for _ in range(count):
        text = (
            word_edit(rng, text)
            if rng.random() < WORD_EDIT_SHARE
            else char_edit(rng, text)
        )
    return text


def rows() -> list[dict[str, object]]:
    rng = random.Random(SEED)
    pairs = list(PAIRS)
    for text in TEXTS:
        for count in range(1, EDITS_PER_TEXT + 1):
            pairs.append((text, edited(rng, text, count * count)))
        pairs.append(
            (
                text[: AUTOJUNK_LENGTH - 1],
                edited(rng, text, TRUNCATED_EDITS)[: AUTOJUNK_LENGTH - 1],
            )
        )
    for left, right in itertools.pairwise(TEXTS):
        pairs.append((left, right))
        pairs.append((right + " " + left, left))
        pairs.append((left, right + " " + left))
    return [
        {"a": left, "b": right, "ratio": SequenceMatcher(None, left, right).ratio()}
        for left, right in pairs
    ]


def main() -> int:
    if sys.version_info[:2] != PYTHON_VERSION:
        raise SystemExit(f"run with Python {PYTHON_VERSION[0]}.{PYTHON_VERSION[1]}")
    TABLE.parent.mkdir(parents=True, exist_ok=True)
    body = ",\n".join(f"  {json.dumps(row, ensure_ascii=False)}" for row in rows())
    TABLE.write_text("[\n" + body + "\n]\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
