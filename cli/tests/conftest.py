from __future__ import annotations

import json
from contextlib import contextmanager
from typing import TYPE_CHECKING

import keyring
import pytest
from factories import FIXTURES, ListSink, MemoryKeyring, guard_network
from keyring.backends import fail

from jobranger import paths
from jobranger.matching.profile import Profile, load_profile
from jobranger.net import retry
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

    from keyring.backend import KeyringBackend

USER_DIR_ENVS = {
    "HOME": "home",
    paths.CONFIG_DIR_ENV: "config/jobranger",
    paths.DATA_DIR_ENV: "data/jobranger",
    paths.STATE_DIR_ENV: "state/jobranger",
    paths.XDG_CONFIG_HOME_ENV: "config",
    "XDG_DATA_HOME": "data",
    "XDG_STATE_HOME": "state",
    "XDG_CACHE_HOME": "cache",
}


@pytest.fixture(scope="session", autouse=True)
def isolated_user_dirs(tmp_path_factory: pytest.TempPathFactory) -> Iterator[None]:
    root = tmp_path_factory.mktemp("user")
    with pytest.MonkeyPatch.context() as patch:
        for name, folder in USER_DIR_ENVS.items():
            patch.setenv(name, str(root / folder))
        yield


@pytest.fixture(scope="session", autouse=True)
def isolated_keyring() -> Iterator[None]:
    with _use_keyring(MemoryKeyring()):
        yield


@pytest.fixture(autouse=True, scope="session")
def block_network() -> Iterator[None]:
    with pytest.MonkeyPatch.context() as patch:
        guard_network(patch)
        yield


@pytest.fixture
def finn_search_html() -> str:
    return (FIXTURES / "finn_search.html").read_text()


@pytest.fixture
def finn_ad_html() -> str:
    return (FIXTURES / "finn_ad.html").read_text()


@pytest.fixture
def nav_entry() -> dict[str, object]:
    return json.loads((FIXTURES / "nav_entry.json").read_text())


@pytest.fixture
def fake_profile() -> Profile:
    return load_profile(FIXTURES / "profile_user.yaml")


@pytest.fixture
def no_retry_wait(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(retry.time, "sleep", lambda seconds: None)


@pytest.fixture
def sink() -> ListSink:
    return ListSink()


@pytest.fixture
def store(tmp_path: Path) -> Store:
    return Store(tmp_path / "ads.db")


@pytest.fixture
def config_dir(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
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
