from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

DEFAULT_DB_PATH = "/data/jobranger.db"
DEFAULT_REPORT_DIR = "/data/reports"
DEFAULT_LOG_DIR = "/data/logs"
DEFAULT_FINN_OCCUPATIONS = "0.23"
DEFAULT_CLAUDE_BIN = "claude"
TRUE_ENV_VALUES = frozenset({"1", "true", "yes"})


@dataclass(frozen=True)
class Settings:
    db_path: Path
    report_dir: Path
    finn_occupations: str
    discord_webhook_url: str
    claude_bin: str
    report_all: bool
    reclassify_all: bool
    log_dir: Path = Path(DEFAULT_LOG_DIR)

    @classmethod
    def from_env(cls) -> Settings:
        return cls(
            db_path=Path(os.environ.get("DB_PATH", DEFAULT_DB_PATH)),
            report_dir=Path(os.environ.get("REPORT_DIR", DEFAULT_REPORT_DIR)),
            finn_occupations=os.environ.get(
                "FINN_OCCUPATIONS", DEFAULT_FINN_OCCUPATIONS
            ),
            discord_webhook_url=os.environ.get("DISCORD_WEBHOOK_URL", ""),
            claude_bin=os.environ.get("CLAUDE_BIN", DEFAULT_CLAUDE_BIN),
            report_all=os.environ.get("REPORT_ALL", "").lower() in TRUE_ENV_VALUES,
            reclassify_all=(
                os.environ.get("RECLASSIFY_ALL", "").lower() in TRUE_ENV_VALUES
            ),
            log_dir=Path(os.environ.get("LOG_DIR", DEFAULT_LOG_DIR)),
        )
