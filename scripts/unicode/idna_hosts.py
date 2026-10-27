from __future__ import annotations

import json
import sys
from collections.abc import Callable
from pathlib import Path
from urllib.request import urlopen

UNICODE_VERSION = "16.0.0"
BASE = "https://www.unicode.org/Public"
IDNA_TABLE = f"{BASE}/idna/{UNICODE_VERSION}/IdnaMappingTable.txt"
NORMALIZATION = f"{BASE}/{UNICODE_VERSION}/ucd/DerivedNormalizationProps.txt"
COMBINING_CLASS = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedCombiningClass.txt"
BIDI_CLASS = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedBidiClass.txt"
GENERAL_CATEGORY = f"{BASE}/{UNICODE_VERSION}/ucd/extracted/DerivedGeneralCategory.txt"
OUTPUT = Path("contracts/unicode") / f"idna-hosts-{UNICODE_VERSION}.json"
CODE_POINTS = 0x110000
FIRST_NON_ASCII = 0x80
KEPT_STATUSES = {"valid", "deviation"}
JOINERS = {0x200C, 0x200D}
NFC_QUICK_CHECK = "NFC_QC"
STARTER_CLASS = "0"
RIGHT_TO_LEFT = {"R", "AL", "AN"}
MARKS = {"Mn", "Mc", "Me"}
RULE = (
    "IDNA status valid or deviation, NFC_QC Yes, canonical combining class 0, "
    "general category not a mark, bidi class not R, AL or AN, not ZWNJ or ZWJ"
)


def rows(url: str) -> list[tuple[int, int, list[str]]]:
    with urlopen(url) as response:
        text = response.read().decode("utf-8")
    found = []
    for line in text.splitlines():
        data = line.split("#", 1)[0].strip()
        if not data:
            continue
        fields = [field.strip() for field in data.split(";")]
        first, _, last = fields[0].partition("..")
        found.append((int(first, 16), int(last or first, 16), fields[1:]))
    return found


def flagged(url: str, matches: Callable[[str], bool]) -> bytearray:
    flags = bytearray(CODE_POINTS)
    for first, last, fields in rows(url):
        if matches(fields[0]):
            flags[first : last + 1] = b"\x01" * (last - first + 1)
    return flags


def kept_ranges() -> list[list[str]]:
    kept = flagged(IDNA_TABLE, lambda status: status in KEPT_STATUSES)
    excluded = [
        flagged(NORMALIZATION, lambda value: value == NFC_QUICK_CHECK),
        flagged(COMBINING_CLASS, lambda value: value != STARTER_CLASS),
        flagged(BIDI_CLASS, lambda value: value in RIGHT_TO_LEFT),
        flagged(GENERAL_CATEGORY, lambda value: value in MARKS),
    ]
    ranges: list[list[int]] = []
    for code in range(FIRST_NON_ASCII, CODE_POINTS):
        if not kept[code] or code in JOINERS or any(flags[code] for flags in excluded):
            continue
        if ranges and ranges[-1][1] == code - 1:
            ranges[-1][1] = code
        else:
            ranges.append([code, code])
    return [[f"{first:04X}", f"{last:04X}"] for first, last in ranges]


def main() -> int:
    header = {
        "property": "IDNA host code points mapped to themselves",
        "unicode_version": UNICODE_VERSION,
        "rule": RULE,
        "sources": [IDNA_TABLE, NORMALIZATION, COMBINING_CLASS, BIDI_CLASS, GENERAL_CATEGORY],
    }
    lines = [f"  {json.dumps(key)}: {json.dumps(value)}," for key, value in header.items()]
    ranges = ",\n".join(f"    {json.dumps(pair)}" for pair in kept_ranges())
    text = "{\n" + "\n".join(lines) + '\n  "ranges": [\n' + ranges + "\n  ]\n}\n"
    OUTPUT.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
