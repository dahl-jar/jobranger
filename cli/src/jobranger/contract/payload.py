from __future__ import annotations

from datetime import date
from typing import Annotated, Literal

from pydantic import (
    BaseModel,
    ConfigDict,
    Field,
    RootModel,
    StrictBool,
    StrictFloat,
    StrictInt,
    StrictStr,
)


class MessageId(RootModel[StrictStr]):
    root: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]


class AdId(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=200, min_length=1)]


class ModelId(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=120, min_length=1)]


class Term(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=60, min_length=1)]


class Timestamp(RootModel[StrictStr]):
    root: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]


class ClockTime(RootModel[StrictStr]):
    root: Annotated[
        StrictStr,
        Field(max_length=5, min_length=5, pattern="^([01][0-9]|2[0-3]):[0-5][0-9]$"),
    ]


class Money(RootModel[StrictInt | StrictFloat]):
    root: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )


class Count(RootModel[StrictInt]):
    root: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class ShortText(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=300)]


class Check(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    text: Annotated[StrictStr, Field(max_length=160, min_length=1)]
    state: Literal["met", "partly", "missing"]


class Place(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=80, min_length=1)]


class Base64Data(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(pattern="^[A-Za-z0-9+/]*={0,2}$")]


class Url(RootModel[StrictStr]):
    root: Annotated[
        StrictStr, Field(max_length=2048, pattern="^https?://[\\x21-\\x7e]+$")
    ]


class Email(RootModel[StrictStr]):
    root: Annotated[
        StrictStr,
        Field(
            max_length=254,
            pattern="^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]+$",
        ),
    ]


