from __future__ import annotations

from dataclasses import dataclass
from typing import TYPE_CHECKING

from jobranger.paths import config_dir, write_private
from jobranger.settings import ConfigInvalidError

if TYPE_CHECKING:
    from pathlib import Path

GUIDANCE_LIMIT = 12_000
GUIDANCE_FILE_NAME = "guidance.md"
DEFAULT_TEXT = ""
LIMIT_ERROR = "guidance has {length} characters, the limit is {limit}"
UNREADABLE_ERROR = "guidance could not be read as UTF-8 text: {reason}"
BYTE_ORDER_MARK = "﻿"


@dataclass(frozen=True)
class Guidance:
    text: str

    @property
    def is_default(self) -> bool:
        return self.text == DEFAULT_TEXT


DEFAULT = Guidance(text=DEFAULT_TEXT)


def guidance_path() -> Path:
    return config_dir() / GUIDANCE_FILE_NAME


def load_guidance(path: Path) -> Guidance:
    if not path.exists():
        return DEFAULT
    try:
        raw = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as error:
        raise ConfigInvalidError(
            path, [UNREADABLE_ERROR.format(reason=error)]
        ) from error
    return Guidance(text=_validated(path, raw))


def save_guidance(path: Path, text: str) -> Guidance:
    normalized = _validated(path, text)
    if not normalized:
        return reset_guidance(path)
    write_private(path, f"{normalized}\n")
    return Guidance(text=normalized)


def reset_guidance(path: Path) -> Guidance:
    write_private(path, DEFAULT_TEXT)
    return DEFAULT


def _validated(path: Path, text: str) -> str:
    normalized = (
        text.replace(BYTE_ORDER_MARK, "")
        .replace("\r\n", "\n")
        .replace("\r", "\n")
        .strip()
    )
    if len(normalized) > GUIDANCE_LIMIT:
        raise ConfigInvalidError(
            path, [LIMIT_ERROR.format(length=len(normalized), limit=GUIDANCE_LIMIT)]
        )
    return normalized
