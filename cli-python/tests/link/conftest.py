from __future__ import annotations

from typing import TYPE_CHECKING

import pytest

if TYPE_CHECKING:
    from pathlib import Path

    from factories import MemoryKeyring


@pytest.fixture(autouse=True)
def link_config(config_dir: Path, memory_keyring: MemoryKeyring) -> Path:
    return config_dir
