from __future__ import annotations

import stat
from typing import TYPE_CHECKING

import pytest
from pydantic import ValidationError

from jobranger.catalogue import default_employer_feeds
from jobranger.settings import (
    ConfigInvalidError,
    ConfigMissingError,
    ProfileRef,
    ScheduleConfig,
    SourcesConfig,
    Targets,
    default_config,
    load_config,
    save_config,
)

if TYPE_CHECKING:
    from pathlib import Path

OWNER_READ_WRITE = 0o600
OWNER_ONLY_DIR = 0o700
COMMENT = "# chosen during setup"


@pytest.fixture
def config_path(tmp_path: Path) -> Path:
    path = tmp_path / "config.yaml"
    save_config(path, default_config())
    return path


def test_should_keep_comments_when_saving_config(config_path: Path) -> None:
    text = config_path.read_text().replace(
        "  relocate: true", f"  relocate: true  {COMMENT}"
    )
    config_path.write_text(text)
    config = load_config(config_path)
    changed = config.model_copy(
        update={"targets": config.targets.model_copy(update={"places": ("Bergen",)})}
    )

    save_config(config_path, changed)

    assert COMMENT in config_path.read_text()


def test_should_remove_key_dropped_from_model_when_saving(config_path: Path) -> None:
    config_path.write_text(config_path.read_text() + "legacy_key: 1\n")

    save_config(config_path, default_config())

    assert "legacy_key" not in config_path.read_text()


def test_should_write_config_with_owner_only_permissions(config_path: Path) -> None:
    assert stat.S_IMODE(config_path.stat().st_mode) == OWNER_READ_WRITE


def test_should_reject_run_times_less_than_two_hours_apart() -> None:
    with pytest.raises(ValidationError, match="2 hours"):
        ScheduleConfig(times=("23:30", "01:00"))


def test_should_reject_unknown_config_key(config_path: Path) -> None:
    config_path.write_text(config_path.read_text() + "unknown_key: 1\n")

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert raised.value.path == config_path


def test_should_reject_unknown_role_in_subfields() -> None:
    with pytest.raises(ValidationError):
        Targets.model_validate({"subfields": ["blockchain"]})


def test_should_default_finn_and_linkedin_off() -> None:
    sources = SourcesConfig()

    assert (sources.finn, sources.linkedin) == (False, False)


def test_should_report_dotted_location_for_invalid_value(config_path: Path) -> None:
    text = config_path.read_text().replace("hide_masters: true", "hide_masters: 7")
    config_path.write_text(text)

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert any(
        error.startswith("targets.limits.hide_masters") for error in raised.value.errors
    )


def test_should_name_missing_config_path(tmp_path: Path) -> None:
    missing = tmp_path / "absent.yaml"

    with pytest.raises(ConfigMissingError) as raised:
        load_config(missing)

    assert raised.value.path == missing


def test_should_create_private_parent_dirs_when_saving(tmp_path: Path) -> None:
    path = tmp_path / "nested" / "jobranger" / "config.yaml"

    save_config(path, default_config())

    assert stat.S_IMODE(path.parent.stat().st_mode) == OWNER_ONLY_DIR


def test_should_write_scalar_lists_inline(config_path: Path) -> None:
    assert "levels: [junior, mid, none]" in config_path.read_text()


def test_should_write_feed_list_as_blocks(config_path: Path) -> None:
    assert "  - kind: teamtailor" in config_path.read_text()


def test_should_copy_catalogue_feeds_into_default_config() -> None:
    assert default_config().sources.employer_feeds == default_employer_feeds()


def test_should_round_trip_saved_config(config_path: Path) -> None:
    assert load_config(config_path) == default_config()


def test_should_write_profile_path_relative_to_home(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setenv("HOME", str(tmp_path))
    path = tmp_path / "config.yaml"
    config = default_config().model_copy(
        update={"profile": ProfileRef(path=tmp_path / ".config" / "profile.yaml")}
    )

    save_config(path, config)

    assert "path: ~/.config/profile.yaml" in path.read_text()


def test_should_save_changed_scalar_value(config_path: Path) -> None:
    config = load_config(config_path)
    changed = config.model_copy(
        update={"targets": config.targets.model_copy(update={"relocate": False})}
    )

    save_config(config_path, changed)

    assert load_config(config_path).targets.relocate is False


def test_should_write_empty_optional_keys_in_new_file(config_path: Path) -> None:
    text = config_path.read_text()

    assert "  cv_name:\n" in text


def test_should_replace_leftover_temp_file_when_saving(tmp_path: Path) -> None:
    path = tmp_path / "config.yaml"
    path.with_suffix(".yaml.tmp").write_text("junk: [" * 2000)

    save_config(path, default_config())

    assert load_config(path) == default_config()
