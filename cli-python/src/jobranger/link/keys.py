from __future__ import annotations

import base64
import hashlib
import json
import logging
import re
from contextlib import AbstractContextManager, ExitStack, contextmanager
from dataclasses import dataclass
from typing import TYPE_CHECKING

from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey

from jobranger import credentials, paths
from jobranger.logging_setup import log_event

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

logger = logging.getLogger(__name__)

LINK_KEYS_NAME = "link-keys"
METADATA_FILE_NAME = "link.json"
STORE_FIELD = "store"
CHANNEL_FIELD = "channel"
SIGNING_FIELD = "signing"
AGREEMENT_FIELD = "agreement"
PAIR_START_PURPOSE = "pair.start"
CHANNEL_PREFIX = b"jobranger-chan"
PROOF_PREFIX = "jobranger relay v1\n"
KEY_BYTES = 32
STORED_KEY_PATTERN = re.compile(r"[A-Za-z0-9_-]{43}")
STORES: dict[str, credentials.SecretStore] = {"keyring": "keyring", "file": "file"}
CREATED_EVENT = "link.keys.created"
ROTATED_EVENT = "link.keys.rotated"
UNPAIR_HINT = "run `jobranger unpair` to make new ones"


class LinkKeyError(Exception):
    pass


@dataclass(frozen=True)
class LinkKeys:
    signing: Ed25519PrivateKey
    agreement: X25519PrivateKey


@dataclass(frozen=True)
class _Metadata:
    store: credentials.SecretStore
    channel: str
    agreement: str


def load_or_create_keys() -> LinkKeys:
    try:
        existing, failure = _load_unlocked()
        if existing is not None:
            return existing
        with _locked_or_raise(failure):
            existing = _load_locked()
            if existing is not None:
                return existing
            created = _record(_new_pair())
    except (paths.PrivateFileError, credentials.CredentialsFileError) as error:
        raise LinkKeyError(str(error)) from error
    log_event(logger, logging.INFO, CREATED_EVENT)
    return created


def rotate_keys() -> LinkKeys:
    try:
        with _locked():
            rotated = _record(_new_pair())
    except (paths.PrivateFileError, credentials.CredentialsFileError) as error:
        raise LinkKeyError(str(error)) from error
    log_event(logger, logging.INFO, ROTATED_EVENT)
    return rotated


def channel_id(signing_public: bytes) -> str:
    return _encode(hashlib.sha256(CHANNEL_PREFIX + signing_public).digest())


def ownership_proof(keys: LinkKeys, purpose: str, nonce: str) -> bytes:
    message = f"{PROOF_PREFIX}{purpose}\n{nonce}"
    return keys.signing.sign(message.encode("utf-8"))


def _load_unlocked() -> tuple[LinkKeys | None, LinkKeyError | None]:
    metadata = _read_metadata()
    if metadata is None:
        return None, None
    try:
        return _check(metadata), None
    except LinkKeyError as error:
        return None, error


@contextmanager
def _locked_or_raise(failure: LinkKeyError | None) -> Iterator[None]:
    with ExitStack() as stack:
        try:
            stack.enter_context(_locked())
        except paths.PrivateFileError as error:
            if failure is None:
                raise
            raise failure from error
        yield


def _load_locked() -> LinkKeys | None:
    metadata = _read_metadata()
    if metadata is None:
        return _adopt()
    return _check(metadata)


def _locked() -> AbstractContextManager[None]:
    return paths.locked(credentials.secrets_path(), _metadata_path())


def _adopt() -> LinkKeys | None:
    file_pair = credentials.read_secret_from("file", LINK_KEYS_NAME)
    try:
        keyring_pair = credentials.read_secret_from("keyring", LINK_KEYS_NAME)
    except credentials.KeyringMissingError as error:
        if file_pair is None:
            return None
        msg = (
            "keyring backend is missing so stored link keys cannot be checked "
            f"store=keyring name={LINK_KEYS_NAME}, {UNPAIR_HINT}"
        )
        raise credentials.KeyringMissingError(msg) from error
    found = [
        (pair, store)
        for pair, store in (
            (keyring_pair, STORES["keyring"]),
            (file_pair, STORES["file"]),
        )
        if pair is not None
    ]
    if not found:
        return None
    if len({pair for pair, _ in found}) > 1:
        msg = f"link keys differ between keyring and file, {UNPAIR_HINT}"
        raise LinkKeyError(msg)
    stored, store = found[0]
    keys = _parse_pair(stored)
    _write_metadata(store, keys)
    return keys


