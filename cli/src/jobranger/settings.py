from __future__ import annotations

import io
import itertools
import re
from pathlib import Path
from typing import TYPE_CHECKING, Literal

from pydantic import (
    BaseModel,
    ConfigDict,
    NonNegativeFloat,
    ValidationError,
    field_serializer,
    field_validator,
)
from ruamel.yaml import YAML
from ruamel.yaml.comments import CommentedMap, CommentedSeq

from jobranger import paths
from jobranger.catalogue import (
    EmployerFeed,
    Level,
    RoleId,
    YearBand,
    default_employer_feeds,
)
from jobranger.providers.base import ProviderName

if TYPE_CHECKING:
    from collections.abc import Mapping

DEFAULT_LEVELS: frozenset[Level] = frozenset({"junior", "mid", "none"})
DEFAULT_YEARS: frozenset[YearBand] = frozenset({"0-1", "1-3"})
DEFAULT_RUN_TIMES = ("03:00", "15:00")
MIN_RUN_GAP_MINUTES = 120
MINUTES_PER_HOUR = 60
MINUTES_PER_DAY = 24 * MINUTES_PER_HOUR
RUN_TIME_PATTERN = re.compile(r"^([01]\d|2[0-3]):([0-5]\d)$")
HOME_PREFIX = "~"
LOCATION_SEPARATOR = "."
DEFAULT_OLLAMA_URL = "http://localhost:11434"


class ConfigMissingError(Exception):
    def __init__(self, path: Path) -> None:
        super().__init__(f"file not found: {path}")
        self.path = path


class ConfigInvalidError(Exception):
    def __init__(self, path: Path, errors: list[str]) -> None:
        super().__init__(f"{path} is invalid: {'; '.join(errors)}")
        self.path = path
        self.errors = errors


class Limits(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    levels: frozenset[Level] = DEFAULT_LEVELS
    years: frozenset[YearBand] = DEFAULT_YEARS
    hide_masters: bool = True

    @field_serializer("levels", "years")
    def _sorted(self, values: frozenset[str]) -> list[str]:
        return sorted(values)


class Targets(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    subfields: tuple[RoleId, ...] = ()
    limits: Limits = Limits()
    places: tuple[str, ...] = ()
    relocate: bool = True

    @field_validator("places")
    @classmethod
    def _dedup_places(cls, places: tuple[str, ...]) -> tuple[str, ...]:
        kept: dict[str, str] = {}
        for place in places:
            stripped = place.strip()
            if stripped:
                kept.setdefault(stripped.casefold(), stripped)
        return tuple(kept.values())


class SourcesConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    nav: bool = True
    employers: bool = True
    finn: bool = False
    linkedin: bool = False
    employer_feeds: tuple[EmployerFeed, ...] = ()


class ScheduleConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    times: tuple[str, ...] = DEFAULT_RUN_TIMES

    @field_validator("times")
    @classmethod
    def _spaced(cls, times: tuple[str, ...]) -> tuple[str, ...]:
        minutes = sorted(_minutes(time) for time in times)
        gaps = [later - earlier for earlier, later in itertools.pairwise(minutes)]
        if len(minutes) > 1:
            gaps.append(minutes[0] + MINUTES_PER_DAY - minutes[-1])
        if any(gap < MIN_RUN_GAP_MINUTES for gap in gaps):
            msg = "run times must be at least 2 hours apart"
            raise ValueError(msg)
        return times


class PriceOverride(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    input_per_mtok: NonNegativeFloat
    output_per_mtok: NonNegativeFloat


class ModelConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    provider: ProviderName | None = None
    model: str | None = None
    ollama_url: str = DEFAULT_OLLAMA_URL
    monthly_limit_usd: NonNegativeFloat | None = None
    price: PriceOverride | None = None


class EmailConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    on: bool = False
    address: str | None = None


class NotifyConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    website: bool = True
    email: EmailConfig = EmailConfig()


class ProfileRef(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    path: Path
    cv_name: str | None = None
    cv_read_at: str | None = None

    @field_validator("path")
    @classmethod
    def _expand(cls, path: Path) -> Path:
        return path.expanduser()

    @field_serializer("path")
    def _with_home(self, path: Path) -> str:
        home = Path.home()
        if path.is_relative_to(home):
            return str(Path(HOME_PREFIX) / path.relative_to(home))
        return str(path)


class AppConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    version: Literal[1] = 1
    profile: ProfileRef
    targets: Targets = Targets()
    sources: SourcesConfig = SourcesConfig()
    schedule: ScheduleConfig = ScheduleConfig()
    model: ModelConfig = ModelConfig()
    notify: NotifyConfig = NotifyConfig()


def default_config() -> AppConfig:
    return AppConfig(
        profile=ProfileRef(path=paths.profile_path()),
        sources=SourcesConfig(employer_feeds=default_employer_feeds()),
    )


def load_config(path: Path) -> AppConfig:
    data = read_yaml(path)
    try:
        return AppConfig.model_validate(data)
    except ValidationError as error:
        raise ConfigInvalidError(path, validation_errors(error)) from error


def save_config(path: Path, config: AppConfig) -> None:
    node = _load_node(path)
    _merge_into(node, config.model_dump(mode="json"))
    write_yaml(path, node)


def read_yaml(path: Path) -> object:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError as error:
        raise ConfigMissingError(path) from error
    return YAML().load(text)


def write_yaml(path: Path, data: object) -> None:
    buffer = io.StringIO()
    YAML().dump(data, buffer)
    paths.write_private(path, buffer.getvalue())


def validation_errors(error: ValidationError) -> list[str]:
    return [
        f"{LOCATION_SEPARATOR.join(str(part) for part in item['loc'])}: {item['msg']}"
        for item in error.errors()
    ]


def _load_node(path: Path) -> CommentedMap:
    try:
        loaded = read_yaml(path)
    except ConfigMissingError:
        return CommentedMap()
    return loaded if isinstance(loaded, CommentedMap) else CommentedMap()


def _merge_into(node: CommentedMap, data: Mapping[str, object]) -> None:
    for key in [key for key in node if key not in data]:
        del node[key]
    for key, value in data.items():
        current = node.get(key)
        if isinstance(value, dict):
            if not isinstance(current, CommentedMap):
                current = CommentedMap()
                node[key] = current
            _merge_into(current, value)
        elif isinstance(value, list):
            node[key] = _sequence(value)
        elif key not in node or current != value:
            node[key] = value


def _sequence(values: list[object]) -> CommentedSeq:
    items: list[object] = []
    for value in values:
        if isinstance(value, dict):
            mapping = CommentedMap()
            _merge_into(mapping, value)
            items.append(mapping)
        else:
            items.append(value)
    sequence = CommentedSeq(items)
    if not any(isinstance(item, CommentedMap) for item in items):
        sequence.fa.set_flow_style()
    return sequence


def _minutes(time: str) -> int:
    matched = RUN_TIME_PATTERN.match(time)
    if matched is None:
        msg = f"run time {time!r} is not HH:MM"
        raise ValueError(msg)
    return int(matched.group(1)) * MINUTES_PER_HOUR + int(matched.group(2))
