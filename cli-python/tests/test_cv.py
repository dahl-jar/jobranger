from __future__ import annotations

import codecs
import io
import json
import os
import struct
import subprocess
import tempfile
import threading
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

import fake_cv_workers
import pytest
from cv_samples import (
    CV_FIXTURES,
    CV_PDF,
    LONG_LINE,
    PDF_TEXT_LINE,
    document,
    docx,
    flate_stream,
    paragraph,
    pdf,
    pdf_with_text,
)
from pypdf import PdfWriter

from jobranger import cv

if TYPE_CHECKING:
    from collections.abc import Callable, Sequence

CV_MARKDOWN = CV_FIXTURES / "cv_user.md"
CV_LONE_SURROGATE_PDF = CV_FIXTURES / "cv_user_lone_surrogate.pdf"
SURROGATE_PDF_TEXT = (
    "Backend developer Python Kafka Kubernetes PostgreSQL experience at Employer One AS"
    * 5
    + "\n"
)
PDF_PASSWORD = "user-password"
PADDING_MEMBER = "word/media/padding.bin"
BLOCKED_READ_TIMEOUT_SECONDS = 2
TEST_DEADLINE_SECONDS = 1
DEADLINE_GRACE_SECONDS = 4
STREAM_LIMIT_WAIT_SECONDS = cv.EXTRACT_DEADLINE_SECONDS - DEADLINE_GRACE_SECONDS
NORWEGIAN_LINE = "Utvikler i Bergen og Ålesund. Ærlig, øvet og våken. " * 6
ACCIDENTAL_PAIR_LINE = NORWEGIAN_LINE + "«SKOLEN PÅ»"
EQUAL_COUNTS_BYTES = "å".encode() + b"\x7f\xff"
EQUAL_COUNTS_TEXT = "å".encode().decode("cp1252") + "ÿ"
WRITABLE_DIR_VARIABLES = ("HOME", "TMPDIR", "TEMP", "TMP")
CENTRAL_HEADER = b"PK\x01\x02"
LYING_SIZE = 1000
CONCURRENT_UPLOADS = 2
OVERLAP_WAIT_SECONDS = 0.5
ESCAPED_CONTROL = "\x01"
ESCAPED_CONTROL_BYTES = len(json.dumps(ESCAPED_CONTROL)) - 2


def _plain_stream(content: bytes) -> bytes:
    return b"<< /Length %d >>\nstream\n%b\nendstream" % (len(content), content)


def _encrypted_pdf() -> bytes:
    writer = PdfWriter(clone_from=CV_PDF)
    writer.encrypt(user_password=PDF_PASSWORD, algorithm="AES-256")
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()


def _pdf_padded_to(size: int) -> bytes:
    data = CV_PDF.read_bytes()
    pointer = data.rindex(b"startxref")
    padding = b"%" + b"0" * (size - len(data) - 2) + b"\n"
    return data[:pointer] + padding + data[pointer:]


def _docx_with_stack_table() -> bytes:
    cells = "".join(
        f"<w:tc>{paragraph(name)}</w:tc>" for name in ("Python", "Java", "TypeScript")
    )
    return docx(document(paragraph(LONG_LINE) + f"<w:tbl><w:tr>{cells}</w:tr></w:tbl>"))


def _with_stored_member(docx_bytes: bytes, padding: bytes) -> bytes:
    source = zipfile.ZipFile(io.BytesIO(docx_bytes))
    out = io.BytesIO()
    with zipfile.ZipFile(out, "w") as archive:
        for info in source.infolist():
            archive.writestr(info, source.read(info))
        archive.writestr(PADDING_MEMBER, padding)
    return out.getvalue()


def _docx_padded_to(size: int) -> bytes:
    base = _docx_with_stack_table()
    unpadded = len(_with_stored_member(base, b""))
    return _with_stored_member(base, bytes(size - unpadded))


def _docx_lying_about_size() -> bytes:
    data = bytearray(docx(document(paragraph(LONG_LINE) + "<w:p/>" * 3_000_000)))
    small = struct.pack("<I", LYING_SIZE)
    position = data.find(CENTRAL_HEADER)
    data[position + 24 : position + 28] = small
    data[22:26] = small
    return bytes(data)


def _docx_table_with_cells(rows: int, columns: int) -> bytes:
    row = "<w:tr>" + f"<w:tc>{paragraph('c')}</w:tc>" * columns + "</w:tr>"
    return docx(document(paragraph(LONG_LINE) + "<w:tbl>" + row * rows + "</w:tbl>"))


