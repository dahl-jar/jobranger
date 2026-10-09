from __future__ import annotations

import json
import logging
import os
import stat
from typing import TYPE_CHECKING, Literal

import keyring
from keyring.errors import KeyringError, NoKeyringError, PasswordDeleteError

from jobranger import paths

if TYPE_CHECKING:
    from pathlib import Path

logger = logging.getLogger(__name__)

KEYRING_SERVICE = "jobranger"
SECRETS_FILE_NAME = "secrets.json"
ENV_VARS = {
    "anthropic_api_key": "ANTHROPIC_API_KEY",
    "openai_api_key": "OPENAI_API_KEY",
    "openrouter_api_key": "OPENROUTER_API_KEY",
}
OPEN_TO_OTHERS = stat.S_IRWXG | stat.S_IRWXO
MASK_CHAR = "•"
MASK_LENGTH = 12
VISIBLE_TAIL = 4

type SecretLocation = Literal["env", "keyring", "file"]


def get_secret(name: str) -> str | None:
    env_var = ENV_VARS.get(name)
    env_value = os.environ.get(env_var) if env_var else None
    if env_value:
        return env_value
    try:
        stored = keyring.get_password(KEYRING_SERVICE, name)
    except KeyringError:
        stored = None
    if stored is not None:
        return stored
    return _read_file(_secrets_path()).get(name)


def set_secret(name: str, value: str) -> SecretLocation:
    try:
        keyring.set_password(KEYRING_SERVICE, name, value)
    except KeyringError:
        path = _secrets_path()
        _write_file(path, {**_read_file(path), name: value})
        return "file"
    _remove_from_file(name)
    return "keyring"


def delete_secret(name: str) -> None:
    try:
        keyring.delete_password(KEYRING_SERVICE, name)
    except (PasswordDeleteError, NoKeyringError):
        logger.debug("keyring holds no entry to delete name=%s", name)
    _remove_from_file(name)


def _remove_from_file(name: str) -> None:
    path = _secrets_path()
    stored = _read_file(path)
    if name in stored:
        del stored[name]
        _write_file(path, stored)


def mask_secret(value: str) -> str:
    return MASK_CHAR * MASK_LENGTH + value[-VISIBLE_TAIL:]


def _secrets_path() -> Path:
    return paths.config_dir() / SECRETS_FILE_NAME


def _read_file(path: Path) -> dict[str, str]:
    try:
        mode = stat.S_IMODE(path.stat().st_mode)
    except FileNotFoundError:
        return {}
    if mode & OPEN_TO_OTHERS:
        os.chmod(path, paths.PRIVATE_FILE_MODE)
        logger.warning("secrets file was readable by others, set to 0600 path=%s", path)
    loaded = json.loads(path.read_text(encoding="utf-8"))
    return {str(key): str(value) for key, value in loaded.items()}


def _write_file(path: Path, data: dict[str, str]) -> None:
    paths.write_private(path, json.dumps(data))