def _check(metadata: _Metadata) -> LinkKeys:
    store = metadata.store
    recorded = credentials.read_secret_from(store, LINK_KEYS_NAME)
    if recorded is None:
        msg = (
            f"link keys missing from recorded store store={store} name={LINK_KEYS_NAME}"
        )
        raise LinkKeyError(msg)
    keys = _parse_pair(recorded)
    if _channel_of(keys) != metadata.channel:
        msg = f"stored link keys do not match the recorded channel; {UNPAIR_HINT}"
        raise LinkKeyError(msg)
    if metadata.agreement != _agreement_of(keys):
        msg = f"stored link keys do not match the recorded agreement key; {UNPAIR_HINT}"
        raise LinkKeyError(msg)
    return keys


def _record(pair: str) -> LinkKeys:
    keys = _parse_pair(pair)
    store = credentials.set_secret(LINK_KEYS_NAME, pair)
    _write_metadata(store, keys)
    return keys


def _metadata_path() -> Path:
    return paths.config_dir() / METADATA_FILE_NAME


def _read_metadata() -> _Metadata | None:
    path = _metadata_path()
    metadata = paths.read_json_object(path)
    if metadata is None:
        return None
    store = metadata.get(STORE_FIELD)
    channel = metadata.get(CHANNEL_FIELD)
    agreement = metadata.get(AGREEMENT_FIELD)
    if store not in STORES or channel is None or agreement is None:
        msg = (
            "link metadata needs store keyring or file, a channel and an agreement "
            f"key path={path}, {UNPAIR_HINT}"
        )
        raise LinkKeyError(msg)
    return _Metadata(STORES[store], channel, agreement)


def _write_metadata(store: credentials.SecretStore, keys: LinkKeys) -> None:
    paths.write_json_object(
        _metadata_path(),
        {
            STORE_FIELD: store,
            CHANNEL_FIELD: _channel_of(keys),
            AGREEMENT_FIELD: _agreement_of(keys),
        },
    )


def _channel_of(keys: LinkKeys) -> str:
    return channel_id(keys.signing.public_key().public_bytes_raw())


def _agreement_of(keys: LinkKeys) -> str:
    return _encode(keys.agreement.public_key().public_bytes_raw())


def _new_pair() -> str:
    return json.dumps(
        {
            SIGNING_FIELD: _encode(Ed25519PrivateKey.generate().private_bytes_raw()),
            AGREEMENT_FIELD: _encode(X25519PrivateKey.generate().private_bytes_raw()),
        }
    )


def _parse_pair(stored: str) -> LinkKeys:
    msg = f"stored link keys are not a JSON object name={LINK_KEYS_NAME}"
    try:
        pair = json.loads(stored)
    except ValueError:
        raise LinkKeyError(msg) from None
    if not isinstance(pair, dict):
        raise LinkKeyError(msg)
    return LinkKeys(
        signing=Ed25519PrivateKey.from_private_bytes(_key_field(pair, SIGNING_FIELD)),
        agreement=X25519PrivateKey.from_private_bytes(
            _key_field(pair, AGREEMENT_FIELD)
        ),
    )


def _key_field(pair: dict[str, object], field: str) -> bytes:
    value = pair.get(field)
    if not isinstance(value, str) or STORED_KEY_PATTERN.fullmatch(value) is None:
        msg = (
            f"stored link key is not {KEY_BYTES} base64url bytes "
            f"name={LINK_KEYS_NAME} field={field}"
        )
        raise LinkKeyError(msg)
    return base64.urlsafe_b64decode(value + "=")


def _encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")