def _worker_reply_of_size(size: int) -> bytes:
    extra = size - len(json.dumps({"text": LONG_LINE}))
    controls, letters = divmod(extra, ESCAPED_CONTROL_BYTES)
    text = LONG_LINE + ESCAPED_CONTROL * controls + "x" * letters
    return json.dumps({"text": text}).encode()


def _reason(read: Callable[[], str]) -> str | None:
    try:
        read()
    except cv.CvUnreadableError as error:
        return error.reason
    return None


def _reason_within(
    seconds: float, read: Callable[[], str], release: Callable[[], None] | None = None
) -> str | None:
    reasons: list[str | None] = []
    worker = threading.Thread(target=lambda: reasons.append(_reason(read)), daemon=True)
    worker.start()
    worker.join(seconds)
    if worker.is_alive():
        if release is not None:
            release()
            worker.join()
        pytest.fail(f"extraction still running after {seconds} s")
    return reasons[0]


def _release_fifo(path: Path) -> Callable[[], None]:
    return lambda: os.close(os.open(path, os.O_WRONLY | os.O_NONBLOCK))


def test_should_extract_text_from_pdf_with_text_layer() -> None:
    text = cv.extract_text(CV_PDF)

    assert "Employer One AS" in text
    assert "java_project" in text


def test_should_raise_no_text_when_pdf_is_a_scan() -> None:
    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.extract_text(CV_FIXTURES / "cv_user_scan.pdf")

    assert raised.value.reason == "no_text"


def test_should_put_each_table_cell_on_its_own_line(tmp_path: Path) -> None:
    path = tmp_path / "cv.docx"
    path.write_bytes(_docx_with_stack_table())

    text = cv.extract_text(path)

    assert "Python\nJava\nTypeScript" in text


def test_should_reject_doc_suffix(tmp_path: Path) -> None:
    path = tmp_path / "cv.doc"
    path.write_text(LONG_LINE)

    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.extract_text(path)

    assert raised.value.reason == "type"
    assert str(raised.value) == "cv file unreadable: type"


def test_should_read_suffix_in_any_case(tmp_path: Path) -> None:
    path = tmp_path / "CV.MD"
    path.write_bytes(CV_MARKDOWN.read_bytes())

    text = cv.extract_text(path)

    assert "Employer One AS" in text


@pytest.mark.parametrize(
    ("data", "expected"),
    [
        pytest.param(NORWEGIAN_LINE.encode(), NORWEGIAN_LINE, id="utf-8"),
        pytest.param(
            codecs.BOM_UTF8 + NORWEGIAN_LINE.encode(),
            NORWEGIAN_LINE,
            id="utf-8 with bom",
        ),
        pytest.param(
            codecs.BOM_UTF16_LE + NORWEGIAN_LINE.encode("utf-16-le"),
            NORWEGIAN_LINE,
            id="utf-16 le",
        ),
        pytest.param(
            codecs.BOM_UTF16_BE + NORWEGIAN_LINE.encode("utf-16-be"),
            NORWEGIAN_LINE,
            id="utf-16 be",
        ),
        pytest.param(NORWEGIAN_LINE.encode("cp1252"), NORWEGIAN_LINE, id="cp1252"),
        pytest.param(
            codecs.BOM_UTF8 + NORWEGIAN_LINE.encode("cp1252"),
            NORWEGIAN_LINE,
            id="cp1252 with utf-8 bom",
        ),
        pytest.param(
            ACCIDENTAL_PAIR_LINE.encode("cp1252"),
            ACCIDENTAL_PAIR_LINE,
            id="cp1252 with an accidental utf-8 pair",
        ),
        pytest.param(
            NORWEGIAN_LINE.encode("cp1252") + b"\x7f",
            NORWEGIAN_LINE,
            id="cp1252 with delete character",
        ),
        pytest.param(
            NORWEGIAN_LINE.encode() + b"\xff",
            NORWEGIAN_LINE,
            id="utf-8 with one bad byte",
        ),
        pytest.param(
            NORWEGIAN_LINE.encode() + "Å".encode()[:1],
            NORWEGIAN_LINE,
            id="utf-8 cut mid character",
        ),
        pytest.param(
            LONG_LINE.encode() + EQUAL_COUNTS_BYTES,
            LONG_LINE + EQUAL_COUNTS_TEXT,
            id="as many bad bytes as utf-8 characters",
        ),
    ],
)
def test_should_decode_text_file(tmp_path: Path, data: bytes, expected: str) -> None:
    path = tmp_path / "cv.txt"
    path.write_bytes(data)

    assert cv.extract_text(path) == expected


