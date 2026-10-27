from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, ConfigDict

from jobranger.catalogue import Level, RoleType

type Fit = Literal["strong", "stretch", "unsuitable", "unknown"]
type CheckState = Literal["met", "partly", "missing"]


class Check(BaseModel):
    model_config = ConfigDict(extra="forbid")

    text: str
    state: CheckState


class MatchAssessment(BaseModel):
    model_config = ConfigDict(extra="forbid")

    status: Fit
    reason: str
    evidence: list[str]
    gaps: list[str]
    blockers: list[str]
    is_low_code: bool
    required_experience_years: float | None


class AdAssessment(BaseModel):
    model_config = ConfigDict(extra="forbid")

    is_programming_job: bool
    label: Level
    summary: str
    requires_master: bool
    display_title: str | None
    role_type: RoleType | None
    technologies: list[str]
    requirements: list[str]
    checks: list[Check]
    match: MatchAssessment
