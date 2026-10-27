from __future__ import annotations

import json
import logging
import math
import os
import re
import sys
import traceback
from contextvars import ContextVar
from datetime import datetime
from pathlib import Path
from typing import TextIO, override

LogValue = int | float | bool | str

RUN_ID: ContextVar[str | None] = ContextVar("run_id", default=None)
FIELDS_ATTR = "jobranger_fields"
MAX_STRING_CHARS = 64
INVALID_EVENT = "invalid_event"
EVENT_PATTERN = re.compile(r"[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+")
EVENT_TRAILING_PUNCTUATION = ",:;"
SURROGATES = re.compile("[\ud800-\udfff]")
REPLACEMENT_CHAR = "\ufffd"
LOG_SUFFIX = ".jsonl"
LOG_FILE_MODE = 0o600
LOG_DIR_MODE = 0o700
QUIET_LOGGERS = ("httpx", "httpx2", "httpcore")
ALLOWED_FIELDS = frozenset(
    {
        "source",
        "stage",
        "count",
        "new",
        "active",
        "seen",
        "found_new",
        "checked",
        "total",
        "expired",
        "unknown",
        "skipped",
        "attempt",
        "delay_s",
        "host",
        "status",
        "error_type",
        "calls",
        "input_tokens",
        "output_tokens",
        "cache_read",
        "cache_write",
        "cost_usd",
        "duration_ms",
        "pages",
        "window_hours",
        "provider",
        "model",
        "size",
        "returned",
        "expected",
        "removed",
        "keep_days",
        "messages",
        "ad_id",
        "requests",
        "reused",
        "batch_id",
        "guidance_default",
        "about_chars",
        "doc_count",
        "file",
        "failure",
    }
)


def bind_run_id(run_id: str) -> None:
    RUN_ID.set(run_id)


def log_event(
    logger: logging.Logger, level: int, event: str, /, **fields: LogValue
) -> None:
    logger.log(level, event, extra={FIELDS_ATTR: fields})


class JsonFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        return json.dumps(_safe_entry(record), allow_nan=False)


class ConsoleFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        entry = _safe_entry(record)
        details = [
            f"{key}={value}" for key, value in entry.items() if key in ALLOWED_FIELDS
        ]
        return _printable(
            " ".join([str(entry["level"]), str(entry["event"]), *details])
        )


class _DropFailedWrites(logging.Handler):
    @override
    def handleError(self, record: logging.LogRecord) -> None:
        pass


class _FileHandler(_DropFailedWrites, logging.FileHandler):
    pass


class _ConsoleHandler(_DropFailedWrites, logging.StreamHandler[TextIO]):
    pass


def _safe_entry(record: logging.LogRecord) -> dict[str, object]:
    try:
        return _entry(record)
    except Exception:
        return _base_entry(record, INVALID_EVENT)


def configure_logging(log_dir: Path, console_level: int | None) -> logging.Handler:
    log_dir.mkdir(mode=LOG_DIR_MODE, parents=True, exist_ok=True)
    log_dir.chmod(LOG_DIR_MODE)
    day = datetime.now().astimezone().date().isoformat()
    path = log_dir / f"{day}{LOG_SUFFIX}"
    _create_private(path)
    handler = _FileHandler(path, encoding="utf-8")
    handler.setFormatter(JsonFormatter())
    root = logging.getLogger()
    _remove_own_handlers(root)
    root.setLevel(logging.INFO)
    root.addHandler(handler)
    for name in QUIET_LOGGERS:
        logging.getLogger(name).setLevel(logging.WARNING)
    if console_level is not None:
        console = _ConsoleHandler(sys.stderr)
        console.setLevel(console_level)
        console.setFormatter(ConsoleFormatter())
        root.addHandler(console)
    return handler


def _entry(record: logging.LogRecord) -> dict[str, object]:
    entry = _base_entry(record, _event(record.msg))
    fields: dict[str, object] = getattr(record, FIELDS_ATTR, {})
    for key, value in fields.items():
        if key in ALLOWED_FIELDS:
            entry[key] = _scalar(value)
    if record.exc_info is not None and record.exc_info[0] is not None:
        error_type, _, trace = record.exc_info
        entry["error_type"] = _scalar(error_type.__name__)
        entry["trace"] = [
            f"{Path(frame.filename).name}:{frame.lineno} {frame.name}"
            for frame in traceback.extract_tb(trace)
        ]
    return entry


def _base_entry(record: logging.LogRecord, event: str) -> dict[str, object]:
    return {
        "ts": datetime.fromtimestamp(record.created)
        .astimezone()
        .isoformat(timespec="seconds"),
        "level": record.levelname.lower(),
        "logger": record.name,
        "event": event,
        "run_id": RUN_ID.get(),
    }


def _event(message: object) -> str:
    tokens = str(message).split(maxsplit=1)
    token = tokens[0].rstrip(EVENT_TRAILING_PUNCTUATION) if tokens else ""
    if len(token) <= MAX_STRING_CHARS and EVENT_PATTERN.fullmatch(token):
        return token
    return INVALID_EVENT


def _scalar(value: object) -> LogValue | None:
    if isinstance(value, str):
        return SURROGATES.sub(REPLACEMENT_CHAR, value[:MAX_STRING_CHARS])
    if isinstance(value, float) and not math.isfinite(value):
        return str(value)
    if isinstance(value, int | float):
        return value
    return None


def _printable(text: str) -> str:
    return "".join(
        char if char.isprintable() else char.encode("unicode_escape").decode("ascii")
        for char in text
    )


def _create_private(path: Path) -> None:
    os.close(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_APPEND, LOG_FILE_MODE))
    path.chmod(LOG_FILE_MODE)


def _remove_own_handlers(root: logging.Logger) -> None:
    for handler in root.handlers[:]:
        if isinstance(handler.formatter, JsonFormatter | ConsoleFormatter):
            root.removeHandler(handler)
            handler.close()
