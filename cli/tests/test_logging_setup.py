from __future__ import annotations

import io
import json
import logging
import stat
from datetime import datetime
from pathlib import Path
from typing import TYPE_CHECKING

import httpx2
import pytest

from jobranger.logging_setup import (
    FIELDS_ATTR,
    INVALID_EVENT,
    LOG_SUFFIX,
    MAX_STRING_CHARS,
    RUN_ID,
    ConsoleFormatter,
    JsonFormatter,
    bind_run_id,
    configure_logging,
    log_event,
)

if TYPE_CHECKING:
    from collections.abc import Iterator

LOGGER_NAME = "jobranger.test_logging_setup"
LEAKED_TEXT = "Senior developer wanted, CV attached"
FAKE_KEY = "sk-ant-api03-" + "x" * 40


class BrokenStream(io.StringIO):
    def write(self, text: str) -> int:
        raise OSError(text)


class ListHandler(logging.Handler):
    def __init__(self) -> None:
        super().__init__()
        self.lines: list[str] = []

    def emit(self, record: logging.LogRecord) -> None:
        self.lines.append(self.format(record))


@pytest.fixture
def logger() -> Iterator[logging.Logger]:
    test_logger = logging.getLogger(LOGGER_NAME)
    test_logger.setLevel(logging.INFO)
    yield test_logger
    test_logger.handlers.clear()
    test_logger.setLevel(logging.NOTSET)


@pytest.fixture
def captured(logger: logging.Logger) -> ListHandler:
    handler = ListHandler()
    handler.setFormatter(JsonFormatter())
    logger.addHandler(handler)
    return handler


@pytest.fixture(autouse=True)
def unbound_run_id() -> Iterator[None]:
    token = RUN_ID.set(None)
    yield
    RUN_ID.reset(token)


@pytest.fixture
def root_logger() -> Iterator[logging.Logger]:
    root = logging.getLogger()
    handlers = root.handlers[:]
    level = root.level
    yield root
    for handler in root.handlers:
        if handler not in handlers:
            handler.close()
    root.handlers[:] = handlers
    root.setLevel(level)
    logging.getLogger("httpx").setLevel(logging.NOTSET)
    logging.getLogger("httpcore").setLevel(logging.NOTSET)
    logging.getLogger("httpx2").setLevel(logging.NOTSET)


def entries(handler: ListHandler) -> list[dict[str, object]]:
    return [json.loads(line) for line in handler.lines]


def only_log_file(log_dir: Path) -> Path:
    [log_file] = log_dir.glob(f"*{LOG_SUFFIX}")
    return log_file


def own_handlers(root: logging.Logger) -> list[logging.Handler]:
    return [
        handler
        for handler in root.handlers
        if isinstance(handler.formatter, JsonFormatter | ConsoleFormatter)
    ]


def reject_constant(name: str) -> object:
    raise ValueError(name)


def test_should_drop_fields_outside_allowlist(
    logger: logging.Logger, captured: ListHandler
) -> None:
    log_event(logger, logging.INFO, "source.done", source="nav", cv_text=LEAKED_TEXT)

    [entry] = entries(captured)
    assert entry["source"] == "nav"
    assert "cv_text" not in entry


def test_should_write_base_fields_on_each_line(
    logger: logging.Logger, captured: ListHandler
) -> None:
    log_event(logger, logging.INFO, "run.started")

    [entry] = entries(captured)
    assert (sorted(entry), entry["level"], entry["logger"]) == (
        ["event", "level", "logger", "run_id", "ts"],
        "info",
        LOGGER_NAME,
    )


def test_should_keep_prompt_context_fields(
    logger: logging.Logger, captured: ListHandler
) -> None:
    log_event(
        logger,
        logging.INFO,
        "prompt.built",
        guidance_default=False,
        about_chars=120,
        doc_count=1,
    )

    [entry] = entries(captured)
    assert (entry["guidance_default"], entry["about_chars"], entry["doc_count"]) == (
        False,
        120,
        1,
    )


def test_should_never_render_format_args(
    logger: logging.Logger, captured: ListHandler
) -> None:
    logger.info("classify.chunk.unreadable output=%r", LEAKED_TEXT)

    [line] = captured.lines
    assert json.loads(line)["event"] == "classify.chunk.unreadable"
    assert LEAKED_TEXT not in line