@pytest.mark.parametrize("undefined", [0x81, 0x8D, 0x8F, 0x90, 0x9D])
def test_should_drop_undefined_cp1252_byte(tmp_path: Path, undefined: int) -> None:
    path = tmp_path / "cv.txt"
    path.write_bytes(LONG_LINE.encode() + bytes([undefined]))

    assert cv.extract_text(path) == LONG_LINE


@pytest.mark.parametrize(
    "joined",
    [
        pytest.param("\U0001f468\u200d\U0001f4bb", id="emoji zwj sequence"),
        pytest.param("\U0001f3f3\ufe0f\u200d\U0001f308", id="rainbow flag"),
        pytest.param("می\u200cخواهم", id="persian zwnj"),
    ],
)
def test_should_keep_joiners(tmp_path: Path, joined: str) -> None:
    path = tmp_path / "cv.txt"
    path.write_bytes(f"{LONG_LINE} {joined}".encode())

    assert cv.extract_text(path) == f"{LONG_LINE} {joined}"


@pytest.mark.parametrize(
    "unsafe",
    [
        pytest.param(
            "".join(chr(code) for code in range(1, 0x20) if chr(code) not in "\t\n\r"),
            id="c0 controls",
        ),
        pytest.param("".join(map(chr, range(0x7F, 0xA0))), id="del and c1 controls"),
        pytest.param(chr(0x061C), id="arabic letter mark"),
        pytest.param(chr(0x200E) + chr(0x200F), id="direction marks"),
        pytest.param("".join(map(chr, range(0x202A, 0x202F))), id="bidi embeddings"),
        pytest.param("".join(map(chr, range(0x2066, 0x206A))), id="bidi isolates"),
        pytest.param(
            chr(0xE0001) + "".join(map(chr, range(0xE0020, 0xE0080))),
            id="tag characters",
        ),
        pytest.param(
            "".join(map(chr, (0x200B, 0x2060, 0xFEFF))),
            id="zero-width characters",
        ),
        pytest.param(
            "".join(map(chr, (0xAD, 0x180E, 0xFFF9, 0xFFFA, 0xFFFB))),
            id="other format characters",
        ),
    ],
)
def test_should_remove_unsafe_characters(tmp_path: Path, unsafe: str) -> None:
    path = tmp_path / "cv.txt"
    path.write_bytes(f"{LONG_LINE}\t{unsafe}\nend".encode())

    text = cv.extract_text(path)

    assert text == f"{LONG_LINE}\t\nend"


def test_should_drop_lone_surrogate_from_pdf_text() -> None:
    text = cv.extract_text(CV_LONE_SURROGATE_PDF)

    assert text == SURROGATE_PDF_TEXT


@pytest.mark.parametrize(
    ("content", "readable"),
    [("x" * 199 + " \n\t" * 100, False), ("x" * 200, True)],
)
def test_should_need_200_non_space_characters(
    tmp_path: Path, content: str, readable: bool
) -> None:
    path = tmp_path / "cv.txt"
    path.write_text(content)

    assert _reason(lambda: cv.extract_text(path)) == (None if readable else "no_text")


@pytest.mark.parametrize(
    ("size", "readable"),
    [(5_242_880, True), (5_242_881, False)],
)
def test_should_reject_file_over_5_mb(
    tmp_path: Path, size: int, readable: bool
) -> None:
    path = tmp_path / "cv.docx"
    path.write_bytes(_docx_padded_to(size))

    assert _reason(lambda: cv.extract_text(path)) == (None if readable else "too_large")


@pytest.mark.parametrize(
    ("name", "content"),
    [
        ("encrypted.pdf", _encrypted_pdf),
        ("not_zip.docx", lambda: b"not a docx"),
        ("binary.txt", lambda: LONG_LINE.encode() + b"\x00\x01\x02"),
        (
            "odd_utf16.txt",
            lambda: codecs.BOM_UTF16_LE + NORWEGIAN_LINE.encode("utf-16-le") + b"x",
        ),
        ("pdf_as.txt", lambda: b"\n  " + pdf([_plain_stream(PDF_TEXT_LINE * 8)])),
    ],
)
def test_should_raise_unreadable_for_broken_file(
    tmp_path: Path, name: str, content: Callable[[], bytes]
) -> None:
    path = tmp_path / name
    path.write_bytes(content())

    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.extract_text(path)

    assert raised.value.reason == "unreadable"


