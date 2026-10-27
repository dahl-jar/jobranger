from __future__ import annotations

import json
import sys
import unicodedata
from pathlib import Path

UNICODE_VERSION = unicodedata.unidata_version
REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT = REPO_ROOT / "cli/src/Jobranger.Cli/Secrets" / f"key-folds-{UNICODE_VERSION}.json"
CODE_POINTS = 0x110000
FIRST_NON_ASCII = 0x80
FIRST_PRINTABLE = 0x20
LAST_PRINTABLE = 0x7E
SURROGATES = range(0xD800, 0xE000)
FORM = "NFKC"
RULE = "code points whose NFKC form differs from the code point and is printable ASCII (U+0020 to U+007E)"


def hex_code(code: int) -> str:
    return f"{code:04X}"


def is_printable_ascii(text: str) -> bool:
    return all(FIRST_PRINTABLE <= ord(char) <= LAST_PRINTABLE for char in text)


def fold_rows() -> list[list[str]]:
    found = []
    for code in range(FIRST_NON_ASCII, CODE_POINTS):
        if code in SURROGATES:
            continue
        char = chr(code)
        folded = unicodedata.normalize(FORM, char)
        if folded != char and is_printable_ascii(folded):
            found.append([hex_code(code), folded])
    return found


def main() -> int:
    header = {
        "property": "NFKC folds to printable ASCII, used to find API key fragments",
        "unicode_version": UNICODE_VERSION,
        "rule": RULE,
        "source": f"Python unicodedata {UNICODE_VERSION}",
    }
    lines = [f"  {json.dumps(key)}: {json.dumps(value)}," for key, value in header.items()]
    body = ",\n".join(f"    {json.dumps(row)}" for row in fold_rows())
    text = "{\n" + "\n".join(lines) + '\n  "folds": [\n' + body + "\n  ]\n}\n"
    OUTPUT.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