def test_should_cut_long_strings_to_limit(
    logger: logging.Logger, captured: ListHandler
) -> None:
    log_event(logger, logging.INFO, "source.done", source="x" * 100)

    [entry] = entries(captured)
    assert entry["source"] == "x" * MAX_STRING_CHARS


def test_should_tag_lines_with_bound_run_id(
    logger: logging.Logger, captured: ListHandler
) -> None:
    bind_run_id("run-1")

    log_event(logger, logging.INFO, "run.started")

    [entry] = entries(captured)
    assert entry["run_id"] == "run-1"


def raise_with_secret_message() -> None:
    raise ValueError(LEAKED_TEXT)


def test_should_log_exception_type_without_message(
    logger: logging.Logger, captured: ListHandler
) -> None:
    try:
        raise_with_secret_message()
    except ValueError:
        logger.exception("source.failed reading %s", "nav")

    [line] = captured.lines
    entry = json.loads(line)
    assert entry["error_type"] == "ValueError"
    assert entry["trace"][-1].endswith(" raise_with_secret_message")
    assert LEAKED_TEXT not in line


def test_should_write_json_lines_to_dated_file(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    handler = configure_logging(tmp_path, None)

    log_event(logging.getLogger(LOGGER_NAME), logging.INFO, "run.started", count=2)
    handler.flush()

    [log_file] = tmp_path.glob("????-??-??.jsonl")
    entry = json.loads(log_file.read_text())
    assert (entry["event"], entry["count"]) == ("run.started", 2)


def test_should_keep_httpx_info_lines_out_of_log_file(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    handler = configure_logging(tmp_path, None)

    logging.getLogger("httpx").info("HTTP Request: GET %s", "https://example.test")
    handler.flush()

    assert only_log_file(tmp_path).read_text() == ""


def test_should_keep_format_args_out_of_console_lines(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    configure_logging(tmp_path, logging.WARNING)

    logging.getLogger(LOGGER_NAME).warning("source.failed body=%s", LEAKED_TEXT)

    err = capsys.readouterr().err
    assert "source.failed" in err
    assert LEAKED_TEXT not in err


def test_should_print_level_event_and_fields_on_console(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    configure_logging(tmp_path, logging.INFO)

    log_event(logging.getLogger(LOGGER_NAME), logging.INFO, "source.done", source="nav")

    assert capsys.readouterr().err == "info source.done source=nav\n"


def test_should_hide_lines_below_console_level(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    configure_logging(tmp_path, logging.WARNING)

    logging.getLogger(LOGGER_NAME).info("source.done")

    assert capsys.readouterr().err == ""


def test_should_skip_console_output_when_console_level_is_none(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    configure_logging(tmp_path, None)

    logging.getLogger(LOGGER_NAME).warning("source.failed")

    assert capsys.readouterr().err == ""


def test_should_keep_sdk_request_lines_out_of_log_file(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    handler = configure_logging(tmp_path, None)
    client = httpx2.Client(
        transport=httpx2.MockTransport(lambda _: httpx2.Response(200))
    )

    client.get("https://api.example.test/v1/messages")
    handler.flush()

    assert only_log_file(tmp_path).read_text() == ""


@pytest.mark.parametrize(
    "message",
    [
        f"{FAKE_KEY} invalid",
        ValueError(f"{FAKE_KEY} invalid"),
        "0123456789abcdef0123456789abcdef rejected",
        f"{FAKE_KEY}\nrun.started",
        "run." + "x" * (MAX_STRING_CHARS - 3),
        "",
    ],
    ids=["key-first", "exception", "lowercase-key", "multi-line", "too-long", "empty"],
)
def test_should_replace_event_that_is_not_an_event_name(
    message: object, logger: logging.Logger, captured: ListHandler
) -> None:
    logger.warning(message)

    [line] = captured.lines
    assert json.loads(line)["event"] == INVALID_EVENT
    assert FAKE_KEY not in line


def test_should_keep_event_name_at_length_limit(
    logger: logging.Logger, captured: ListHandler
) -> None:
    event = "run." + "x" * (MAX_STRING_CHARS - 4)

    logger.info(event)

    [entry] = entries(captured)
    assert entry["event"] == event


def test_should_keep_event_name_followed_by_comma(
    logger: logging.Logger, captured: ListHandler
) -> None:
    logger.info("nav.feed.failed, position saved at last completed page")

    [entry] = entries(captured)
    assert entry["event"] == "nav.feed.failed"


def test_should_escape_control_characters_on_console(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    configure_logging(tmp_path, logging.INFO)

    log_event(
        logging.getLogger(LOGGER_NAME),
        logging.INFO,
        "source.done",
        source="\x1b[2J\x1b]0;title\x07\r\nerror forged.line",
    )

    err = capsys.readouterr().err
    assert err.count("\n") == 1
    assert not any(char in err for char in "\x1b\x07\r")


def test_should_write_non_finite_floats_as_strings(
    logger: logging.Logger, captured: ListHandler
) -> None:
    log_event(logger, logging.INFO, "budget.spent", cost_usd=float("nan"))

    [line] = captured.lines
    assert json.loads(line, parse_constant=reject_constant)["cost_usd"] == "nan"


def test_should_escape_line_separators_in_file(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    handler = configure_logging(tmp_path, None)

    log_event(
        logging.getLogger(LOGGER_NAME),
        logging.INFO,
        "source.done",
        source="a b\u0085c d",
    )
    handler.flush()

    assert len(only_log_file(tmp_path).read_text().splitlines()) == 1


def test_should_replace_lone_surrogates(
    tmp_path: Path, root_logger: logging.Logger, capsys: pytest.CaptureFixture[str]
) -> None:
    handler = configure_logging(tmp_path, None)

    log_event(
        logging.getLogger(LOGGER_NAME), logging.INFO, "source.done", host="\ud800"
    )
    handler.flush()

    entry = json.loads(only_log_file(tmp_path).read_text())
    assert (entry["host"], capsys.readouterr().err) == ("�", "")


def test_should_write_each_line_once_when_configured_twice(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    configure_logging(tmp_path, None)
    handler = configure_logging(tmp_path, None)

    log_event(logging.getLogger(LOGGER_NAME), logging.INFO, "run.started")
    handler.flush()

    assert len(only_log_file(tmp_path).read_text().splitlines()) == 1


def test_should_create_log_file_readable_by_owner_only(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    configure_logging(tmp_path, None)

    assert stat.S_IMODE(only_log_file(tmp_path).stat().st_mode) == 0o600


def test_should_make_existing_log_file_private(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    day = datetime.now().astimezone().date().isoformat()
    log_file = tmp_path / f"{day}{LOG_SUFFIX}"
    log_file.touch()
    log_file.chmod(0o644)

    configure_logging(tmp_path, None)

    assert stat.S_IMODE(log_file.stat().st_mode) == 0o600


def test_should_create_missing_log_dir(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    log_dir = tmp_path / "state" / "logs"

    configure_logging(log_dir, None)

    assert log_dir.is_dir()


def test_should_make_existing_log_dir_private(
    tmp_path: Path, root_logger: logging.Logger
) -> None:
    log_dir = tmp_path / "logs"
    log_dir.mkdir(mode=0o755)

    configure_logging(log_dir, None)

    assert stat.S_IMODE(log_dir.stat().st_mode) == 0o700


def test_should_log_only_key_of_non_scalar_value(
    logger: logging.Logger, captured: ListHandler
) -> None:
    fields: dict[str, object] = {"status": {"nested": FAKE_KEY}, "host": Path("/x")}

    logger.info("source.done", extra={FIELDS_ATTR: fields})

    [line] = captured.lines
    entry = json.loads(line)
    assert (entry["status"], entry["host"]) == (None, None)
    assert FAKE_KEY not in line


class Unprintable:
    def __str__(self) -> str:
        raise RuntimeError(FAKE_KEY)


def test_should_write_fallback_line_when_message_cannot_render(
    logger: logging.Logger,
) -> None:
    record = logger.makeRecord(
        LOGGER_NAME, logging.ERROR, __file__, 1, Unprintable(), None, None
    )

    line = JsonFormatter().format(record)

    assert json.loads(line)["event"] == INVALID_EVENT
    assert FAKE_KEY not in line


def test_should_drop_record_silently_when_log_write_fails(
    tmp_path: Path,
    root_logger: logging.Logger,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    configure_logging(tmp_path, logging.INFO)
    for handler in own_handlers(root_logger):
        monkeypatch.setattr(handler, "stream", BrokenStream())

    logging.getLogger(LOGGER_NAME).warning("source.failed body=%s", LEAKED_TEXT)

    assert capsys.readouterr().err == ""
