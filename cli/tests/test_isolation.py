from __future__ import annotations

import keyring
import pytest
from factories import MemoryKeyring

from jobranger import paths


def test_should_resolve_user_dirs_under_test_tmp_without_per_test_fixtures(
    monkeypatch: pytest.MonkeyPatch, tmp_path_factory: pytest.TempPathFactory
) -> None:
    for name in (paths.CONFIG_DIR_ENV, paths.XDG_CONFIG_HOME_ENV):
        monkeypatch.delenv(name, raising=False)

    resolved = [paths.config_dir(), paths.data_dir(), paths.state_dir()]

    root = tmp_path_factory.getbasetemp()
    assert [path for path in resolved if not path.is_relative_to(root)] == []


def test_should_use_fake_keyring_without_per_test_fixtures() -> None:
    assert isinstance(keyring.get_keyring(), MemoryKeyring)
