from __future__ import annotations

import json
import os
import stat
from contextlib import contextmanager
from typing import TYPE_CHECKING

import keyring
import pytest
from factories import MemoryKeyring
from keyring.backends import fail

from jobranger import credentials

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

    from keyring.backend import KeyringBackend

KEY_NAME = "anthropic_api_key"
KEY_ENV_VAR = "ANTHROPIC_API_KEY"
FILE_KEY = "sk-file-1234"
KEYRING_KEY = "sk-keyring-1234"
ENV_KEY = "sk-env-1234"
OTHER_KEY_NAME = "openai_api_key"
OTHER_FILE_KEY = "sk-other-1234"
OWNER_READ_WRITE = 0o600
WORLD_READABLE = 0o644


@pytest.fixture(autouse=True)
def config_dir(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    for env_var in credentials.ENV_VARS.values():
        monkeypatch.delenv(env_var, raising=False)
    monkeypatch.setenv("JOBRANGER_CONFIG_DIR", str(tmp_path))
    return tmp_path


@pytest.fixture
def memory_keyring() -> Iterator[MemoryKeyring]:
    backend = MemoryKeyring()
    with _use_keyring(backend):
        yield backend


@pytest.fixture
def no_keyring() -> Iterator[None]:
    with _use_keyring(fail.Keyring()):
        yield


@contextmanager
def _use_keyring(backend: KeyringBackend) -> Iterator[None]:
    previous = keyring.get_keyring()
    keyring.set_keyring(backend)
    try:
        yield
    finally:
        keyring.set_keyring(previous)


@pytest.fixture
def secrets_file(config_dir: Path) -> Path:
    path = config_dir / credentials.SECRETS_FILE_NAME
    path.write_text(json.dumps({KEY_NAME: FILE_KEY}))
    os.chmod(path, OWNER_READ_WRITE)
    return path


@pytest.mark.usefixtures("no_keyring")
def test_should_read_api_key_from_file_when_keyring_is_unavailable(
    secrets_file: Path,
) -> None:
    assert credentials.get_secret(KEY_NAME) == FILE_KEY


def test_should_prefer_env_var_over_keyring(
    memory_keyring: MemoryKeyring, monkeypatch: pytest.MonkeyPatch
) -> None:
    memory_keyring.set_password(credentials.KEYRING_SERVICE, KEY_NAME, KEYRING_KEY)
    monkeypatch.setenv(KEY_ENV_VAR, ENV_KEY)

    assert credentials.get_secret(KEY_NAME) == ENV_KEY


def test_should_store_key_in_keyring_when_available(
    memory_keyring: MemoryKeyring,
) -> None:
    location = credentials.set_secret(KEY_NAME, KEYRING_KEY)

    assert location == "keyring"
    assert memory_keyring.passwords == {
        (credentials.KEYRING_SERVICE, KEY_NAME): KEYRING_KEY
    }


@pytest.mark.usefixtures("no_keyring")
def test_should_create_secrets_file_with_mode_0600(config_dir: Path) -> None:
    credentials.set_secret(KEY_NAME, FILE_KEY)

    path = config_dir / credentials.SECRETS_FILE_NAME
    assert stat.S_IMODE(path.stat().st_mode) == OWNER_READ_WRITE


@pytest.mark.usefixtures("no_keyring")
def test_should_store_each_provider_key_in_file_without_keyring() -> None:
    locations = (
        credentials.set_secret(KEY_NAME, FILE_KEY),
        credentials.set_secret(OTHER_KEY_NAME, OTHER_FILE_KEY),
    )

    assert (
        locations,
        credentials.get_secret(KEY_NAME),
        credentials.get_secret(OTHER_KEY_NAME),
    ) == (("file", "file"), FILE_KEY, OTHER_FILE_KEY)


@pytest.mark.usefixtures("no_keyring")
def test_should_replace_leftover_temp_file_when_storing_key(config_dir: Path) -> None:
    leftover = config_dir / f"{credentials.SECRETS_FILE_NAME}.tmp"
    leftover.write_text("junk")

    credentials.set_secret(KEY_NAME, FILE_KEY)

    assert credentials.get_secret(KEY_NAME) == FILE_KEY


def test_should_drop_file_copy_when_key_moves_to_keyring(
    memory_keyring: MemoryKeyring, secrets_file: Path
) -> None:
    credentials.set_secret(KEY_NAME, KEYRING_KEY)

    assert KEY_NAME not in json.loads(secrets_file.read_text())


@pytest.mark.usefixtures("no_keyring")
def test_should_tighten_existing_secrets_file_to_0600(secrets_file: Path) -> None:
    os.chmod(secrets_file, WORLD_READABLE)

    credentials.get_secret(KEY_NAME)

    assert stat.S_IMODE(secrets_file.stat().st_mode) == OWNER_READ_WRITE


def test_should_return_none_after_delete(
    memory_keyring: MemoryKeyring, secrets_file: Path
) -> None:
    memory_keyring.set_password(credentials.KEYRING_SERVICE, KEY_NAME, KEYRING_KEY)

    credentials.delete_secret(KEY_NAME)

    assert credentials.get_secret(KEY_NAME) is None


@pytest.mark.usefixtures("memory_keyring")
def test_should_delete_file_key_when_keyring_has_no_entry(secrets_file: Path) -> None:
    credentials.delete_secret(KEY_NAME)

    assert credentials.get_secret(KEY_NAME) is None


def test_should_mask_all_but_last_four_characters() -> None:
    assert credentials.mask_secret("sk-ant-0000003f9a") == "•" * 12 + "3f9a"
