from __future__ import annotations

import errno
import os
import threading
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


def test_should_prefer_jobranger_config_dir_over_xdg_config_home(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setenv("XDG_CONFIG_HOME", str(tmp_path / "xdg"))
    monkeypatch.setenv("JOBRANGER_CONFIG_DIR", str(tmp_path / "own"))

    assert paths.config_dir() == tmp_path / "own"


def test_should_default_config_dir_to_dot_config_on_every_platform(
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


def test_should_keep_file_whole_when_written_concurrently(tmp_path: Path) -> None:
    target = tmp_path / "config.yaml"
    texts = [f"writer-{index}" * 512 for index in range(4)]
    errors: list[OSError] = []

    def write_repeatedly(text: str) -> None:
        try:
            for _ in range(100):
                paths.write_private(target, text)
        except OSError as error:
            errors.append(error)

    threads = [
        threading.Thread(target=write_repeatedly, args=(text,)) for text in texts
    ]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert (errors, target.read_text() in texts) == ([], True)
    assert [entry.name for entry in tmp_path.iterdir()] == ["config.yaml"]


def test_should_write_text_as_utf_8(tmp_path: Path) -> None:
    target = tmp_path / "config.yaml"

    paths.write_private(target, "name: æøå")

    assert target.read_bytes() == "name: æøå".encode()


def test_should_remove_temp_file_when_replace_fails(tmp_path: Path) -> None:
    target = tmp_path / "config.yaml"
    (target / "occupied").mkdir(parents=True)

    with pytest.raises(OSError, match="config.yaml"):
        paths.write_private(target, "text")

    assert [entry.name for entry in tmp_path.iterdir()] == ["config.yaml"]


def test_should_remove_temp_file_left_by_crashed_write(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    target = tmp_path / "config.yaml"
    with monkeypatch.context() as patch:
        patch.setattr(paths.os, "replace", lambda source, destination: None)
        paths.write_private(target, "text")
    leftovers = [entry.name for entry in tmp_path.iterdir()]

    paths.remove_stale_temps(target)

    assert (len(leftovers), list(tmp_path.iterdir())) == (1, [])


def test_should_raise_replace_error_when_temp_file_already_vanished(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def vanish_then_fail(source: str, destination: Path) -> None:
        Path(source).unlink()
        raise OSError(errno.EXDEV, os.strerror(errno.EXDEV))

    monkeypatch.setattr(paths.os, "replace", vanish_then_fail)

    with pytest.raises(OSError, match=os.strerror(errno.EXDEV)):
        paths.write_private(tmp_path / "config.yaml", "text")


def test_should_skip_stale_temp_that_vanished_before_removal(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    target = tmp_path / "config.yaml"
    vanished = tmp_path / ".config.yaml.gone.tmp"
    monkeypatch.setattr(paths.Path, "glob", lambda self, pattern: iter([vanished]))

    paths.remove_stale_temps(target)

    assert list(tmp_path.iterdir()) == []
