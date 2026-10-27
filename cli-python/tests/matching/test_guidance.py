from __future__ import annotations

import stat
from typing import TYPE_CHECKING

import pytest
from factories import GUIDANCE

from jobranger.contract.limits import GUIDANCE_LIMIT
from jobranger.matching.guidance import (
    BYTE_ORDER_MARK,
    Guidance,
    load_guidance,
    reset_guidance,
    save_guidance,
)
from jobranger.settings import ConfigInvalidError

if TYPE_CHECKING:
    from pathlib import Path

BLANK = Guidance(text="")


@pytest.fixture
def path(tmp_path: Path) -> Path:
    return tmp_path / "config" / "guidance.md"


def test_should_return_default_when_guidance_file_missing(path: Path) -> None:
    guidance = load_guidance(path)

    assert (guidance, guidance.is_default) == (BLANK, True)


def test_should_load_saved_guidance_as_edited(path: Path) -> None:
    save_guidance(path, f"\n{GUIDANCE}\n\n")

    guidance = load_guidance(path)

    assert (guidance, guidance.is_default) == (Guidance(text=GUIDANCE), False)


@pytest.mark.parametrize(
    "text",
    [" \n\t ", BYTE_ORDER_MARK, f"{BYTE_ORDER_MARK}{BYTE_ORDER_MARK} \r\n"],
    ids=["whitespace", "bom", "double bom"],
)
def test_should_empty_guidance_file_when_saved_text_is_blank(
    path: Path, text: str
) -> None:
    save_guidance(path, GUIDANCE)

    saved = save_guidance(path, text)

    assert (saved, load_guidance(path), path.read_text()) == (BLANK, BLANK, "")


def test_should_keep_saved_guidance_when_new_text_is_over_limit(path: Path) -> None:
    save_guidance(path, GUIDANCE)

    with pytest.raises(ConfigInvalidError):
        save_guidance(path, "x" * (GUIDANCE_LIMIT + 1))

    assert load_guidance(path) == Guidance(text=GUIDANCE)


def test_should_name_length_and_limit_when_saved_text_is_over_limit(
    path: Path,
) -> None:
    with pytest.raises(ConfigInvalidError) as error:
        save_guidance(path, "x" * (GUIDANCE_LIMIT + 1))

    assert (error.value.path, error.value.errors) == (
        path,
        ["guidance has 12001 characters, the limit is 12000"],
    )


def test_should_reject_guidance_file_over_limit(path: Path) -> None:
    path.parent.mkdir(parents=True)
    path.write_text("x" * (GUIDANCE_LIMIT + 1))

    with pytest.raises(ConfigInvalidError) as error:
        load_guidance(path)

    assert error.value.path == path


def test_should_accept_guidance_at_limit(path: Path) -> None:
    text = "x" * GUIDANCE_LIMIT

    save_guidance(path, text)

    assert load_guidance(path).text == text


def test_should_report_default_when_file_is_blank(path: Path) -> None:
    path.parent.mkdir(parents=True)
    path.write_text(f"{BYTE_ORDER_MARK}\n  \r\n")

    assert load_guidance(path) == BLANK


@pytest.mark.parametrize(
    ("prefix", "newline"),
    [(BYTE_ORDER_MARK, "\r\n"), (BYTE_ORDER_MARK * 2, "\r")],
    ids=["bom crlf", "double bom cr"],
)
def test_should_save_guidance_with_plain_line_breaks(
    path: Path, prefix: str, newline: str
) -> None:
    saved = save_guidance(path, f"{prefix}{GUIDANCE.replace('\n', newline)}")

    assert (saved, load_guidance(path)) == (
        Guidance(text=GUIDANCE),
        Guidance(text=GUIDANCE),
    )


@pytest.mark.parametrize(
    "writer",
    [
        lambda target: target.write_bytes(b"Treat \xff\xfe roles"),
        lambda target: target.mkdir(),
    ],
    ids=["non-utf8", "directory"],
)
def test_should_reject_unreadable_guidance_file(path: Path, writer) -> None:
    path.parent.mkdir(parents=True)
    writer(path)

    with pytest.raises(ConfigInvalidError) as error:
        load_guidance(path)

    assert error.value.path == path


def test_should_name_decode_error_when_guidance_file_is_not_utf8(path: Path) -> None:
    path.parent.mkdir(parents=True)
    path.write_bytes(b"Treat \xff roles")

    with pytest.raises(ConfigInvalidError) as error:
        load_guidance(path)

    assert error.value.errors == [
        "guidance could not be read as UTF-8 text: 'utf-8' codec can't decode "
        "byte 0xff in position 6: invalid start byte"
    ]


@pytest.mark.parametrize(
    "write",
    [lambda target: save_guidance(target, GUIDANCE), reset_guidance],
    ids=["save", "reset"],
)
def test_should_write_guidance_file_private(path: Path, write) -> None:
    path.parent.mkdir(parents=True)

    write(path)

    assert stat.S_IMODE(path.stat().st_mode) == 0o600


def test_should_return_default_after_reset(path: Path) -> None:
    save_guidance(path, GUIDANCE)

    reset_guidance(path)

    assert (load_guidance(path), path.read_text()) == (BLANK, "")