class Recheck(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    ads: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    cost_usd: Money | None


class LastRun(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    finished_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    ads_read: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    matches: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    cost_usd: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )


class ActiveRun(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    mode: Literal["scheduled", "manual", "dry_run"]
    started_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    results_by: Timestamp | None


class ActiveAd(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    company: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    title: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    place: Annotated[StrictStr, Field(max_length=120)]
    role_type: (
        Literal["general_software"]
        | Literal[
            "backend",
            "frontend",
            "fullstack",
            "mobile",
            "data_engineering",
            "devops_platform",
            "ml_ai",
            "embedded",
            "game",
        ]
    )
    source: Literal["nav", "finn", "linkedin", "teamtailor", "ashby"]
    published_at: Timestamp | None
    found_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    url: Annotated[
        StrictStr, Field(max_length=2048, pattern="^https?://[\\x21-\\x7e]+$")
    ]
    deadline: date | None
    fit: Literal["strong", "partial", "on_request"]
    is_new: StrictBool
    today: StrictBool
    summary: Annotated[StrictStr, Field(max_length=700)]
    reason: Annotated[StrictStr, Field(max_length=300)]
    gaps: Annotated[list[ShortText], Field(max_length=5)]
    checks: Annotated[list[Check], Field(max_length=5)]
    mark: Literal["applied", "skipped", "not_for_me"] | None
    marked_at: Timestamp | None
    shown_anyway: StrictBool


class HiddenAd(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    company: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    title: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    place: Annotated[StrictStr, Field(max_length=120)]
    role_type: (
        Literal["general_software"]
        | Literal[
            "backend",
            "frontend",
            "fullstack",
            "mobile",
            "data_engineering",
            "devops_platform",
            "ml_ai",
            "embedded",
            "game",
        ]
        | None
    )
    source: Literal["nav", "finn", "linkedin", "teamtailor", "ashby"]
    url: Annotated[
        StrictStr, Field(max_length=2048, pattern="^https?://[\\x21-\\x7e]+$")
    ]
    found_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    today: StrictBool
    kind: Literal["senior", "years", "masters", "role", "place", "other"]
    reason: Annotated[StrictStr, Field(max_length=120, min_length=1)]


class HiddenCounts(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    senior: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    years: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    masters: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    role: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    place: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    other: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class ClosedAd(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    closed_on: date
    company: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    title: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    source: Literal["nav", "finn", "linkedin", "teamtailor", "ashby"]
    reason: Literal["deadline", "removed", "filled"]
    mark: Literal["applied", "skipped", "not_for_me", "none"]


class HistoryEntry(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    text: Annotated[StrictStr, Field(max_length=200, min_length=1)]


class CvName(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=120)]


class Application(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    ad_id: AdId | None
    company: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    title: Annotated[StrictStr, Field(max_length=200)]
    place: Annotated[StrictStr, Field(max_length=120)]
    role_type: (
        Literal["general_software"]
        | Literal[
            "backend",
            "frontend",
            "fullstack",
            "mobile",
            "data_engineering",
            "devops_platform",
            "ml_ai",
            "embedded",
            "game",
        ]
        | None
    )
    source: Literal["nav", "finn", "linkedin", "teamtailor", "ashby"] | None
    url: Url | None
    deadline: date | None
    fit: Literal["strong", "partial", "on_request"] | None
    status: Literal["waiting", "interview", "offer", "accepted", "rejected"]
    note: Annotated[StrictStr, Field(max_length=2000)]
    applied_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    found_at: Timestamp | None
    cv_name: CvName | None
    checks: Annotated[list[Check], Field(max_length=5)]
    history: Annotated[list[HistoryEntry], Field(max_length=20)]
    removed: StrictBool


class RoleOption(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Literal[
        "backend",
        "frontend",
        "fullstack",
        "mobile",
        "data_engineering",
        "devops_platform",
        "ml_ai",
        "embedded",
        "game",
    ]
    label: Annotated[StrictStr, Field(max_length=40, min_length=1)]


class Targets(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    subfields: Annotated[
        list[
            Literal[
                "backend",
                "frontend",
                "fullstack",
                "mobile",
                "data_engineering",
                "devops_platform",
                "ml_ai",
                "embedded",
                "game",
            ]
        ],
        Field(max_length=9),
    ]
    levels: Annotated[
        list[Literal["junior", "mid", "senior", "lead", "none"]], Field(max_length=5)
    ]
    years: Annotated[list[Literal["0-1", "1-3", "3-6", "6+"]], Field(max_length=4)]
    hide_masters: StrictBool
    places: Annotated[list[Place], Field(max_length=20)]
    relocate: StrictBool


class Sources(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    nav: StrictBool
    employers: StrictBool
    finn: StrictBool
    linkedin: StrictBool


class Schedule(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    times: Annotated[list[ClockTime], Field(max_length=12)]
    min_interval_hours: Annotated[StrictInt, Field(ge=2, le=2)]


class EmailNotify(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    on: StrictBool
    address: Email | None


class Notify(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    website: StrictBool
    email: EmailNotify


class Price(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    input_per_mtok: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )
    output_per_mtok: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )


class KeyHint(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=4)]


class ModelConfig(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    provider: Literal["anthropic", "openai", "openrouter", "ollama"] | None
    model: ModelId | None
    key_hint: KeyHint | None
    price: Price
    monthly_limit_usd: Money | None


class TargetsPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    subfields: Annotated[
        list[
            Literal[
                "backend",
                "frontend",
                "fullstack",
                "mobile",
                "data_engineering",
                "devops_platform",
                "ml_ai",
                "embedded",
                "game",
            ]
        ]
        | None,
        Field(max_length=9),
    ] = None
    levels: Annotated[
        list[Literal["junior", "mid", "senior", "lead", "none"]] | None,
        Field(max_length=5),
    ] = None
    years: Annotated[
        list[Literal["0-1", "1-3", "3-6", "6+"]] | None, Field(max_length=4)
    ] = None
    hide_masters: StrictBool | None = None
    places: Annotated[list[Place] | None, Field(max_length=20)] = None
    relocate: StrictBool | None = None


class SourcesPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    nav: StrictBool | None = None
    employers: StrictBool | None = None
    finn: StrictBool | None = None
    linkedin: StrictBool | None = None


class SchedulePatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    times: Annotated[list[ClockTime] | None, Field(max_length=12)] = None


class EmailNotifyPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    on: StrictBool | None = None
    address: Email | None = None


class NotifyPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    website: StrictBool | None = None
    email: EmailNotifyPatch | None = None


class PricePatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    input_per_mtok: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
        | None
    ) = None
    output_per_mtok: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
        | None
    ) = None


class ModelConfigPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    price: PricePatch | None = None
    monthly_limit_usd: Money | None = None


class ConfigPatch(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    targets: TargetsPatch | None = None
    sources: SourcesPatch | None = None
    schedule: SchedulePatch | None = None
    notify: NotifyPatch | None = None
    model: ModelConfigPatch | None = None
    language: Literal["en", "nb"] | None = None


class ProjectRef(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=40, min_length=1)]
    name: Annotated[StrictStr, Field(max_length=120, min_length=1)]


class Id(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=40, min_length=1)]


class ProjectEdit(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Id | None
    name: Annotated[StrictStr, Field(max_length=120, min_length=1)]


class UsageDay(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    day: date
    calls: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    input_tokens: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    output_tokens: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    cost_usd: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )
    ads_read: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    ads_checked: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class ModelInfo(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=120, min_length=1)]
    name: Annotated[StrictStr, Field(max_length=120, min_length=1)]
    input_per_mtok: Money | None
    output_per_mtok: Money | None
    usd_per_day: Money | None


class ProviderModels(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    provider: Literal["anthropic", "openai", "openrouter", "ollama"]
    error: ShortText | None
    models: Annotated[list[ModelInfo], Field(max_length=1000)]


class ContextDoc(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    id: Annotated[StrictStr, Field(max_length=64, pattern="^doc_[a-z0-9_]+$")]
    name: Annotated[StrictStr, Field(max_length=120, min_length=1)]
    chars: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    added_at: date


class LearningExample(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    decision_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    ad_id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    company: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    title: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    decision: Literal["applied", "not_for_me", "show_anyway"]
    at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]


class SourceStep(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    source: Literal["nav", "finn", "linkedin", "teamtailor", "ashby"]
    state: Literal["pending", "active", "done", "failed"]
    found_new: Count | None


class ModelStep(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    state: Literal["pending", "active", "done", "skipped"]
    checked: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    results_by: Timestamp | None


class Status(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["status"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    cli_version: Annotated[StrictStr, Field(max_length=32, min_length=1)]
    contract_version: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    machine: Annotated[StrictStr, Field(max_length=64, min_length=1)]
    paired_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    last_run: LastRun | None
    next_run_at: Timestamp | None
    running: ActiveRun | None
    budget_paused: StrictBool


class AdsSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["ads.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    generated_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    as_of: date
    active: Annotated[list[ActiveAd], Field(max_length=73)]
    active_total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    hidden: Annotated[list[HiddenAd], Field(max_length=500)]
    hidden_total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    hidden_counts: HiddenCounts
    closed: Annotated[list[ClosedAd], Field(max_length=200)]
    closed_total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class ApplicationsSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["applications.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    generated_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    applications: Annotated[list[Application], Field(max_length=145)]
    applications_total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class ConfigSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["config.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    roles: Annotated[list[RoleOption], Field(max_length=9)]
    targets: Targets
    sources: Sources
    schedule: Schedule
    notify: Notify
    model: ModelConfig
    language: Literal["en", "nb"]


class ProfileSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["profile.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    cv_name: CvName | None
    cv_read_at: Timestamp | None
    summary: Annotated[StrictStr, Field(max_length=300)]
    education: Annotated[StrictStr, Field(max_length=300)]
    languages: Annotated[list[Term], Field(max_length=40)]
    stack: Annotated[list[Term], Field(max_length=40)]
    projects: Annotated[list[ProjectRef], Field(max_length=20)]


class UsageSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["usage.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    month: Annotated[
        StrictStr,
        Field(max_length=7, min_length=7, pattern="^[0-9]{4}-(0[1-9]|1[0-2])$"),
    ]
    month_tokens: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    today_tokens: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    month_usd: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )
    today_usd: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )
    resets_on: date
    days: Annotated[list[UsageDay], Field(max_length=14)]


class ModelsSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["models.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    providers: Annotated[list[ProviderModels], Field(max_length=4)]


class GuidanceSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["guidance.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    text: Annotated[StrictStr, Field(max_length=12000)]
    is_default: StrictBool
    limit: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]


class ContextSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["context.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    about: Annotated[StrictStr, Field(max_length=4000)]
    about_limit: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    docs: Annotated[list[ContextDoc], Field(max_length=10)]
    docs_limit: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]


class UnavailableReason(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=200)]


class LearningSnapshot(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["learning.snapshot"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    state: Literal["unavailable", "collecting", "preparing", "on"]
    unavailable_reason: UnavailableReason | None
    decision_count: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    cold_start_threshold: Annotated[StrictInt, Field(ge=20, le=20)]
    examples: Annotated[list[LearningExample], Field(max_length=200)]
    examples_total: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]


class RunProgress(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["run.progress"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    mode: Literal["scheduled", "manual", "dry_run"]
    started_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    percent: Annotated[StrictInt, Field(ge=0, le=100)]
    sources: Annotated[list[SourceStep], Field(max_length=5)]
    model: ModelStep


class RunFinished(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["run.finished"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    finished_at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]
    ads_read: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    new_ads: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    matches: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    hidden: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    cost_usd: (
        Annotated[StrictInt, Field(ge=0.0, le=1000000.0)]
        | Annotated[StrictFloat, Field(ge=0.0, le=1000000.0)]
    )
    failed_sources: Annotated[
        list[Literal["nav", "finn", "linkedin", "teamtailor", "ashby"]],
        Field(max_length=5),
    ]
    budget_paused: StrictBool


class RunCancelled(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["run.cancelled"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    at: Annotated[
        StrictStr,
        Field(
            pattern="^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?(Z|[+-]([01][0-9]|2[0-3]):[0-5][0-9])$"
        ),
    ]


class Saved(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["saved"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    ref: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    applies_from: Literal["now", "next_run"]
    recheck: Recheck | None = None


class FieldModel(RootModel[StrictStr]):
    root: Annotated[StrictStr, Field(max_length=120)]


class ErrorMessage(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["error"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    ref: MessageId | None
    code: Literal[
        "invalid_payload",
        "unknown_type",
        "unsupported_version",
        "not_found",
        "run_in_progress",
        "no_run",
        "run_failed",
        "key_rejected",
        "cv_type",
        "cv_unreadable",
        "cv_no_text",
        "cv_too_large",
        "cv_parse_failed",
        "chunk_incomplete",
        "internal",
        "guidance_empty",
        "context_full",
        "context_unreadable",
    ]
    message: Annotated[StrictStr, Field(max_length=300, min_length=1)]
    field: FieldModel | None


class Unpaired(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["unpaired"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    reason: Literal["refused", "web", "cli", "revoked"]


class RunStart(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["run.start"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]


class RunCancel(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["run.cancel"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    run_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]


class AdMark(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["ad.mark"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    ad_id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    mark: Literal["applied", "skipped", "not_for_me"]


class AdUnmark(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["ad.unmark"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    ad_id: Annotated[StrictStr, Field(max_length=200, min_length=1)]


class AdShowAnyway(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["ad.show_anyway"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    ad_id: Annotated[StrictStr, Field(max_length=200, min_length=1)]
    on: StrictBool


class ApplicationCreate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["application.create"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    company: Annotated[
        StrictStr,
        Field(
            max_length=200,
            min_length=1,
            pattern="[^\\u0000-\\u0020\\u007f-\\u00a0\\u00ad\\u034f\\u061c\\u115f-\\u1160\\u1680\\u17b4-\\u17b5\\u180b-\\u180f\\u2000-\\u200f\\u2028-\\u202f\\u205f-\\u206f\\u2800\\u3000\\u3164\\ufe00-\\ufe0f\\ufeff\\uffa0\\ufff0-\\ufff8\U0001bca0-\U0001bca3\U0001d173-\U0001d17a\U000e0000-\U000e0fff]",
        ),
    ]
    title: Annotated[StrictStr, Field(max_length=200)]
    note: Annotated[StrictStr, Field(max_length=2000)]


class ApplicationUpdate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["application.update"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    application_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    status: Literal["waiting", "interview", "offer", "accepted", "rejected"] | None = (
        None
    )
    note: Annotated[StrictStr | None, Field(max_length=2000)] = None


class ApplicationRemove(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["application.remove"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    application_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]
    removed: StrictBool


class ConfigUpdate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["config.update"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    patch: ConfigPatch


class ModelSelect(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["model.select"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    provider: Literal["anthropic", "openai", "openrouter", "ollama"]
    model: Annotated[StrictStr, Field(max_length=120, min_length=1)]


class KeyReplace(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["key.replace"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    provider: Literal["anthropic", "openai", "openrouter", "ollama"]
    key: Annotated[StrictStr, Field(pattern="^[\\x21-\\x7e]{12,512}$")]


class CvUpload(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["cv.upload"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    name: Annotated[
        StrictStr,
        Field(
            max_length=120,
            min_length=1,
            pattern="\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$",
        ),
    ]
    size_bytes: Annotated[StrictInt, Field(ge=1, le=5242880)]
    content_b64: Annotated[
        StrictStr, Field(max_length=6990508, pattern="^[A-Za-z0-9+/]*={0,2}$")
    ]


class ProfileUpdate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["profile.update"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    summary: Annotated[StrictStr | None, Field(max_length=300)] = None
    education: Annotated[StrictStr | None, Field(max_length=300)] = None
    languages: Annotated[list[Term] | None, Field(max_length=40)] = None
    stack: Annotated[list[Term] | None, Field(max_length=40)] = None
    projects: Annotated[list[ProjectEdit] | None, Field(max_length=20)] = None


class GuidanceUpdate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["guidance.update"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    text: Annotated[StrictStr, Field(max_length=12000)]


class GuidanceReset(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["guidance.reset"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]


class ContextUpdate(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["context.update"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    about: Annotated[StrictStr, Field(max_length=4000)]


class ContextUpload(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["context.upload"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    name: Annotated[
        StrictStr,
        Field(
            max_length=120,
            min_length=1,
            pattern="\\.([pP][dD][fF]|[tT][xX][tT]|[mM][dD])$",
        ),
    ]
    content_b64: Annotated[
        StrictStr, Field(max_length=6990508, pattern="^[A-Za-z0-9+/]*={0,2}$")
    ]


class ContextRemove(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["context.remove"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    doc_id: Annotated[StrictStr, Field(max_length=64, pattern="^doc_[a-z0-9_]+$")]


class LearningForget(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["learning.forget"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    decision_id: Annotated[StrictInt, Field(ge=1, le=9007199254740991)]


class LearningForgetAll(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["learning.forget_all"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]


class Unpair(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["unpair"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]


class Chunk(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    type: Literal["chunk"]
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    chunk_id: Annotated[
        StrictStr,
        Field(pattern="^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    ]
    index: Annotated[StrictInt, Field(ge=0, le=255)]
    count: Annotated[StrictInt, Field(ge=1, le=256)]
    data: Annotated[
        StrictStr, Field(max_length=43692, pattern="^[A-Za-z0-9+/]*={0,2}$")
    ]


class Payload(
    RootModel[
        Status
        | AdsSnapshot
        | ApplicationsSnapshot
        | ConfigSnapshot
        | ProfileSnapshot
        | UsageSnapshot
        | ModelsSnapshot
        | GuidanceSnapshot
        | ContextSnapshot
        | LearningSnapshot
        | RunProgress
        | RunFinished
        | RunCancelled
        | Saved
        | ErrorMessage
        | Unpaired
        | RunStart
        | RunCancel
        | AdMark
        | AdUnmark
        | AdShowAnyway
        | ApplicationCreate
        | ApplicationUpdate
        | ApplicationRemove
        | ConfigUpdate
        | ModelSelect
        | KeyReplace
        | CvUpload
        | ProfileUpdate
        | GuidanceUpdate
        | GuidanceReset
        | ContextUpdate
        | ContextUpload
        | ContextRemove
        | LearningForget
        | LearningForgetAll
        | Unpair
        | Chunk
    ]
):
    root: Annotated[
        Status
        | AdsSnapshot
        | ApplicationsSnapshot
        | ConfigSnapshot
        | ProfileSnapshot
        | UsageSnapshot
        | ModelsSnapshot
        | GuidanceSnapshot
        | ContextSnapshot
        | LearningSnapshot
        | RunProgress
        | RunFinished
        | RunCancelled
        | Saved
        | ErrorMessage
        | Unpaired
        | RunStart
        | RunCancel
        | AdMark
        | AdUnmark
        | AdShowAnyway
        | ApplicationCreate
        | ApplicationUpdate
        | ApplicationRemove
        | ConfigUpdate
        | ModelSelect
        | KeyReplace
        | CvUpload
        | ProfileUpdate
        | GuidanceUpdate
        | GuidanceReset
        | ContextUpdate
        | ContextUpload
        | ContextRemove
        | LearningForget
        | LearningForgetAll
        | Unpair
        | Chunk,
        Field(discriminator="type", title="Payload"),
    ]
