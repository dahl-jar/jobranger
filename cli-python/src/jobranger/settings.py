from __future__ import annotations

import io
import itertools
import re
from dataclasses import dataclass
from email.errors import HeaderParseError
from email.headerregistry import Address
from email.message import EmailMessage
from pathlib import Path
from typing import TYPE_CHECKING, Annotated, Literal, Self
from urllib.parse import SplitResult, urlsplit

import httpx
from pydantic import (
    BaseModel,
    ConfigDict,
    Field,
    ValidationError,
    field_serializer,
    field_validator,
    model_validator,
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
from jobranger.providers.urls import ollama_root

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
OLLAMA_URL_SCHEMES = frozenset({"http", "https"})
INVALID_OLLAMA_URL = "ollama_url {url!r} is not an http or https URL with a host"
SMTP_DEFAULT_PORTS = {"smtp": 587, "smtps": 465}
IMPLICIT_TLS_SCHEME = "smtps"
ROOT_LABEL = "."
EMPTY_URL_PATHS = frozenset({"", "/"})
INVALID_SMTP_URL = (
    "smtp_url is not smtp://host[:port] or smtps://host[:port]"
    " without a user, path or query"
)
INVALID_OLLAMA_URL_FIELD = "ollama_url is not an http or https URL with a host"
INVALID_ADDRESS = "address {address!r} is not one email address"
INVALID_SENDER = "sender {sender!r} is not one email address"
MISSING_ADDRESS = "email is on but no email address is set"
MISSING_SMTP_URL = "email goes through SMTP but no SMTP server (smtp_url) is set"

type EmailVia = Literal["smtp", "resend"]


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
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    levels: frozenset[Level] = DEFAULT_LEVELS
    years: frozenset[YearBand] = DEFAULT_YEARS
    hide_masters: bool = True

    @field_serializer("levels", "years")
    def _sorted(self, values: frozenset[str]) -> list[str]:
        return sorted(values)


class Targets(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

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
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    nav: bool = True
    employers: bool = True
    finn: bool = False
    linkedin: bool = False
    employer_feeds: tuple[EmployerFeed, ...] = ()


class ScheduleConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

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


MAX_AMOUNT_USD = 1_000_000
Amount = Annotated[float, Field(ge=0, le=MAX_AMOUNT_USD, allow_inf_nan=False)]


class PriceOverride(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    input_per_mtok: Amount
    output_per_mtok: Amount


class ModelConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    provider: ProviderName | None = None
    model: str | None = None
    ollama_url: str = DEFAULT_OLLAMA_URL
    monthly_limit_usd: Amount | None = None
    price: PriceOverride | None = None

    @field_validator("ollama_url")
    @classmethod
    def _valid_ollama_url(cls, url: str) -> str:
        root = ollama_root(url)
        if not is_ollama_url(root):
            raise ValueError(INVALID_OLLAMA_URL_FIELD)
        return root


@dataclass(frozen=True)
class SmtpServer:
    host: str
    port: int
    implicit_tls: bool


class EmailConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    on: bool = False
    address: str | None = None
    via: EmailVia = "smtp"
    smtp_url: str | None = None
    smtp_user: str | None = None
    sender: str | None = None

    @field_validator("address")
    @classmethod
    def _one_address(cls, address: str | None) -> str | None:
        if address is not None and not is_email_address(address):
            raise ValueError(INVALID_ADDRESS.format(address=address))
        return address

    @field_validator("sender")
    @classmethod
    def _one_sender(cls, sender: str | None) -> str | None:
        if sender is not None and not is_mailbox(sender):
            raise ValueError(INVALID_SENDER.format(sender=sender))
        return sender

    @field_validator("smtp_url")
    @classmethod
    def _valid_smtp_url(cls, url: str | None) -> str | None:
        if url is not None:
            smtp_server(url)
        return url

    @model_validator(mode="after")
    def _complete_when_on(self) -> Self:
        if self.on and self.address is None:
            raise ValueError(MISSING_ADDRESS)
        if self.on and self.via == "smtp" and self.smtp_url is None:
            raise ValueError(MISSING_SMTP_URL)
        return self


class NotifyConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

    website: bool = True
    email: EmailConfig = EmailConfig()


class ProfileRef(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

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
    model_config = ConfigDict(extra="forbid", frozen=True, hide_input_in_errors=True)

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
        raise ConfigInvalidError(path, validation_errors(error)) from None


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


def is_ollama_url(url: str) -> bool:
    try:
        parsed = httpx.URL(url)
    except httpx.InvalidURL:
        return False
    return parsed.scheme in OLLAMA_URL_SCHEMES and bool(parsed.host)


def _minutes(time: str) -> int:
    matched = RUN_TIME_PATTERN.match(time)
    if matched is None:
        msg = f"run time {time!r} is not HH:MM"
        raise ValueError(msg)
    return int(matched.group(1)) * MINUTES_PER_HOUR + int(matched.group(2))


def smtp_server(url: str) -> SmtpServer:
    invalid = ValueError(INVALID_SMTP_URL)
    try:
        parts = urlsplit(url)
        host = parts.hostname
        port = parts.port
    except ValueError as error:
        raise invalid from error
    if not host or not _bare_server_url(parts):
        raise invalid
    try:
        server_host = _ascii_host(host)
    except ValueError as error:
        raise invalid from error
    default_port = SMTP_DEFAULT_PORTS[parts.scheme]
    return SmtpServer(
        server_host,
        default_port if port is None else port,
        parts.scheme == IMPLICIT_TLS_SCHEME,
    )


def _bare_server_url(parts: SplitResult) -> bool:
    return (
        parts.scheme in SMTP_DEFAULT_PORTS
        and "@" not in parts.netloc
        and parts.path in EMPTY_URL_PATHS
        and not parts.query
        and not parts.fragment
    )


def _ascii_host(host: str) -> str:
    converted = ascii_domain(host.removesuffix(ROOT_LABEL))
    if not converted or converted.endswith(ROOT_LABEL):
        raise ValueError
    return converted


def ascii_domain(domain: str) -> str:
    if any(char.isspace() or not char.isprintable() for char in domain):
        raise ValueError
    return domain.encode("idna").decode("ascii")


def ascii_address(address: str) -> str:
    if any(char.isspace() for char in address):
        raise ValueError
    parsed = Address(addr_spec=address)
    return str(Address(username=parsed.username, domain=ascii_domain(parsed.domain)))


def ascii_mailbox(value: str) -> str:
    if value != value.strip() or "\t" in value:
        raise ValueError
    message = EmailMessage()
    message["From"] = value
    header = message["From"]
    if len(header.addresses) != 1 or header.defects:
        raise ValueError
    mailbox = header.addresses[0]
    return str(
        Address(
            display_name=mailbox.display_name,
            username=mailbox.username,
            domain=ascii_domain(mailbox.domain),
        )
    )


def is_email_address(address: str) -> bool:
    try:
        ascii_address(address)
    except (ValueError, HeaderParseError):
        return False
    return True


def is_mailbox(value: str) -> bool:
    try:
        ascii_mailbox(value)
    except (ValueError, HeaderParseError):
        return False
    return True
