from __future__ import annotations

import logging
import os
from contextlib import contextmanager
from typing import TYPE_CHECKING, Literal

import keyring
from keyring.errors import KeyringError, NoKeyringError, PasswordDeleteError

from jobranger import paths
from jobranger.logging_setup import log_event

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

logger = logging.getLogger(__name__)

KEYRING_SERVICE = "jobranger"
SECRETS_FILE_NAME = "secrets.json"
ENV_VARS = {
    "anthropic_api_key": "ANTHROPIC_API_KEY",
    "openai_api_key": "OPENAI_API_KEY",
    "openrouter_api_key": "OPENROUTER_API_KEY",
}
MASK_CHAR = "•"
MASK_LENGTH = 12
VISIBLE_TAIL = 4
NO_KEYRING_ENTRY_EVENT = "credentials.keyring.no_entry"

type SecretStore = Literal["keyring", "file"]


class CredentialsUnavailableError(Exception):
    pass


class CredentialsFileError(Exception):
    pass


class KeyringMissingError(CredentialsUnavailableError):
    pass


def get_secret(name: str) -> str | None:
    env_var = ENV_VARS.get(name)
    env_value = os.environ.get(env_var) if env_var else None
    if env_value:
        return env_value
    found = find_secret(name)
    return found[0] if found else None


def find_secret(name: str) -> tuple[str, SecretStore] | None:
    for store in ("keyring", "file"):
        try:
            value = read_secret_from(store, name)
        except KeyringMissingError:
            continue
        if value is not None:
            return value, store
    return None


def read_secret_from(store: SecretStore, name: str) -> str | None:
    if store == "file":
        return _read_file(secrets_path()).get(name)
    try:
        return keyring.get_password(KEYRING_SERVICE, name)
    except NoKeyringError as error:
        raise KeyringMissingError(
            f"keyring backend is missing store=keyring name={name}"
        ) from error
    except KeyringError as error:
        raise _unavailable(name, error) from error


def set_secret(name: str, value: str) -> SecretStore:
    with _secrets_file_errors(), paths.locked(secrets_path()):
        path = secrets_path()
        stored = _read_file(path)
        try:
            keyring.set_password(KEYRING_SERVICE, name, value)
        except NoKeyringError:
            paths.write_json_object(path, {**stored, name: value})
            return "file"
        except KeyringError as error:
            raise _unavailable(name, error) from error
        _remove_from_file(path, stored, name)
        return "keyring"


def delete_secret(name: str) -> None:
    with _secrets_file_errors(), paths.locked(secrets_path()):
        path = secrets_path()
        stored = _read_file(path)
        try:
            keyring.delete_password(KEYRING_SERVICE, name)
        except (PasswordDeleteError, NoKeyringError):
            log_event(logger, logging.DEBUG, NO_KEYRING_ENTRY_EVENT)
        except KeyringError as error:
            raise _unavailable(name, error) from error
        _remove_from_file(path, stored, name)


def mask_secret(value: str) -> str:
    return MASK_CHAR * MASK_LENGTH + value[-VISIBLE_TAIL:]


@contextmanager
def _secrets_file_errors() -> Iterator[None]:
    try:
        yield
    except paths.PrivateFileError as error:
        raise CredentialsFileError(str(error)) from error


def _remove_from_file(path: Path, stored: dict[str, str], name: str) -> None:
    if name in stored:
        paths.write_json_object(
            path, {key: value for key, value in stored.items() if key != name}
        )


def secrets_path() -> Path:
    return paths.config_dir() / SECRETS_FILE_NAME


def _read_file(path: Path) -> dict[str, str]:
    with _secrets_file_errors():
        return paths.read_json_object(path) or {}


def _unavailable(name: str, error: KeyringError) -> CredentialsUnavailableError:
    return CredentialsUnavailableError(
        f"keyring is present but failed name={name} error={type(error).__name__}"
    )
