from __future__ import annotations

import importlib
import re
import stat
import sys
from typing import TYPE_CHECKING

import pytest
from factories import INVALID_URL, OLLAMA_PASSWORD, OLLAMA_URL, traceback_text
from mail_fakes import SMTP_PASSWORD
from pydantic import BaseModel, ValidationError

from jobranger.catalogue import default_employer_feeds
from jobranger.notify.send import DEFAULT_RESEND_SENDER
from jobranger.settings import (
    DEFAULT_OLLAMA_URL,
    AppConfig,
    ConfigInvalidError,
    ConfigMissingError,
    EmailConfig,
    Limits,
    ModelConfig,
    NotifyConfig,
    PriceOverride,
    ProfileRef,
    ScheduleConfig,
    SmtpServer,
    SourcesConfig,
    Targets,
    default_config,
    load_config,
    save_config,
    smtp_server,
)

if TYPE_CHECKING:
    from collections.abc import Iterator
    from pathlib import Path

PROVIDER_SDKS = frozenset({"openai", "anthropic"})
OWNER_READ_WRITE = 0o600
OWNER_ONLY_DIR = 0o700
COMMENT = "# chosen during setup"
LONG_LABEL = "a" * 64
CREDENTIAL_SMTP_URLS = [
    f"smtp://user:{SMTP_PASSWORD}@smtp.example.test",
    f"smtp://user:Qa/{SMTP_PASSWORD}#x@smtp.example.test:587",
    f"smtp://user:Qa?{SMTP_PASSWORD}@smtp.example.test",
    f"smtp:/user:{SMTP_PASSWORD}@smtp.example.test",
    f"smtps://user:Qa/{SMTP_PASSWORD}@smtp.example.test",
    f"SMTP://user:Qa/{SMTP_PASSWORD}@smtp.example.test",
]
CREDENTIAL_SMTP_IDS = [
    "plain",
    "slash and hash",
    "question mark",
    "single slash",
    "smtps",
    "upper-case scheme",
]


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


def test_should_reject_run_time_not_in_hh_mm_format() -> None:
    with pytest.raises(ValidationError, match="is not HH:MM"):
        ScheduleConfig(times=("25:00",))


@pytest.mark.parametrize(
    "fields",
    [
        {"monthly_limit_usd": -1},
        {"price": {"input_per_mtok": -1, "output_per_mtok": 10}},
        {"price": {"input_per_mtok": 2, "output_per_mtok": -1}},
    ],
)
def test_should_reject_negative_model_amount(fields: dict[str, object]) -> None:
    with pytest.raises(ValidationError, match="greater than or equal to 0"):
        ModelConfig.model_validate(fields)


@pytest.mark.parametrize("value", [".nan", "1e303"])
@pytest.mark.parametrize(
    ("block", "location"),
    [
        ("  monthly_limit_usd: {value}\n", "model.monthly_limit_usd"),
        (
            "  price:\n    input_per_mtok: {value}\n    output_per_mtok: 1\n",
            "model.price.input_per_mtok",
        ),
        (
            "  price:\n    input_per_mtok: 1\n    output_per_mtok: {value}\n",
            "model.price.output_per_mtok",
        ),
    ],
)
def test_should_reject_non_finite_or_huge_model_amount(
    config_path: Path, value: str, block: str, location: str
) -> None:
    key = block.split(":", maxsplit=1)[0]
    text = config_path.read_text().replace(f"{key}:\n", block.format(value=value))
    config_path.write_text(text)

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert any(error.startswith(location) for error in raised.value.errors)


def test_should_reject_unknown_config_key(config_path: Path) -> None:
    config_path.write_text(config_path.read_text() + "unknown_key: 1\n")

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert raised.value.path == config_path


def test_should_join_every_error_in_invalid_config_message(config_path: Path) -> None:
    config_path.write_text(config_path.read_text() + "unknown_key: 1\nother_key: 2\n")

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    errors = "; ".join(raised.value.errors)
    assert str(raised.value) == f"{config_path} is invalid: {errors}"


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


@pytest.mark.parametrize("url", ["ftp://box:11434", "http://", INVALID_URL])
def test_should_reject_invalid_ollama_url(config_path: Path, url: str) -> None:
    text = config_path.read_text().replace(DEFAULT_OLLAMA_URL, url)
    config_path.write_text(text)

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert any(error.startswith("model.ollama_url") for error in raised.value.errors)


def test_should_hide_ollama_password_when_config_fails_to_load(
    config_path: Path,
) -> None:
    url = f"http://user:{OLLAMA_PASSWORD}@[::1"
    config_path.write_text(config_path.read_text().replace(DEFAULT_OLLAMA_URL, url))

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert OLLAMA_PASSWORD not in traceback_text(raised.value)


