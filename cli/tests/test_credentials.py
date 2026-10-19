from __future__ import annotations

import errno
import json
import logging
import os
import stat
import threading
from typing import TYPE_CHECKING

import keyring
import pytest
from keyring.backend import KeyringBackend
from keyring.errors import KeyringLocked

from jobranger import credentials, paths
from jobranger.logging_setup import JsonFormatter

if TYPE_CHECKING:
    from collections.abc import Callable
    from pathlib import Path

    from factories import MemoryKeyring

KEY_NAME = "anthropic_api_key"
KEY_ENV_VAR = "ANTHROPIC_API_KEY"
FILE_KEY = "sk-file-1234"
KEYRING_KEY = "sk-keyring-1234"
ENV_KEY = "sk-env-1234"
OTHER_KEY_NAME = "openai_api_key"
OTHER_FILE_KEY = "sk-other-1234"
OWNER_READ_WRITE = 0o600
WORLD_READABLE = 0o644
NO_ACCESS = 0o000
OWNER_ALL = 0o700
OPEN_DIR = 0o755
NON_ASCII_KEY = "sk-æøå-1234"
BLOCKED_OPEN_TIMEOUT_SECONDS = 2


class LockedKeyring(KeyringBackend):
    priority = 0

    def get_password(self, service: str, username: str) -> str | None:
        raise KeyringLocked(username)

    def set_password(self, service: str, username: str, password: str) -> None:
        raise KeyringLocked(username)

    def delete_password(self, service: str, username: str) -> None:
        raise KeyringLocked(username)


