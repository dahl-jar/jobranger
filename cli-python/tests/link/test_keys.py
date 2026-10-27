from __future__ import annotations

import base64
import errno
import hashlib
import json
import logging
import multiprocessing
import os
import re
import stat
import threading
from typing import TYPE_CHECKING

import keyring
import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey
from keyring.backends import fail

from jobranger import credentials, paths
from jobranger.link import keys

if TYPE_CHECKING:
    from collections.abc import Callable
    from multiprocessing.queues import Queue
    from multiprocessing.synchronize import Barrier
    from pathlib import Path

    from factories import MemoryKeyring

VECTOR_SIGNING_SEED = hashlib.sha256(b"jobranger vector cli ed25519").digest()
VECTOR_SIGNING_PUBLIC = "ZSHR3pRvRcigeIxxQgTcdHAJLSBvGb0K1qeC35Vkxck"
VECTOR_CHANNEL = "QzATLaCZ1ciBwANAgYReA3Np4WJ48r7ZPzlZsVwdxSQ"
VECTOR_NONCE = "AAECAwQFBgcICQoLDA0ODw"
VECTOR_PAIR_START_SIGNATURE = (
    "VX8cfTvedl8Po_4YNqhOyEzpXHVLjQ7P48I1HjAMhmZG"
    "Lk2mvhaLY6EellYLwfGJ8pxqHU6AC3T7tSDDqbg7DA"
)
OWNER_READ_WRITE = 0o600
SHARED_DIR = 0o750
RACE_PROCESSES = 4
RACE_TRIALS = 20
RACE_TIMEOUT_SECONDS = 30
CONCURRENT_WAIT_SECONDS = 0.5
UNPAIR_HINT = "run `jobranger unpair` to make new ones"
METADATA_RULE = (
    "link metadata needs store keyring or file, a channel and an agreement key"
)
KEYRING_UNCHECKED = "keyring backend is missing so stored link keys cannot be checked"


def _decode(text: str) -> bytes:
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def _encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def _vector_keys() -> keys.LinkKeys:
    return keys.LinkKeys(
        signing=Ed25519PrivateKey.from_private_bytes(VECTOR_SIGNING_SEED),
        agreement=X25519PrivateKey.generate(),
    )


def _public_bytes(link_keys: keys.LinkKeys) -> tuple[bytes, bytes]:
    return (
        link_keys.signing.public_key().public_bytes_raw(),
        link_keys.agreement.public_key().public_bytes_raw(),
    )


def _store_pair(memory_keyring: MemoryKeyring, pair: object) -> None:
    memory_keyring.set_password(
        credentials.KEYRING_SERVICE, keys.LINK_KEYS_NAME, json.dumps(pair)
    )


def _channel(link_keys: keys.LinkKeys) -> str:
    return keys.channel_id(link_keys.signing.public_key().public_bytes_raw())


def _agreement(link_keys: keys.LinkKeys) -> str:
    return _encode(link_keys.agreement.public_key().public_bytes_raw())


def _refuse_lock(path: Path) -> None:
    raise PermissionError(errno.EPERM, os.strerror(errno.EPERM))


def _stored_pair(memory_keyring: MemoryKeyring) -> dict[str, str]:
    return json.loads(
        memory_keyring.passwords[(credentials.KEYRING_SERVICE, keys.LINK_KEYS_NAME)]
    )


def _start_once(thread: threading.Thread) -> None:
    if thread.ident is None:
        thread.start()
        thread.join(CONCURRENT_WAIT_SECONDS)


def test_should_derive_channel_id_from_ed25519_public_key() -> None:
    assert keys.channel_id(_decode(VECTOR_SIGNING_PUBLIC)) == VECTOR_CHANNEL


def test_should_sign_ownership_proof_over_purpose_and_nonce() -> None:
    proof = keys.ownership_proof(_vector_keys(), keys.PAIR_START_PURPOSE, VECTOR_NONCE)

    assert _encode(proof) == VECTOR_PAIR_START_SIGNATURE


GOOD_KEY = _encode(VECTOR_SIGNING_SEED)


def test_should_reuse_keys_stored_as_base64url(
    memory_keyring: MemoryKeyring,
) -> None:
    _store_pair(memory_keyring, {"signing": GOOD_KEY, "agreement": _encode(bytes(32))})

    loaded = keys.load_or_create_keys()

    assert _channel(loaded) == VECTOR_CHANNEL


