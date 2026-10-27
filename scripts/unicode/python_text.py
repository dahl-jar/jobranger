from __future__ import annotations

import html
import json
import re
import sys
import unicodedata
from html.entities import html5
from pathlib import Path

UNICODE_VERSION = "15.0.0"
PYTHON_VERSION = (3, 12)
REPO_ROOT = Path(__file__).resolve().parents[2]
TEXT_DIR = REPO_ROOT / "cli/src/Jobranger.Cli/Text"
ENTITIES = TEXT_DIR / "entities.json"
TABLES = TEXT_DIR / f"python-text-{UNICODE_VERSION}.json"
CODE_POINTS = 0x110000
ENTITY_PREFIX = "&"
IGNORE_CASE_LETTERS = "abcdefghijklmnopqrstuvwxyzæøå. "
WORD = re.compile(r"\w")
HIDDEN_CATEGORIES = frozenset({"Cc", "Cs", "Cn"})
URL_DELIMITERS = "/?#@:"


def hex_code(code: int) -> str:
    return f"{code:04X}"


def hex_text(text: str) -> str:
    return " ".join(hex_code(ord(char)) for char in text)


def ranges(test) -> list[list[str]]:
    found: list[list[str]] = []
    start = None
    for code in range(CODE_POINTS + 1):
        inside = code < CODE_POINTS and test(chr(code))
        if inside and start is None:
            start = code
        elif not inside and start is not None:
            found.append([hex_code(start), hex_code(code - 1)])
            start = None
    return found


def casefold_rows() -> list[list[str]]:
    rows = []
    for code in range(CODE_POINTS):
        char = chr(code)
        folded = char.casefold()
        if folded != char:
            rows.append([hex_code(code), hex_text(folded)])
    return rows


def ignore_case_rows() -> list[list[str]]:
    rows = []
    for code in range(CODE_POINTS):
        char = chr(code)
        if char in IGNORE_CASE_LETTERS:
            continue
        targets = [
            letter
            for letter in IGNORE_CASE_LETTERS
            if re.fullmatch(re.escape(letter), char, re.IGNORECASE)
        ]
        if len(targets) > 1:
            raise ValueError(f"{hex_code(code)} matches {targets}")
        if targets:
            rows.append([hex_code(code), hex_code(ord(targets[0]))])
    return rows


def charref_rows() -> list[list[str]]:
    return [
        [hex_code(code), hex_text(text)]
        for code, text in sorted(html._invalid_charrefs.items())
    ]


def dropped_ranges() -> list[list[str]]:
    dropped = html._invalid_codepoints
    return ranges(lambda char: ord(char) in dropped)


def expands_to_url_delimiter(char: str) -> bool:
    if char.isascii():
        return False
    normalized = unicodedata.normalize("NFKC", char)
    return any(mark in normalized for mark in URL_DELIMITERS)


def section(name: str, entries: list[list[str]]) -> str:
    body = ",\n".join(f"    {json.dumps(entry)}" for entry in entries)
    return f'  "{name}": [\n{body}\n  ]'


def write_tables() -> None:
    header = {
        "property": "Python str and re behaviour the C# text helpers copy",
        "unicode_version": UNICODE_VERSION,
        "python_version": ".".join(str(part) for part in PYTHON_VERSION),
        "ignore_case_letters": IGNORE_CASE_LETTERS,
    }
    lines = [f"  {json.dumps(key)}: {json.dumps(value)}," for key, value in header.items()]
    sections = [
        section("word", ranges(lambda char: WORD.fullmatch(char) is not None)),
        section("space", ranges(str.isspace)),
        section("casefold", casefold_rows()),
        section("ignore_case", ignore_case_rows()),
        section("charrefs", charref_rows()),
        section("dropped_charrefs", dropped_ranges()),
        section(
            "hidden",
            ranges(lambda char: unicodedata.category(char) in HIDDEN_CATEGORIES),
        ),
        section("unprintable", ranges(lambda char: not char.isprintable())),
        section("nfkc_delimiters", ranges(expands_to_url_delimiter)),
    ]
    text = "{\n" + "\n".join(lines) + "\n" + ",\n".join(sections) + "\n}\n"
    TABLES.write_text(text, encoding="utf-8")


def write_entities() -> None:
    lines = [
        f"  {json.dumps(ENTITY_PREFIX + name)}: "
        + json.dumps({"codepoints": [ord(char) for char in text], "characters": text})
        for name, text in sorted(html5.items())
    ]
    ENTITIES.write_text("{\n" + ",\n".join(lines) + "\n}\n", encoding="utf-8")


def main() -> int:
    if sys.version_info[:2] != PYTHON_VERSION:
        raise SystemExit(f"run with Python {PYTHON_VERSION[0]}.{PYTHON_VERSION[1]}")
    if unicodedata.unidata_version != UNICODE_VERSION:
        raise SystemExit(f"needs Unicode {UNICODE_VERSION}")
    write_tables()
    write_entities()
    return 0


if __name__ == "__main__":
    sys.exit(main())