def test_should_raise_unreadable_when_file_is_missing(tmp_path: Path) -> None:
    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.extract_text(tmp_path / "missing.pdf")

    assert raised.value.reason == "unreadable"


@pytest.mark.parametrize(
    ("name", "build"),
    [("cv.pdf", pdf_with_text), ("cv.txt", str.encode)],
)
@pytest.mark.parametrize(("chars", "readable"), [(100_000, True), (100_001, False)])
def test_should_reject_text_over_100_000_chars(
    tmp_path: Path,
    name: str,
    build: Callable[[str], bytes],
    chars: int,
    readable: bool,
) -> None:
    path = tmp_path / name
    path.write_bytes(build("x" * chars))

    assert _reason(lambda: cv.extract_text(path)) == (None if readable else "too_large")


def test_should_turn_carriage_returns_into_line_breaks(tmp_path: Path) -> None:
    path = tmp_path / "cv.txt"
    path.write_bytes(f"{LONG_LINE}\r\nLINE-TWO\rLINE-THREE".encode())

    assert cv.extract_text(path) == f"{LONG_LINE}\nLINE-TWO\nLINE-THREE"


@pytest.mark.parametrize(
    ("content", "expected"),
    [
        pytest.param(
            lambda: pdf(
                [
                    flate_stream(
                        (b"BT /F1 1 Tf 0 0 Td (" + b"A" * 1000 + b") Tj ET\n") * 70_000
                    )
                ]
            ),
            "too_large",
            id="pdf with 70 MB of text on one page",
        ),
        pytest.param(
            lambda: docx(document(paragraph(LONG_LINE) + "<w:p/>" * 7_000_000)),
            "too_large",
            id="docx with 7 million paragraphs",
        ),
        pytest.param(
            lambda: _docx_table_with_cells(20_000, 50),
            "too_large",
            id="docx with 1 million cells",
        ),
        pytest.param(
            _docx_lying_about_size, "unreadable", id="docx lying about its size"
        ),
    ],
)
def test_should_stop_hostile_file_within_deadline(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    content: Callable[[], bytes],
    expected: str,
) -> None:
    monkeypatch.setattr(cv, "EXTRACT_DEADLINE_SECONDS", TEST_DEADLINE_SECONDS)
    data = content()
    path = tmp_path / ("cv.pdf" if data.startswith(b"%PDF") else "cv.docx")
    path.write_bytes(data)

    reason = _reason_within(
        TEST_DEADLINE_SECONDS + DEADLINE_GRACE_SECONDS, lambda: cv.extract_text(path)
    )

    assert reason == expected


def test_should_raise_too_large_when_pdf_has_10_million_operators(
    tmp_path: Path,
) -> None:
    path = tmp_path / "cv.pdf"
    path.write_bytes(pdf([flate_stream(PDF_TEXT_LINE * 5 + b"0 0 Td\n" * 10_000_000)]))

    reason = _reason_within(STREAM_LIMIT_WAIT_SECONDS, lambda: cv.extract_text(path))

    assert reason == "too_large"