def test_should_change_channel_id_after_rotate() -> None:
    before = keys.load_or_create_keys()

    rotated = keys.rotate_keys()

    assert _channel(rotated) != _channel(before)


def test_should_load_rotated_keys_after_rotate() -> None:
    keys.load_or_create_keys()
    rotated = keys.rotate_keys()

    loaded = keys.load_or_create_keys()

    assert _public_bytes(loaded) == _public_bytes(rotated)


@pytest.mark.usefixtures("no_keyring")
def test_should_keep_keys_in_0600_file_without_keyring(config_dir: Path) -> None:
    created = keys.load_or_create_keys()

    loaded = keys.load_or_create_keys()

    secrets_file = config_dir / credentials.SECRETS_FILE_NAME
    assert _public_bytes(loaded) == _public_bytes(created)
    assert stat.S_IMODE(secrets_file.stat().st_mode) == OWNER_READ_WRITE


@pytest.mark.parametrize(
    "pair",
    [
        GOOD_KEY,
        {"agreement": GOOD_KEY},
        {"signing": GOOD_KEY},
        {"signing": GOOD_KEY, "agreement": _encode(bytes(31))},
        {"signing": GOOD_KEY, "agreement": f"{GOOD_KEY}!!"},
        {"signing": GOOD_KEY, "agreement": 1234},
    ],
    ids=[
        "not an object",
        "no signing key",
        "no agreement key",
        "short key",
        "junk after key",
        "number",
    ],
)
def test_should_raise_link_key_error_without_key_material_for_invalid_stored_pair(
    memory_keyring: MemoryKeyring, pair: object
) -> None:
    _store_pair(memory_keyring, pair)

    with pytest.raises(keys.LinkKeyError, match=keys.LINK_KEYS_NAME) as raised:
        keys.load_or_create_keys()

    assert GOOD_KEY not in str(raised.value)


def test_should_raise_link_key_error_for_stored_value_that_is_not_json(
    memory_keyring: MemoryKeyring,
) -> None:
    memory_keyring.set_password(
        credentials.KEYRING_SERVICE, keys.LINK_KEYS_NAME, f"{{{GOOD_KEY}"
    )

    with pytest.raises(keys.LinkKeyError, match=keys.LINK_KEYS_NAME) as raised:
        keys.load_or_create_keys()

    assert (GOOD_KEY in str(raised.value), raised.value.__suppress_context__) == (
        False,
        True,
    )


@pytest.mark.usefixtures("no_keyring")
@pytest.mark.parametrize(
    "operation", [keys.load_or_create_keys, keys.rotate_keys], ids=["load", "rotate"]
)
def test_should_raise_link_key_error_when_secrets_file_is_damaged(
    config_dir: Path, operation: Callable[[], keys.LinkKeys]
) -> None:
    (config_dir / credentials.SECRETS_FILE_NAME).write_text("{")

    with pytest.raises(keys.LinkKeyError, match=credentials.SECRETS_FILE_NAME):
        operation()


def _stored_keys(memory_keyring: MemoryKeyring) -> list[str]:
    stored = memory_keyring.passwords[
        (credentials.KEYRING_SERVICE, keys.LINK_KEYS_NAME)
    ]
    return list(json.loads(stored).values())


def test_should_log_key_creation_without_key_material(
    memory_keyring: MemoryKeyring, caplog: pytest.LogCaptureFixture
) -> None:
    caplog.set_level(logging.DEBUG)

    keys.load_or_create_keys()

    private_keys = _stored_keys(memory_keyring)
    events = [record.getMessage() for record in caplog.records]
    assert (events, any(key in caplog.text for key in private_keys)) == (
        ["link.keys.created"],
        False,
    )


def test_should_not_log_creation_again_when_keys_are_reused(
    caplog: pytest.LogCaptureFixture,
) -> None:
    keys.load_or_create_keys()
    caplog.set_level(logging.DEBUG)

    keys.load_or_create_keys()

    assert caplog.records == []


def test_should_log_rotation_without_key_material(
    memory_keyring: MemoryKeyring, caplog: pytest.LogCaptureFixture
) -> None:
    keys.load_or_create_keys()
    caplog.set_level(logging.DEBUG)

    keys.rotate_keys()

    private_keys = _stored_keys(memory_keyring)
    events = [record.getMessage() for record in caplog.records]
    assert (events, any(key in caplog.text for key in private_keys)) == (
        ["link.keys.rotated"],
        False,
    )