@pytest.fixture(autouse=True)
def clear_key_env(config_dir: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    for env_var in credentials.ENV_VARS.values():
        monkeypatch.delenv(env_var, raising=False)


@pytest.fixture
def locked_keyring(memory_keyring: MemoryKeyring) -> None:
    keyring.set_keyring(LockedKeyring())


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


def test_should_prefer_keyring_over_file_when_both_hold_secret(
    memory_keyring: MemoryKeyring, secrets_file: Path
) -> None:
    memory_keyring.set_password(credentials.KEYRING_SERVICE, KEY_NAME, KEYRING_KEY)

    assert credentials.find_secret(KEY_NAME) == (KEYRING_KEY, "keyring")


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


@pytest.mark.usefixtures("no_keyring")
def test_should_name_only_the_file_when_warning_about_open_secrets_file(
    secrets_file: Path, caplog: pytest.LogCaptureFixture
) -> None:
    os.chmod(secrets_file, WORLD_READABLE)

    credentials.get_secret(KEY_NAME)

    lines = [JsonFormatter().format(record) for record in caplog.records]
    entries = [json.loads(line) for line in lines]
    leaked = [
        line for line in lines if str(secrets_file.parent) in line or FILE_KEY in line
    ]
    assert ([(entry["event"], entry.get("file")) for entry in entries], leaked) == (
        [("paths.private_file.tightened", credentials.SECRETS_FILE_NAME)],
        [],
    )


@pytest.mark.usefixtures("no_keyring")
def test_should_read_non_ascii_secret_from_utf_8_file(secrets_file: Path) -> None:
    secrets_file.write_text(
        json.dumps({KEY_NAME: NON_ASCII_KEY}, ensure_ascii=False), encoding="utf-8"
    )

    assert credentials.get_secret(KEY_NAME) == NON_ASCII_KEY


@pytest.mark.usefixtures("no_keyring")
def test_should_not_warn_when_secrets_file_is_private(
    secrets_file: Path, caplog: pytest.LogCaptureFixture
) -> None:
    credentials.get_secret(KEY_NAME)

    assert caplog.records == []


@pytest.mark.usefixtures("no_keyring")
def test_should_raise_file_error_when_secrets_file_cannot_be_written(
    config_dir: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fail_write(path: Path, text: str) -> None:
        raise OSError(errno.ENOSPC, os.strerror(errno.ENOSPC))

    monkeypatch.setattr(paths, "write_private", fail_write)

    with pytest.raises(credentials.CredentialsFileError) as raised:
        credentials.set_secret(KEY_NAME, FILE_KEY)

    message = str(raised.value)
    path = config_dir / credentials.SECRETS_FILE_NAME
    assert (str(path) in message, os.strerror(errno.ENOSPC) in message) == (True, True)


@pytest.mark.usefixtures("no_keyring")
def test_should_name_error_type_when_write_error_has_no_text(
    config_dir: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fail_write(path: Path, text: str) -> None:
        raise OSError

    monkeypatch.setattr(paths, "write_private", fail_write)

    with pytest.raises(credentials.CredentialsFileError) as raised:
        credentials.set_secret(KEY_NAME, FILE_KEY)

    path = config_dir / credentials.SECRETS_FILE_NAME
    assert str(raised.value) == f"file cannot be used path={path} reason=OSError"


@pytest.mark.usefixtures("no_keyring")
def test_should_raise_file_error_when_secrets_file_cannot_be_made_private(
    secrets_file: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    os.chmod(secrets_file, WORLD_READABLE)

    def refuse_chmod(descriptor: int, mode: int) -> None:
        raise PermissionError(errno.EPERM, os.strerror(errno.EPERM))

    monkeypatch.setattr(paths.os, "fchmod", refuse_chmod)

    with pytest.raises(credentials.CredentialsFileError) as raised:
        credentials.get_secret(KEY_NAME)

    assert str(raised.value) == (
        f"file cannot be used path={secrets_file} reason={os.strerror(errno.EPERM)}"
    )


def test_should_log_missing_keyring_entry_without_secret_on_delete(
    memory_keyring: MemoryKeyring,
    secrets_file: Path,
    caplog: pytest.LogCaptureFixture,
) -> None:
    caplog.set_level(logging.DEBUG)

    credentials.delete_secret(KEY_NAME)

    events = [record.getMessage() for record in caplog.records]
    assert (events, FILE_KEY in caplog.text) == (
        ["credentials.keyring.no_entry"],
        False,
    )


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


@pytest.mark.usefixtures("locked_keyring")
@pytest.mark.parametrize(
    "operation",
    [
        lambda: credentials.get_secret(KEY_NAME),
        lambda: credentials.set_secret(KEY_NAME, FILE_KEY),
        lambda: credentials.delete_secret(KEY_NAME),
    ],
    ids=["get", "set", "delete"],
)
def test_should_raise_unavailable_without_file_fallback_when_keyring_is_locked(
    config_dir: Path, operation: Callable[[], object]
) -> None:
    with pytest.raises(
        credentials.CredentialsUnavailableError,
        match=f"name={KEY_NAME} error=KeyringLocked",
    ):
        operation()

    assert not (config_dir / credentials.SECRETS_FILE_NAME).exists()


def _write_text(content: str) -> Callable[[Path], None]:
    return lambda path: path.write_text(content)


def _write_secret_then_chmod(path: Path) -> None:
    path.write_text(json.dumps({KEY_NAME: FILE_KEY}))
    os.chmod(path, NO_ACCESS)


def _symlink_to_other_json(path: Path) -> None:
    other = path.parent / "other.json"
    other.write_text(json.dumps({KEY_NAME: FILE_KEY}))
    os.symlink(other, path)


@pytest.mark.usefixtures("no_keyring")
@pytest.mark.parametrize(
    ("damage", "reason"),
    [
        (_write_text('{"anthropic_api_key": "sk-file-1234"'), "not UTF-8 JSON"),
        (_write_text("[]"), "not a JSON object of strings"),
        (_write_text('{"anthropic_api_key": 1234}'), "not a JSON object of strings"),
        (
            lambda path: path.write_bytes(b"\xff\xfe sk-file-1234 \x00"),
            "not UTF-8 JSON",
        ),
        (_write_secret_then_chmod, os.strerror(errno.EACCES)),
        (lambda path: path.mkdir(), "not a regular file"),
        (_symlink_to_other_json, os.strerror(errno.ELOOP)),
    ],
    ids=[
        "truncated",
        "list",
        "number value",
        "binary",
        "no permission",
        "directory",
        "symlink to json",
    ],
)
def test_should_raise_file_error_naming_path_for_damaged_secrets_file(
    config_dir: Path, damage: Callable[[Path], None], reason: str
) -> None:
    path = config_dir / credentials.SECRETS_FILE_NAME
    damage(path)

    with pytest.raises(credentials.CredentialsFileError) as raised:
        credentials.get_secret(KEY_NAME)

    assert str(raised.value) == f"file cannot be used path={path} reason={reason}"


@pytest.mark.usefixtures("no_keyring")
def test_should_leave_symlink_target_untouched_when_storing_key(
    config_dir: Path,
) -> None:
    target = config_dir / "other.json"
    target.write_text("{}")
    os.chmod(target, WORLD_READABLE)
    os.symlink(target, config_dir / credentials.SECRETS_FILE_NAME)

    with pytest.raises(credentials.CredentialsFileError):
        credentials.set_secret(KEY_NAME, FILE_KEY)

    assert (target.read_text(), stat.S_IMODE(target.stat().st_mode)) == (
        "{}",
        WORLD_READABLE,
    )


@pytest.mark.usefixtures("no_keyring")
def test_should_refuse_symlinked_lock_file(config_dir: Path) -> None:
    target = config_dir / "other.lock"
    target.write_text("")
    os.symlink(target, config_dir / paths.SECRETS_LOCK_NAME)

    with pytest.raises(credentials.CredentialsFileError):
        credentials.set_secret(KEY_NAME, FILE_KEY)


def test_should_write_nothing_to_keyring_when_secrets_file_is_damaged(
    memory_keyring: MemoryKeyring, config_dir: Path
) -> None:
    (config_dir / credentials.SECRETS_FILE_NAME).write_text("{")

    with pytest.raises(credentials.CredentialsFileError):
        credentials.set_secret(KEY_NAME, KEYRING_KEY)

    assert memory_keyring.passwords == {}


def test_should_keep_keyring_entry_when_delete_meets_damaged_secrets_file(
    memory_keyring: MemoryKeyring, config_dir: Path
) -> None:
    memory_keyring.set_password(credentials.KEYRING_SERVICE, KEY_NAME, KEYRING_KEY)
    (config_dir / credentials.SECRETS_FILE_NAME).write_text("{")

    with pytest.raises(credentials.CredentialsFileError):
        credentials.delete_secret(KEY_NAME)

    assert memory_keyring.passwords == {
        (credentials.KEYRING_SERVICE, KEY_NAME): KEYRING_KEY
    }


def _get_secret_without_blocking(path: Path) -> str | None:
    raised: list[str | None] = []

    def read() -> None:
        try:
            credentials.get_secret(KEY_NAME)
        except credentials.CredentialsFileError as error:
            raised.append(str(error))
        else:
            raised.append(None)

    worker = threading.Thread(target=read, daemon=True)
    worker.start()
    worker.join(BLOCKED_OPEN_TIMEOUT_SECONDS)
    if worker.is_alive():
        os.close(os.open(path, os.O_WRONLY | os.O_NONBLOCK))
        worker.join()
        pytest.fail(f"get_secret blocked on {path.name}")
    return raised[0]


@pytest.mark.usefixtures("no_keyring")
def test_should_raise_file_error_when_secrets_path_is_a_fifo(config_dir: Path) -> None:
    path = config_dir / credentials.SECRETS_FILE_NAME
    os.mkfifo(path)

    assert _get_secret_without_blocking(path) == (
        f"file cannot be used path={path} reason=not a regular file"
    )


@pytest.mark.usefixtures("no_keyring")
def test_should_force_config_dir_to_0700_when_storing_secret(config_dir: Path) -> None:
    os.chmod(config_dir, OPEN_DIR)

    credentials.set_secret(KEY_NAME, FILE_KEY)

    assert stat.S_IMODE(config_dir.stat().st_mode) == OWNER_ALL


@pytest.mark.usefixtures("no_keyring")
def test_should_remove_stale_temp_files_of_secrets_file_when_storing(
    config_dir: Path,
) -> None:
    (config_dir / f".{credentials.SECRETS_FILE_NAME}.abc123.tmp").write_text(FILE_KEY)
    (config_dir / "notes.tmp").write_text("keep")

    credentials.set_secret(KEY_NAME, FILE_KEY)

    assert sorted(entry.name for entry in config_dir.iterdir()) == [
        "notes.tmp",
        credentials.SECRETS_FILE_NAME,
        paths.SECRETS_LOCK_NAME,
    ]
