from __future__ import annotations

from collections import Counter
from typing import TYPE_CHECKING, Annotated

from pydantic import BaseModel, ConfigDict, Field, ValidationError, model_validator

from jobranger.settings import (
    ConfigInvalidError,
    read_yaml,
    validation_errors,
    write_yaml,
)

if TYPE_CHECKING:
    from pathlib import Path

EVIDENCE_ID_PATTERN = r"^[a-z][a-z0-9_]{1,39}$"
FACTS_LIMIT = 1200
MONTH_PATTERN = r"^\d{4}-\d{2}$"
PROFILE_INDENT = 1


class Evidence(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    id: Annotated[str, Field(pattern=EVIDENCE_ID_PATTERN)]
    name: str
    facts: Annotated[str, Field(max_length=FACTS_LIMIT)]
    start: Annotated[str, Field(pattern=MONTH_PATTERN)] | None = None
    end: Annotated[str, Field(pattern=MONTH_PATTERN)] | None = None


class Profile(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    summary: str
    professional_years: Annotated[float, Field(ge=0)]
    has_master: bool = False
    education: tuple[Evidence, ...] = ()
    experience: tuple[Evidence, ...] = ()
    projects: tuple[Evidence, ...] = ()
    languages: tuple[str, ...] = ()
    stack: tuple[str, ...] = ()

    @model_validator(mode="after")
    def _unique_ids(self) -> Profile:
        counts = Counter(
            item.id for item in (*self.education, *self.experience, *self.projects)
        )
        repeated = sorted(item for item, count in counts.items() if count > 1)
        if repeated:
            msg = f"evidence ids used more than once: {', '.join(repeated)}"
            raise ValueError(msg)
        return self


def load_profile(path: Path) -> Profile:
    data = read_yaml(path)
    try:
        return Profile.model_validate(data)
    except ValidationError as error:
        raise ConfigInvalidError(path, validation_errors(error)) from error


def save_profile(path: Path, profile: Profile) -> None:
    write_yaml(path, profile.model_dump(mode="json"))


def evidence_ids(profile: Profile) -> frozenset[str]:
    return frozenset(
        item.id for item in (*profile.education, *profile.experience, *profile.projects)
    )


def education_ids(profile: Profile) -> frozenset[str]:
    return frozenset(item.id for item in profile.education)


def profile_text(profile: Profile) -> str:
    return profile.model_dump_json(indent=PROFILE_INDENT)