@pytest.mark.parametrize("url", CREDENTIAL_SMTP_URLS, ids=CREDENTIAL_SMTP_IDS)
def test_should_hide_smtp_password_when_config_fails_to_load(
    config_path: Path, url: str
) -> None:
    config_path.write_text(
        config_path.read_text().replace("    smtp_url:\n", f'    smtp_url: "{url}"\n')
    )

    with pytest.raises(ConfigInvalidError) as raised:
        load_config(config_path)

    assert SMTP_PASSWORD not in traceback_text(raised.value)


@pytest.mark.parametrize("url", CREDENTIAL_SMTP_URLS, ids=CREDENTIAL_SMTP_IDS)
def test_should_hide_smtp_password_in_validation_error(url: str) -> None:
    with pytest.raises(ValidationError) as raised:
        EmailConfig(smtp_url=url)

    assert SMTP_PASSWORD not in str(raised.value)


@pytest.mark.parametrize(
    "model",
    [
        Limits,
        Targets,
        SourcesConfig,
        ScheduleConfig,
        PriceOverride,
        ModelConfig,
        EmailConfig,
        NotifyConfig,
        ProfileRef,
        AppConfig,
    ],
)
def test_should_leave_input_out_of_validation_error(model: type[BaseModel]) -> None:
    with pytest.raises(ValidationError) as raised:
        model.model_validate({"unknown": SMTP_PASSWORD})

    assert SMTP_PASSWORD not in str(raised.value)


def test_should_hide_ollama_password_in_validation_error() -> None:
    with pytest.raises(ValidationError) as raised:
        ModelConfig(ollama_url=f"http://user:Qa/{OLLAMA_PASSWORD}#x@[::1")

    assert OLLAMA_PASSWORD not in str(raised.value)


@pytest.mark.parametrize(
    ("model", "field", "url", "message"),
    [
        (EmailConfig, "smtp_url", "http://box", "smtp_url is not smtp://host"),
        (ModelConfig, "ollama_url", "ftp://box", "ollama_url is not an http"),
    ],
    ids=["smtp_url", "ollama_url"],
)
def test_should_name_field_without_its_url_in_error(
    model: type[BaseModel], field: str, url: str, message: str
) -> None:
    with pytest.raises(ValidationError, match=message) as raised:
        model.model_validate({field: url})

    assert url not in str(raised.value)


@pytest.mark.parametrize(
    "url", [f"{OLLAMA_URL}/v1", f"{OLLAMA_URL}/v1/", f"{OLLAMA_URL}//v1"]
)
def test_should_store_ollama_url_without_api_path(url: str) -> None:
    config = ModelConfig(ollama_url=url)

    assert config.ollama_url == OLLAMA_URL


def test_should_accept_https_ollama_url() -> None:
    config = ModelConfig(ollama_url="https://box:11434")

    assert config.ollama_url == "https://box:11434"


def test_should_name_missing_config_path(tmp_path: Path) -> None:
    missing = tmp_path / "absent.yaml"

    with pytest.raises(ConfigMissingError) as raised:
        load_config(missing)

    assert (raised.value.path, str(raised.value)) == (
        missing,
        f"file not found: {missing}",
    )


def test_should_create_private_parent_dirs_when_saving(tmp_path: Path) -> None:
    path = tmp_path / "nested" / "jobranger" / "config.yaml"

    save_config(path, default_config())

    assert stat.S_IMODE(path.parent.stat().st_mode) == OWNER_ONLY_DIR


@pytest.mark.parametrize(
    "line", ["levels: [junior, mid, none]", "times: [03:00, 15:00]"]
)
def test_should_write_scalar_lists_inline(config_path: Path, line: str) -> None:
    assert line in config_path.read_text()


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


@pytest.fixture
def isolated_modules() -> Iterator[None]:
    saved = dict(sys.modules)
    for name in list(sys.modules):
        if _root(name) in PROVIDER_SDKS | {"jobranger"}:
            del sys.modules[name]
    yield
    sys.modules.clear()
    sys.modules.update(saved)


def _root(module_name: str) -> str:
    return module_name.partition(".")[0]


@pytest.mark.usefixtures("isolated_modules")
def test_should_load_settings_without_provider_sdks() -> None:
    importlib.import_module("jobranger.settings")

    assert {_root(name) for name in sys.modules} & PROVIDER_SDKS == set()