def _metadata(config_dir: Path) -> dict[str, str]:
    return json.loads((config_dir / keys.METADATA_FILE_NAME).read_text())


@pytest.mark.parametrize(
    ("backend", "store"), [("memory_keyring", "keyring"), ("no_keyring", "file")]
)
def test_should_record_store_and_channel_when_keys_are_created(
    config_dir: Path, backend: str, store: str, request: pytest.FixtureRequest
) -> None:
    request.getfixturevalue(backend)

    created = keys.load_or_create_keys()

    metadata_file = config_dir / keys.METADATA_FILE_NAME
    assert _metadata(config_dir) == {
        "store": store,
        "channel": _channel(created),
        "agreement": _agreement(created),
    }
    assert stat.S_IMODE(metadata_file.stat().st_mode) == OWNER_READ_WRITE


@pytest.mark.parametrize(
    ("backend", "store"), [("memory_keyring", "keyring"), ("no_keyring", "file")]
)
def test_should_record_metadata_for_stored_pair_without_metadata(
    memory_keyring: MemoryKeyring,
    config_dir: Path,
    backend: str,
    store: str,
    request: pytest.FixtureRequest,
) -> None:
    request.getfixturevalue(backend)
    pair = json.dumps({"signing": GOOD_KEY, "agreement": GOOD_KEY})
    credentials.set_secret(keys.LINK_KEYS_NAME, pair)
    keyring.set_keyring(memory_keyring)

    loaded = keys.load_or_create_keys()

    assert _metadata(config_dir) == {
        "store": store,
        "channel": VECTOR_CHANNEL,
        "agreement": _agreement(loaded),
    }


