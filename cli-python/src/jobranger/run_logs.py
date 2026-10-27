from __future__ import annotations

import logging
import re
from datetime import date
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

logger = logging.getLogger(__name__)

LOG_RETENTION_DAYS = 14
LOG_FORMAT = "%(asctime)s %(levelname)s %(name)s %(message)s"
LOG_FILE_PATTERN = re.compile(r"^(\d{4}-\d{2}-\d{2})\.log$")


def attach_file_handler(log_dir: Path, run_date: str) -> logging.Handler:
    log_dir.mkdir(parents=True, exist_ok=True)
    handler = logging.FileHandler(log_dir / f"{run_date}.log", encoding="utf-8")
    handler.setFormatter(logging.Formatter(LOG_FORMAT))
    logging.getLogger().addHandler(handler)
    return handler


def prune_old_logs(
    log_dir: Path, today: date, keep_days: int = LOG_RETENTION_DAYS
) -> int:
    if not log_dir.is_dir():
        return 0
    removed = 0
    for path in log_dir.iterdir():
        file_date = _log_file_date(path.name)
        if file_date is None or (today - file_date).days <= keep_days:
            continue
        path.unlink()
        removed += 1
    if removed:
        logger.info(
            "logs.pruned removed %d run logs older than %d days", removed, keep_days
        )
    return removed


def _log_file_date(name: str) -> date | None:
    match = LOG_FILE_PATTERN.match(name)
    if match is None:
        return None
    try:
        return date.fromisoformat(match.group(1))
    except ValueError:
        return None
