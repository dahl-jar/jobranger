from __future__ import annotations

import logging
from datetime import date
from pathlib import Path

from jobranger.run_logs import attach_file_handler, prune_old_logs


def test_should_write_run_log_to_date_file(tmp_path: Path) -> None:
    log_dir = tmp_path / "logs"
    logger = logging.getLogger("jobranger.test_run_logs")
    logger.setLevel(logging.INFO)

    handler = attach_file_handler(log_dir, "2026-09-16")
    try:
        logger.info("finn.search.done new=%d", 3)
        handler.flush()
    finally:
        logging.getLogger().removeHandler(handler)
        handler.close()

    assert "finn.search.done new=3" in (log_dir / "2026-09-16.log").read_text()


def test_should_prune_logs_older_than_fourteen_days(tmp_path: Path) -> None:
    names = ("2026-08-31.log", "2026-09-01.log", "2026-09-02.log", "notes.txt")
    for name in names:
        (tmp_path / name).write_text("x")

    removed = prune_old_logs(tmp_path, date(2026, 9, 16))

    assert removed == 2
    assert sorted(path.name for path in tmp_path.iterdir()) == [
        "2026-09-02.log",
        "notes.txt",
    ]