def test_should_raise_too_large_when_worker_misses_deadline(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(cv, "EXTRACT_DEADLINE_SECONDS", TEST_DEADLINE_SECONDS)
    monkeypatch.setattr(cv, "WORKER_COMMAND", fake_cv_workers.command("sleep"))
    data = CV_PDF.read_bytes()

    reason = _reason_within(
        TEST_DEADLINE_SECONDS + DEADLINE_GRACE_SECONDS,
        lambda: cv.text_from_upload("cv.pdf", data),
    )

    assert reason == "too_large"


def test_should_raise_unreadable_when_worker_crashes(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        cv, "WORKER_COMMAND", fake_cv_workers.command("crash_after_reply")
    )

    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.text_from_upload("cv.pdf", CV_PDF.read_bytes())

    assert raised.value.reason == "unreadable"


@pytest.mark.parametrize(
    "reply",
    [
        b"not json",
        b"[]",
        b'{"text": 5}',
        b'{"reason": "no_text"}',
    ],
    ids=[
        "not json",
        "not an object",
        "text not a string",
        "unknown reason",
    ],
)
def test_should_raise_unreadable_for_malformed_worker_reply(
    monkeypatch: pytest.MonkeyPatch, reply: bytes
) -> None:
    monkeypatch.setattr(cv, "WORKER_COMMAND", fake_cv_workers.command("echo"))

    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.text_from_upload("cv.pdf", reply)

    assert raised.value.reason == "unreadable"


@pytest.mark.parametrize(
    ("size", "readable"),
    [(cv.MAX_WORKER_MESSAGE_BYTES, True), (cv.MAX_WORKER_MESSAGE_BYTES + 1, False)],
)
def test_should_limit_worker_reply_size(
    monkeypatch: pytest.MonkeyPatch, size: int, readable: bool
) -> None:
    monkeypatch.setattr(cv, "WORKER_COMMAND", fake_cv_workers.command("echo"))
    reply = _worker_reply_of_size(size)

    reason = _reason(lambda: cv.text_from_upload("cv.pdf", reply))

    assert reason == (None if readable else "unreadable")


def test_should_raise_unreadable_when_worker_cannot_start(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setattr(cv, "WORKER_COMMAND", (str(tmp_path / "missing-python"),))

    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.text_from_upload("cv.pdf", CV_PDF.read_bytes())

    assert raised.value.reason == "unreadable"


def test_should_block_file_writes_in_worker(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    leaked = tmp_path / fake_cv_workers.LEAKED_FILE
    monkeypatch.setattr(
        cv, "WORKER_COMMAND", fake_cv_workers.command("write_file", str(leaked))
    )
    data = CV_PDF.read_bytes()

    reason = _reason(lambda: cv.text_from_upload("cv.pdf", data))

    assert reason == "unreadable"
    assert leaked.read_bytes() == b""


def test_should_hide_worker_stderr(
    monkeypatch: pytest.MonkeyPatch, capfd: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setattr(cv, "WORKER_COMMAND", fake_cv_workers.command("noise"))

    cv.text_from_upload("cv.pdf", CV_PDF.read_bytes())

    assert capfd.readouterr().err == ""


def test_should_raise_unreadable_when_path_is_not_a_regular_file(
    tmp_path: Path,
) -> None:
    path = tmp_path / "cv.txt"
    os.mkfifo(path)

    reason = _reason_within(
        BLOCKED_READ_TIMEOUT_SECONDS, lambda: cv.extract_text(path), _release_fifo(path)
    )

    assert reason == "unreadable"


@pytest.mark.parametrize(
    ("name", "content"),
    [("notes.md", CV_MARKDOWN.read_bytes), ("cv.pdf", CV_PDF.read_bytes)],
)
def test_should_extract_upload_without_writing_to_disk(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    name: str,
    content: Callable[[], bytes],
) -> None:
    data = content()
    for variable in WRITABLE_DIR_VARIABLES:
        monkeypatch.setenv(variable, str(tmp_path))
    monkeypatch.setattr(tempfile, "tempdir", None)
    monkeypatch.chdir(tmp_path)

    cv.text_from_upload(name, data)

    assert list(tmp_path.iterdir()) == []


@pytest.mark.parametrize(
    ("size", "readable"),
    [(cv.CV_MAX_BYTES, True), (cv.CV_MAX_BYTES + 1, False)],
)
def test_should_reject_upload_over_5_mb(size: int, readable: bool) -> None:
    data = _pdf_padded_to(size)

    reason = _reason(lambda: cv.text_from_upload("cv.pdf", data))

    assert reason == (None if readable else "too_large")


def test_should_reject_docx_upload_from_the_website() -> None:
    with pytest.raises(cv.CvUnreadableError) as raised:
        cv.text_from_upload("cv.docx", _docx_with_stack_table())

    assert raised.value.reason == "type"


def test_should_run_one_worker_at_a_time(monkeypatch: pytest.MonkeyPatch) -> None:
    live = 0
    peak = 0
    overlap = threading.Condition()

    def tracked_run(
        argv: Sequence[str], data: bytes, timeout: float
    ) -> subprocess.CompletedProcess[bytes]:
        nonlocal live, peak
        with overlap:
            live += 1
            peak = max(peak, live)
            overlap.notify_all()
            overlap.wait_for(lambda: live > 1, OVERLAP_WAIT_SECONDS)
            live -= 1
        return subprocess.CompletedProcess(
            argv, 0, fake_cv_workers.VALID_REPLY.encode()
        )

    monkeypatch.setattr(cv.child_process, "run_child", tracked_run)
    uploads = [
        threading.Thread(target=cv.text_from_upload, args=("cv.pdf", b"%PDF-"))
        for _ in range(CONCURRENT_UPLOADS)
    ]

    for upload in uploads:
        upload.start()
    for upload in uploads:
        upload.join()

    assert peak == 1
