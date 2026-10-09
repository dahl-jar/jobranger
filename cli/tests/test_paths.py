from __future__ import annotations

from pathlib import Path

import pytest

from jobranger import paths


@pytest.fixture(autouse=True)
def clear_path_env(monkeypatch: pytest.MonkeyPatch) -> None:
    for name in (
        "JOBRANGER_CONFIG_DIR",
        "JOBRANGER_DATA_DIR",
        "JOBRANGER_STATE_DIR",
        "XDG_CONFIG_HOME",
        "XDG_DATA_HOME",
    ):
        monkeypatch.delenv(name, raising=False)


def test_should_use_xdg_config_home_when_set(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setenv("XDG_CONFIG_HOME", str(tmp_path))

    assert paths.config_dir() == tmp_path / "jobranger"


def test_should_default_config_dir_to_dot_config_whatever_the_platform_dir(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setattr(
        paths.platformdirs, "user_config_path", lambda name: tmp_path / "platform"
    )
    monkeypatch.setenv("HOME", str(tmp_path))

    assert paths.config_dir() == tmp_path / ".config" / "jobranger"


def test_should_prefer_jobranger_data_dir_env_over_platform_dir(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setenv("JOBRANGER_DATA_DIR", str(tmp_path))

    assert paths.db_path() == tmp_path / "jobranger.db"


def test_should_place_files_under_overridden_dirs(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setenv("JOBRANGER_CONFIG_DIR", str(tmp_path / "config"))
    monkeypatch.setenv("JOBRANGER_STATE_DIR", str(tmp_path / "state"))

    assert (
        paths.config_path(),
        paths.profile_path(),
        paths.run_lock_path(),
        paths.log_dir(),
    ) == (
        tmp_path / "config" / "config.yaml",
        tmp_path / "config" / "profile.yaml",
        tmp_path / "state" / "run.lock",
        tmp_path / "state" / "logs",
    )


def test_should_name_platform_dirs_after_app() -> None:
    assert (paths.data_dir().name, paths.state_dir().name) == (
        "jobranger",
        "jobranger",
    )