def test_should_name_unpair_for_metadata_without_agreement_key(
    config_dir: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    created = keys.load_or_create_keys()
    older = {"store": "keyring", "channel": _channel(created)}
    (config_dir / keys.METADATA_FILE_NAME).write_text(json.dumps(older))
    monkeypatch.setattr(paths, "private_lock", _refuse_lock)

    with pytest.raises(keys.LinkKeyError, match=re.escape(UNPAIR_HINT)):
        keys.load_or_create_keys()

    assert _metadata(config_dir) == older


@pytest.mark.usefixtures("no_keyring")
def test_should_refuse_adoption_when_keyring_backend_is_missing(
    config_dir: Path,
) -> None:
    pair = json.dumps({"signing": GOOD_KEY, "agreement": GOOD_KEY})
    credentials.set_secret(keys.LINK_KEYS_NAME, pair)

    with pytest.raises(credentials.KeyringMissingError) as raised:
        keys.load_or_create_keys()

    assert (
        str(raised.value).startswith(KEYRING_UNCHECKED),
        (config_dir / keys.METADATA_FILE_NAME).exists(),
    ) == (True, False)


def _write_stale_temps(config_dir: Path) -> list[str]:
    pair = json.dumps({"signing": GOOD_KEY, "agreement": GOOD_KEY})
    names = [
        f".{credentials.SECRETS_FILE_NAME}.crashed.tmp",
        f".{keys.METADATA_FILE_NAME}.crashed.tmp",
    ]
    for name in names:
        (config_dir / name).write_text(pair)
    return names


def _load_after_losing_metadata(config_dir: Path) -> None:
    (config_dir / keys.METADATA_FILE_NAME).unlink()
    keys.load_or_create_keys()


def _load_after_swapping_pair(config_dir: Path) -> None:
    pair = json.dumps({"signing": GOOD_KEY, "agreement": GOOD_KEY})
    credentials.set_secret(keys.LINK_KEYS_NAME, pair)
    with pytest.raises(keys.LinkKeyError):
        keys.load_or_create_keys()


@pytest.mark.parametrize(
    "locked_operation",
    [
        _load_after_losing_metadata,
        _load_after_swapping_pair,
        lambda config_dir: keys.rotate_keys(),
    ],
    ids=["load", "failed load", "rotate"],
)
def test_should_remove_stale_temps_when_taking_the_lock(
    config_dir: Path, locked_operation: Callable[[Path], object]
) -> None:
    keys.load_or_create_keys()
    stale = _write_stale_temps(config_dir)

    locked_operation(config_dir)

    assert [name for name in stale if (config_dir / name).exists()] == []


def test_should_raise_link_key_error_when_agreement_key_differs_from_recorded(
    memory_keyring: MemoryKeyring,
) -> None:
    keys.load_or_create_keys()
    _store_pair(memory_keyring, {**_stored_pair(memory_keyring), "agreement": GOOD_KEY})

    with pytest.raises(keys.LinkKeyError, match=re.escape(UNPAIR_HINT)):
        keys.load_or_create_keys()


def test_should_raise_stored_key_error_when_lock_fails_on_retry(
    memory_keyring: MemoryKeyring, monkeypatch: pytest.MonkeyPatch
) -> None:
    keys.load_or_create_keys()
    short_key = _encode(bytes(31))
    _store_pair(
        memory_keyring, {**_stored_pair(memory_keyring), "agreement": short_key}
    )
    monkeypatch.setattr(paths, "private_lock", _refuse_lock)

    with pytest.raises(keys.LinkKeyError, match="field=agreement") as raised:
        keys.load_or_create_keys()

    assert isinstance(raised.value.__cause__, paths.PrivateFileError)


def test_should_raise_link_key_error_when_both_stores_hold_different_pairs(
    memory_keyring: MemoryKeyring, config_dir: Path
) -> None:
    keys.load_or_create_keys()
    keyring.set_keyring(fail.Keyring())
    keys.rotate_keys()
    keyring.set_keyring(memory_keyring)
    (config_dir / keys.METADATA_FILE_NAME).unlink()

    with pytest.raises(keys.LinkKeyError, match=re.escape(UNPAIR_HINT)):
        keys.load_or_create_keys()


def test_should_load_rotated_keys_when_rotate_runs_during_adoption(
    config_dir: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    keys.load_or_create_keys()
    (config_dir / keys.METADATA_FILE_NAME).unlink()
    rotated: list[keys.LinkKeys] = []
    rotation = threading.Thread(target=lambda: rotated.append(keys.rotate_keys()))
    read_secret_from = credentials.read_secret_from

    def read_then_rotate(store: credentials.SecretStore, name: str) -> str | None:
        found = read_secret_from(store, name)
        if store == "keyring":
            _start_once(rotation)
        return found

    with monkeypatch.context() as patch:
        patch.setattr(credentials, "read_secret_from", read_then_rotate)
        keys.load_or_create_keys()
    rotation.join()

    loaded = keys.load_or_create_keys()

    assert _public_bytes(loaded) == _public_bytes(rotated[0])


def test_should_load_previous_or_rotated_keys_while_rotate_is_writing(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    previous = keys.load_or_create_keys()
    outcomes: list[str] = []

    def load() -> None:
        try:
            outcomes.append(str(_public_bytes(keys.load_or_create_keys())))
        except keys.LinkKeyError as error:
            outcomes.append(type(error).__name__)

    loader = threading.Thread(target=load)
    write_metadata = keys._write_metadata

    def load_then_write(
        store: credentials.SecretStore, link_keys: keys.LinkKeys
    ) -> None:
        _start_once(loader)
        write_metadata(store, link_keys)

    monkeypatch.setattr(keys, "_write_metadata", load_then_write)

    rotated = keys.rotate_keys()

    loader.join()
    assert outcomes[0] in {str(_public_bytes(previous)), str(_public_bytes(rotated))}


def _kill_rotate_before_metadata(monkeypatch: pytest.MonkeyPatch) -> None:
    keys.load_or_create_keys()

    def killed(store: credentials.SecretStore, link_keys: keys.LinkKeys) -> None:
        raise KeyboardInterrupt

    with monkeypatch.context() as patch:
        patch.setattr(keys, "_write_metadata", killed)
        with pytest.raises(KeyboardInterrupt):
            keys.rotate_keys()


def test_should_name_unpair_when_rotate_died_before_metadata(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _kill_rotate_before_metadata(monkeypatch)

    with pytest.raises(keys.LinkKeyError, match=re.escape(UNPAIR_HINT)):
        keys.load_or_create_keys()


def test_should_recover_with_rotate_after_rotate_died_before_metadata(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _kill_rotate_before_metadata(monkeypatch)
    rotated = keys.rotate_keys()

    loaded = keys.load_or_create_keys()

    assert _public_bytes(loaded) == _public_bytes(rotated)


def test_should_refuse_new_keys_while_recorded_keyring_backend_is_missing(
    memory_keyring: MemoryKeyring, config_dir: Path
) -> None:
    created = keys.load_or_create_keys()
    keyring.set_keyring(fail.Keyring())

    with pytest.raises(credentials.KeyringMissingError, match="store=keyring"):
        keys.load_or_create_keys()

    keyring.set_keyring(memory_keyring)
    assert (
        _public_bytes(keys.load_or_create_keys()),
        (config_dir / credentials.SECRETS_FILE_NAME).exists(),
    ) == (_public_bytes(created), False)


@pytest.mark.usefixtures("no_keyring")
def test_should_raise_link_key_error_when_recorded_file_store_lost_the_pair(
    config_dir: Path,
) -> None:
    keys.load_or_create_keys()
    (config_dir / credentials.SECRETS_FILE_NAME).unlink()

    with pytest.raises(keys.LinkKeyError, match="store=file"):
        keys.load_or_create_keys()

    assert not (config_dir / credentials.SECRETS_FILE_NAME).exists()


def test_should_raise_link_key_error_when_pair_differs_from_recorded_channel(
    memory_keyring: MemoryKeyring,
) -> None:
    keys.load_or_create_keys()
    _store_pair(memory_keyring, {"signing": GOOD_KEY, "agreement": GOOD_KEY})

    with pytest.raises(keys.LinkKeyError, match="channel"):
        keys.load_or_create_keys()


@pytest.mark.parametrize(
    ("metadata", "message"),
    [
        ("{", "file cannot be used"),
        ({}, METADATA_RULE),
        ({"store": "cloud", "channel": GOOD_KEY, "agreement": GOOD_KEY}, METADATA_RULE),
        ({"store": "keyring", "agreement": GOOD_KEY}, METADATA_RULE),
        ({"store": "keyring", "channel": GOOD_KEY}, METADATA_RULE),
    ],
    ids=["not json", "empty", "unknown store", "no channel", "no agreement"],
)
@pytest.mark.usefixtures("memory_keyring")
def test_should_raise_link_key_error_for_invalid_metadata(
    config_dir: Path, metadata: str | dict[str, str], message: str
) -> None:
    text = metadata if isinstance(metadata, str) else json.dumps(metadata)
    (config_dir / keys.METADATA_FILE_NAME).write_text(text)

    with pytest.raises(keys.LinkKeyError) as raised:
        keys.load_or_create_keys()

    error = str(raised.value)
    assert (error.startswith(message), keys.METADATA_FILE_NAME in error) == (
        True,
        True,
    )


def test_should_load_existing_keys_when_config_dir_is_read_only(
    config_dir: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    created = keys.load_or_create_keys()
    os.chmod(config_dir, SHARED_DIR)
    monkeypatch.setattr(paths, "private_lock", _refuse_lock)

    loaded = keys.load_or_create_keys()

    assert (
        _public_bytes(loaded),
        stat.S_IMODE(config_dir.stat().st_mode),
    ) == (_public_bytes(created), SHARED_DIR)


def _load_in_child(
    config_dirs: list[str], barrier: Barrier, results: Queue[tuple[str, str]]
) -> None:
    keyring.set_keyring(fail.Keyring())
    for config_dir in config_dirs:
        os.environ["JOBRANGER_CONFIG_DIR"] = config_dir
        barrier.wait(RACE_TIMEOUT_SECONDS)
        try:
            outcome = str(_public_bytes(keys.load_or_create_keys()))
        except Exception as error:
            outcome = type(error).__name__
        results.put((config_dir, outcome))


@pytest.mark.usefixtures("no_keyring")
def test_should_agree_on_saved_keys_when_processes_start_together(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config_dirs = [str(tmp_path / f"trial-{trial}") for trial in range(RACE_TRIALS)]
    context = multiprocessing.get_context("fork")
    barrier = context.Barrier(RACE_PROCESSES)
    results: Queue[tuple[str, str]] = context.Queue()
    workers = [
        context.Process(target=_load_in_child, args=(config_dirs, barrier, results))
        for _ in range(RACE_PROCESSES)
    ]
    for worker in workers:
        worker.start()
    outcomes = [
        results.get(timeout=RACE_TIMEOUT_SECONDS)
        for _ in range(RACE_PROCESSES * RACE_TRIALS)
    ]
    for worker in workers:
        worker.join(RACE_TIMEOUT_SECONDS)

    saved = {}
    for config_dir in config_dirs:
        monkeypatch.setenv("JOBRANGER_CONFIG_DIR", config_dir)
        saved[config_dir] = str(_public_bytes(keys.load_or_create_keys()))
    mismatches = [outcome for outcome in outcomes if outcome[1] != saved[outcome[0]]]
    assert mismatches == []
