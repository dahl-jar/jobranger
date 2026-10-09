from __future__ import annotations

import os
from pathlib import Path

import platformdirs

APP_NAME = "jobranger"
CONFIG_DIR_ENV = "JOBRANGER_CONFIG_DIR"
DATA_DIR_ENV = "JOBRANGER_DATA_DIR"
STATE_DIR_ENV = "JOBRANGER_STATE_DIR"
XDG_CONFIG_HOME_ENV = "XDG_CONFIG_HOME"
PRIVATE_FILE_MODE = 0o600
PRIVATE_DIR_MODE = 0o700
TEMP_SUFFIX = ".tmp"
DOT_CONFIG = ".config"


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
    temp = path.with_suffix(path.suffix + TEMP_SUFFIX)
    descriptor = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, PRIVATE_FILE_MODE)
    os.fchmod(descriptor, PRIVATE_FILE_MODE)
    with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
        handle.write(text)
    os.replace(temp, path)
