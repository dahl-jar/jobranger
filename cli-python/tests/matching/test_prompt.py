from __future__ import annotations

from typing import TYPE_CHECKING

import pytest
from factories import FAKE_LIMITS, FIXTURES, GUIDANCE, targets

from jobranger.matching.guidance import DEFAULT_TEXT, guidance_path, save_guidance
from jobranger.matching.profile import Profile, profile_text
from jobranger.matching.prompt import (
    GUIDANCE_HEADING,
    GUIDANCE_LOCK,
    assessment_version,
    build_system,
    system_for,
)
from jobranger.matching.rules import limit_rules
from jobranger.settings import AppConfig, Limits, ProfileRef

if TYPE_CHECKING:
    from pathlib import Path

LIMITS = "The search accepts roles requiring at most 1 years."
DEFAULT_HEADER = (FIXTURES / "main_prompt_header.txt").read_text(encoding="utf-8")
DEFAULT_RULES = (FIXTURES / "main_base_rules.txt").read_text(encoding="utf-8")
WIDE_LIMITS = Limits(
    levels=frozenset({"junior", "senior"}),
    years=frozenset({"0-1", "6+"}),
    hide_masters=False,
)
DEFAULT_LIMIT_LINES = {
    FAKE_LIMITS: (FIXTURES / "main_limit_rules_narrow.txt").read_text(encoding="utf-8"),
    WIDE_LIMITS: (FIXTURES / "main_limit_rules_wide.txt").read_text(encoding="utf-8"),
}
GUIDANCE_BLOCK = f"{GUIDANCE_HEADING}\n{GUIDANCE}\n{GUIDANCE_LOCK}\n"


def _default_system(limits: Limits, profile: Profile) -> str:
    rules = f"{DEFAULT_RULES.rstrip()}\n{DEFAULT_LIMIT_LINES[limits]}"
    return "\n".join(
        [DEFAULT_HEADER, rules, f"Candidate profile:\n{profile_text(profile)}"]
    )


@pytest.mark.parametrize("limits", [FAKE_LIMITS, WIDE_LIMITS], ids=["narrow", "wide"])
def test_should_build_default_prompt_for_empty_guidance(
    fake_profile: Profile, limits: Limits
) -> None:
    system = build_system(DEFAULT_TEXT, limit_rules(limits), fake_profile)

    assert system == _default_system(limits, fake_profile)


@pytest.mark.parametrize("limits", [FAKE_LIMITS, WIDE_LIMITS], ids=["narrow", "wide"])
def test_should_insert_guidance_block_before_limits(
    fake_profile: Profile, limits: Limits
) -> None:
    default = _default_system(limits, fake_profile)
    position = default.index(DEFAULT_LIMIT_LINES[limits])

    system = build_system(GUIDANCE, limit_rules(limits), fake_profile)

    assert system == f"{default[:position]}{GUIDANCE_BLOCK}{default[position:]}"


def test_should_read_saved_guidance_into_system(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, fake_profile: Profile
) -> None:
    monkeypatch.setenv("JOBRANGER_CONFIG_DIR", str(tmp_path))
    save_guidance(guidance_path(), GUIDANCE)
    config = AppConfig(
        profile=ProfileRef(path=FIXTURES / "profile_user.yaml"), targets=targets()
    )

    system = system_for(config, fake_profile)

    assert GUIDANCE_BLOCK in system


def test_should_change_version_when_guidance_changes(fake_profile: Profile) -> None:
    first = build_system(GUIDANCE, LIMITS, fake_profile)
    second = build_system("Judge roles by the stack.", LIMITS, fake_profile)

    assert assessment_version(first) != assessment_version(second)
