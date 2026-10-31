from __future__ import annotations

import json
import sys
from collections.abc import Callable
from functools import cache
from pathlib import Path
from urllib.request import urlopen

UNICODE_VERSION = "16.0.0"
BASE = "https://www.unicode.org/Public"
IDNA_TABLE = f"{BASE}/idna/{UNICODE_VERSION}/IdnaMappingTable.txt"
UNICODE_DATA = f"{BASE}/{UNICODE_VERSION}/ucd/UnicodeData.txt"
NORMALIZATION = f"{BASE}/{UNICODE_VERSION}/ucd/DerivedNormalizationProps.txt"
COMBINING_CLASS = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedCombiningClass.txt"
BIDI_CLASS = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedBidiClass.txt"
GENERAL_CATEGORY = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedGeneralCategory.txt"
OUTPUT = Path("messages/unicode") / f"idna-hosts-{UNICODE_VERSION}.json"
CODE_POINTS = 0x110000
FIRST_NON_ASCII = 0x80
KEPT_STATUSES = {"valid", "deviation"}
MAPPED_STATUS = "mapped"
IGNORED_STATUS = "ignored"
JOINERS = {0x200C, 0x200D}
NFC_NO = ["NFC_QC", "N"]
COMPOSITION_EXCLUSION = "Full_Composition_Exclusion"
STARTER_CLASS = "0"
RIGHT_TO_LEFT = {"R", "AL", "AN"}
MARKS = {"Mn", "Mc", "Me"}
DECOMPOSITION_FIELD = 5
TAG_START = "<"
PAIR = 2
RULE = (
    "IDNA status valid or deviation, NFC_QC not No, canonical combining class 0, "
    "general category not a mark, bidi class not R, AL or AN, not ZWNJ or ZWJ"
)
MAPPING_RULE = "UTS 46 nontransitional: mapped rows replace the code point, ignored rows remove it, deviation rows stay"
NFC_RULE = (
    "canonical decompositions from UnicodeData.txt, compositions are two-part decompositions "
    "without Full_Composition_Exclusion, Hangul syllables are computed"
)

Row = tuple[int, int, list[str]]


@cache
def download(url: str) -> str:
    with urlopen(url) as response:
        return response.read().decode("utf-8")


def rows(url: str) -> list[Row]:
    found = []
    for line in download(url).splitlines():
        data = line.split("#", 1)[0].strip()
        if not data:
            continue
        fields = [field.strip() for field in data.split(";")]
        first, _, last = fields[0].partition("..")
        found.append((int(first, 16), int(last or first, 16), fields[1:]))
    return found


def flagged(url: str, matches: Callable[[list[str]], bool]) -> bytearray:
    flags = bytearray(CODE_POINTS)
    for first, last, fields in rows(url):
        if matches(fields):
            flags[first : last + 1] = b"\x01" * (last - first + 1)
    return flags


def hex_code(code: int) -> str:
    return f"{code:04X}"


def kept_ranges() -> list[list[str]]:
    kept = flagged(IDNA_TABLE, lambda fields: fields[0] in KEPT_STATUSES)
    excluded = [
        flagged(NORMALIZATION, lambda fields: fields == NFC_NO),
        flagged(COMBINING_CLASS, lambda fields: fields[0] != STARTER_CLASS),
        flagged(BIDI_CLASS, lambda fields: fields[0] in RIGHT_TO_LEFT),
        flagged(GENERAL_CATEGORY, lambda fields: fields[0] in MARKS),
    ]
    ranges: list[list[int]] = []
    for code in range(FIRST_NON_ASCII, CODE_POINTS):
        if not kept[code] or code in JOINERS or any(flags[code] for flags in excluded):
            continue
        if ranges and ranges[-1][1] == code - 1:
            ranges[-1][1] = code
        else:
            ranges.append([code, code])
    return [[hex_code(first), hex_code(last)] for first, last in ranges]


def mapped_rows() -> list[list[str]]:
    return [
        [hex_code(first), hex_code(last), fields[1]]
        for first, last, fields in rows(IDNA_TABLE)
        if fields[0] == MAPPED_STATUS
    ]


def ignored_rows() -> list[list[str]]:
    return [
        [hex_code(first), hex_code(last)] for first, last, fields in rows(IDNA_TABLE) if fields[0] == IGNORED_STATUS
    ]


def combining_class_rows() -> list[list[str | int]]:
    return [
        [hex_code(first), hex_code(last), int(fields[0])]
        for first, last, fields in rows(COMBINING_CLASS)
        if fields[0] != STARTER_CLASS
    ]


def decompositions() -> dict[int, list[int]]:
    found = {}
    for line in download(UNICODE_DATA).splitlines():
        fields = line.split(";")
        mapping = fields[DECOMPOSITION_FIELD]
        if mapping and not mapping.startswith(TAG_START):
            found[int(fields[0], 16)] = [int(part, 16) for part in mapping.split()]
    return found


def decomposition_rows() -> list[list[str]]:
    return [
        [hex_code(code), " ".join(hex_code(part) for part in parts)] for code, parts in sorted(decompositions().items())
    ]


def composition_rows() -> list[list[str]]:
    excluded = flagged(NORMALIZATION, lambda fields: fields[0] == COMPOSITION_EXCLUSION)
    return [
        [hex_code(parts[0]), hex_code(parts[1]), hex_code(code)]
        for code, parts in sorted(decompositions().items())
        if len(parts) == PAIR and not excluded[code]
    ]


def section(name: str, entries: list[list[str]] | list[list[str | int]]) -> str:
    body = ",\n".join(f"    {json.dumps(entry, ensure_ascii=False)}" for entry in entries)
    return f'  "{name}": [\n{body}\n  ]'


def main() -> int:
    header = {
        "property": "IDNA host code points mapped to themselves, UTS 46 mapping and NFC data",
        "unicode_version": UNICODE_VERSION,
        "rule": RULE,
        "mapping_rule": MAPPING_RULE,
        "nfc_rule": NFC_RULE,
        "sources": [IDNA_TABLE, UNICODE_DATA, NORMALIZATION, COMBINING_CLASS, BIDI_CLASS, GENERAL_CATEGORY],
    }
    lines = [f"  {json.dumps(key)}: {json.dumps(value)}," for key, value in header.items()]
    sections = [
        section("ranges", kept_ranges()),
        section("mapped", mapped_rows()),
        section("ignored", ignored_rows()),
        section("decompositions", decomposition_rows()),
        section("compositions", composition_rows()),
        section("combining_classes", combining_class_rows()),
    ]
    text = "{\n" + "\n".join(lines) + "\n" + ",\n".join(sections) + "\n}\n"
    OUTPUT.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
