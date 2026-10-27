from __future__ import annotations

from typing import TYPE_CHECKING

import pytest
from pydantic import ValidationError

from jobranger.matching.profile import (
    Evidence,
    Profile,
    load_profile,
    save_profile,
)
from jobranger.settings import ConfigInvalidError

if TYPE_CHECKING:
    from pathlib import Path


def test_should_reject_duplicate_evidence_ids_across_lists(
    fake_profile: Profile,
) -> None:
    duplicate = Evidence(id="java_project", name="Java course", facts="Java.")

    with pytest.raises(ValidationError, match="java_project"):
        Profile.model_validate(
            {
                **fake_profile.model_dump(),
                "education": [*fake_profile.model_dump()["education"], duplicate],
            }
        )


def test_should_load_saved_profile_unchanged(
    fake_profile: Profile, tmp_path: Path
) -> None:
    path = tmp_path / "profile.yaml"

    save_profile(path, fake_profile)

    assert load_profile(path) == fake_profile


def test_should_name_profile_path_when_profile_is_invalid(
    fake_profile: Profile, tmp_path: Path
) -> None:
    path = tmp_path / "profile.yaml"
    save_profile(path, fake_profile)
    text = path.read_text()
    path.write_text(text.replace("professional_years: ", "professional_years: -", 1))

    with pytest.raises(ConfigInvalidError) as raised:
        load_profile(path)

    assert (raised.value.path, raised.value.errors[0].split(":")[0]) == (
        path,
        "professional_years",
    )