@pytest.mark.parametrize(
    ("url", "server"),
    [
        ("smtp://smtp.example.test", SmtpServer("smtp.example.test", 587, False)),
        ("smtps://smtp.example.test", SmtpServer("smtp.example.test", 465, True)),
        ("smtp://localhost:2525", SmtpServer("localhost", 2525, False)),
    ],
    ids=["smtp", "smtps", "explicit port"],
)
def test_should_read_smtp_server_from_url(url: str, server: SmtpServer) -> None:
    assert smtp_server(url) == server


@pytest.mark.parametrize(
    "url",
    [
        "http://smtp.example.test",
        "smtp://",
        "smtp://user:secret@smtp.example.test",
        "smtp://smtp.example.test/inbox",
        "smtp://smtp.example.test?tls=off",
        "smtp://smtp.example.test#top",
        "smtp://smtp.example.test:99999",
    ],
    ids=["http", "no host", "password", "path", "query", "fragment", "bad port"],
)
def test_should_reject_invalid_smtp_url(url: str) -> None:
    with pytest.raises(ValidationError, match="smtp_url"):
        EmailConfig(smtp_url=url)


def test_should_accept_sender_with_display_name() -> None:
    email = EmailConfig(sender=DEFAULT_RESEND_SENDER)

    assert email.sender == DEFAULT_RESEND_SENDER


def test_should_require_address_when_email_is_on() -> None:
    with pytest.raises(ValidationError, match="email address"):
        EmailConfig(on=True, via="resend")


def test_should_require_smtp_server_when_email_goes_through_smtp() -> None:
    with pytest.raises(ValidationError, match="SMTP server"):
        EmailConfig(on=True, address="user@example.com", via="smtp")


def test_should_accept_resend_email_without_smtp_server() -> None:
    email = EmailConfig(on=True, address="user@example.com", via="resend")

    assert email.smtp_url is None


@pytest.mark.parametrize(
    ("field", "value", "message"),
    [
        ("address", "user@", "address 'user@' is not one email address"),
        ("sender", "a@b.test, c@d.test", "sender 'a@b.test, c@d.test' is not one"),
    ],
    ids=["address", "sender"],
)
def test_should_name_rejected_value_in_email_error(
    field: str, value: str, message: str
) -> None:
    with pytest.raises(ValidationError, match=re.escape(message)):
        EmailConfig.model_validate({field: value})


@pytest.mark.parametrize(
    "url",
    [
        "smtp://smtp..example.test:587",
        "smtp://.:587",
        "smtp://exa mple.test:587",
    ],
    ids=["empty label", "dot only", "space"],
)
def test_should_reject_smtp_host_that_is_not_a_host_name(url: str) -> None:
    with pytest.raises(ValidationError, match="smtp_url"):
        EmailConfig(smtp_url=url)


def test_should_connect_to_ascii_form_of_international_smtp_host() -> None:
    server = smtp_server("smtp://smtp.bø.example")

    assert server.host == "smtp.xn--b-5ga.example"


@pytest.mark.parametrize(
    "address",
    [
        "user@example.com\nBcc: other@example.com",
        "user@",
        "usér@example.com",
        f"user@{LONG_LABEL}.example.test",
    ],
    ids=["line break", "no domain", "non-ascii local part", "long domain label"],
)
def test_should_reject_email_address_when_it_is_not_one_address(address: str) -> None:
    with pytest.raises(ValidationError, match="address .* is not one email address"):
        EmailConfig(address=address)


@pytest.mark.parametrize(
    "sender",
    [
        " jobranger <user@example.com>",
        "jobranger\t<user@example.com>",
        "jobranger <a@b.test>\nBcc: c@d.test",
        "a@b.test,c@d.test",
        "user@",
        "jobranger <usér@example.com>",
        f"jobranger <user@{LONG_LABEL}.example.test>",
    ],
    ids=[
        "leading space",
        "inner tab",
        "line break",
        "two addresses",
        "no domain",
        "non-ascii local part",
        "long domain label",
    ],
)
def test_should_reject_sender_when_it_is_not_one_address(sender: str) -> None:
    with pytest.raises(ValidationError, match="sender .* is not one email address"):
        EmailConfig(sender=sender)


def test_should_accept_address_with_international_domain() -> None:
    email = EmailConfig(address="user@bø.example")

    assert email.address == "user@bø.example"


def test_should_drop_trailing_dot_from_smtp_host() -> None:
    server = smtp_server("smtp://smtp.example.test.:587")

    assert server.host == "smtp.example.test"


@pytest.mark.parametrize(
    "url", ["smtp://a..:587", "smtp://smtp.example.test..:587"], ids=["short", "long"]
)
def test_should_reject_smtp_host_ending_in_two_dots(url: str) -> None:
    with pytest.raises(ValidationError, match="smtp_url"):
        EmailConfig(smtp_url=url)
