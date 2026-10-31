from __future__ import annotations

import json
from pathlib import Path

LINE_BREAK = "\n"


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


def escape_invisible(text: str) -> str:
    return "".join(
        json.dumps(character)[1:-1]
        if not character.isprintable() and character != LINE_BREAK
        else character
        for character in text
    )


def write_json(path: Path, value: object) -> None:
    text = json.dumps(value, ensure_ascii=False, indent=2) + LINE_BREAK
    write_text(path, escape_invisible(text))
