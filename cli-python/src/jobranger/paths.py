from __future__ import annotations

import fcntl
import json
import logging
import os
import stat
import tempfile
import threading
from contextlib import ExitStack, contextmanager
from pathlib import Path
from typing import TYPE_CHECKING

import platformdirs

from jobranger.logging_setup import log_event

if TYPE_CHECKING:
    from collections.abc import Iterator

logger = logging.getLogger(__name__)

APP_NAME = "jobranger"
CONFIG_DIR_ENV = "JOBRANGER_CONFIG_DIR"
DATA_DIR_ENV = "JOBRANGER_DATA_DIR"
STATE_DIR_ENV = "JOBRANGER_STATE_DIR"
XDG_CONFIG_HOME_ENV = "XDG_CONFIG_HOME"
PRIVATE_FILE_MODE = 0o600
PRIVATE_DIR_MODE = 0o700
TEMP_SUFFIX = ".tmp"
DOT_CONFIG = ".config"
SECRETS_LOCK_NAME = "secrets.json.lock"
OPEN_TO_OTHERS = stat.S_IRWXG | stat.S_IRWXO
READ_FLAGS = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
LOCK_FLAGS = os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | os.O_CLOEXEC
TIGHTENED_EVENT = "paths.private_file.tightened"

_lock_holder = threading.local()


class PrivateFileError(Exception):
    def __init__(self, path: Path, reason: str) -> None:
        super().__init__(f"file cannot be used path={path} reason={reason}")


def config_dir() -> Path:
    xdg = _env_dir(XDG_CONFIG_HOME_ENV)
    return _env_dir(CONFIG_DIR_ENV) or (
        xdg / APP_NAME if xdg else Path.home() / DOT_CONFIG / APP_NAME
    )


def data_dir() -> Path:
    return _env_dir(DATA_DIR_ENV) or platformdirs.user_data_path(APP_NAME)


def state_dir() -> Path:
    return _env_dir(STATE_DIR_ENV) or platformdirs.user_state_path(APP_NAME)


def _env_dir(name: str) -> Path | None:
    value = os.environ.get(name)
    return Path(value) if value else None


def config_path() -> Path:
    return config_dir() / "config.yaml"


def profile_path() -> Path:
    return config_dir() / "profile.yaml"


def db_path() -> Path:
    return data_dir() / "jobranger.db"


def run_lock_path() -> Path:
    return state_dir() / "run.lock"


def log_dir() -> Path:
    return state_dir() / "logs"


def write_private(path: Path, text: str) -> None:
    path.parent.mkdir(mode=PRIVATE_DIR_MODE, parents=True, exist_ok=True)
    descriptor, temp = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=TEMP_SUFFIX, dir=path.parent
    )
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
            handle.write(text)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp, path)
    except BaseException:
        Path(temp).unlink(missing_ok=True)
        raise


def remove_stale_temps(path: Path) -> None:
    for stale in path.parent.glob(f".{path.name}.*{TEMP_SUFFIX}"):
        stale.unlink(missing_ok=True)


def read_json_object(path: Path) -> dict[str, str] | None:
    try:
        descriptor = os.open(path, READ_FLAGS)
    except FileNotFoundError:
        return None
    except OSError as error:
        raise PrivateFileError(path, _reason(error)) from error
    try:
        _ensure_private_file(path, descriptor)
        with open(descriptor, "rb", closefd=False) as handle:
            raw = handle.read()
    except OSError as error:
        raise PrivateFileError(path, _reason(error)) from error
    finally:
        os.close(descriptor)
    return _parse_json_object(path, raw)


def write_json_object(path: Path, data: dict[str, str]) -> None:
    with locked():
        try:
            remove_stale_temps(path)
            write_private(path, json.dumps(data))
        except OSError as error:
            raise PrivateFileError(path, _reason(error)) from error


@contextmanager
def locked(*guarded: Path) -> Iterator[None]:
    depth = getattr(_lock_holder, "depth", 0)
    if depth:
        _lock_holder.depth = depth + 1
        try:
            yield
        finally:
            _lock_holder.depth = depth
        return
    directory = config_dir()
    path = directory / SECRETS_LOCK_NAME
    with ExitStack() as stack:
        try:
            directory.mkdir(mode=PRIVATE_DIR_MODE, parents=True, exist_ok=True)
            os.chmod(directory, PRIVATE_DIR_MODE)
            stack.enter_context(private_lock(path))
            for target in guarded:
                remove_stale_temps(target)
        except OSError as error:
            raise PrivateFileError(path, _reason(error)) from error
        _lock_holder.depth = 1
        try:
            yield
        finally:
            _lock_holder.depth = 0


@contextmanager
def private_lock(path: Path) -> Iterator[None]:
    descriptor = os.open(path, LOCK_FLAGS, PRIVATE_FILE_MODE)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX)
        yield
    finally:
        os.close(descriptor)


def _ensure_private_file(path: Path, descriptor: int) -> None:
    mode = os.fstat(descriptor).st_mode
    if not stat.S_ISREG(mode):
        raise PrivateFileError(path, "not a regular file")
    if stat.S_IMODE(mode) & OPEN_TO_OTHERS:
        os.fchmod(descriptor, PRIVATE_FILE_MODE)
        log_event(logger, logging.WARNING, TIGHTENED_EVENT, file=path.name)


def _parse_json_object(path: Path, raw: bytes) -> dict[str, str]:
    try:
        loaded = json.loads(raw.decode("utf-8"))
    except ValueError:
        raise PrivateFileError(path, "not UTF-8 JSON") from None
    if isinstance(loaded, dict):
        entries = {
            key: value for key, value in loaded.items() if isinstance(value, str)
        }
        if len(entries) == len(loaded):
            return entries
    raise PrivateFileError(path, "not a JSON object of strings")


def _reason(error: OSError) -> str:
    return error.strerror or type(error).__name__
