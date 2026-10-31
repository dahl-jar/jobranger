from __future__ import annotations

import json
import os
import resource
import sys
import tempfile
import time
import tracemalloc
from pathlib import Path

import fake_cv_workers
import pytest
from cv_samples import (
    LONG_LINE,
    PDF_TEXT_LINE,
    document,
    docx,
    flate_stream,
    page_stream,
    paragraph,
    pdf,
    pdf_with_contents_array,
    pdf_with_text,
    repeated_cv_pdf,
    word_part,
)
from pypdf import apply_configuration, get_configuration

from jobranger import child_process, cv, cv_worker

REPLY_TIMEOUT_SECONDS = 30
EMPTY_ELEMENTS = 250_000
PEAK_MEMORY_BYTES = 8_000_000
SENTINEL_VARIABLE = "JOBRANGER_SENTINEL"
SYSTEM_ROOT_VARIABLE = "SYSTEMROOT"
FAKE_SYSTEM_ROOT = "C:\\Windows"
HALF_TEXT_CAP = cv.MAX_CV_TEXT_CHARS // 2
TOO_LARGE = {"reason": "too_large"}
UNREADABLE = {"reason": "unreadable"}
TEXT_BOX = (
    '<w:p><w:r><mc:AlternateContent><mc:Choice Requires="wps"><w:drawing>'
    "<wps:txbx><w:txbxContent>"
    + paragraph("TEXT-BOX")
    + "</w:txbxContent></wps:txbx></w:drawing></mc:Choice><mc:Fallback>"
    "<w:pict><v:shape><v:textbox><w:txbxContent>"
    + paragraph("TEXT-BOX")
    + "</w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback>"
    "</mc:AlternateContent></w:r></w:p>"
)


def _docx_unpacked_to(size: int) -> bytes:
    body = paragraph("x")
    padding = size - len(document(body))
    return docx(document(" " * padding + body))


def _raise_memory_error(data: bytes) -> str:
    raise MemoryError


@pytest.mark.parametrize(
    ("body", "marker", "count"),
    [
        pytest.param(TEXT_BOX, "TEXT-BOX", 1, id="text box with fallback"),
        pytest.param(
            TEXT_BOX + paragraph("AFTER-BOX"),
            "AFTER-BOX",
            1,
            id="text after a text box",
        ),
        pytest.param(
            "<mc:AlternateContent><mc:Fallback>"
            + TEXT_BOX
            + "</mc:Fallback></mc:AlternateContent>"
            + paragraph("AFTER-NESTED"),
            "AFTER-NESTED",
            1,
            id="text after a nested fallback",
        ),
        pytest.param(
            '<w:p><w:del w:id="2" w:author="user"><w:r><w:delText>DELETED'
            "</w:delText></w:r></w:del></w:p>",
            "DELETED",
            0,
            id="tracked deletion",
        ),
        pytest.param(
            "<w:p><w:r><w:t>LINE-ONE</w:t><w:br/><w:t>LINE-TWO</w:t><w:cr/>"
            "<w:t>LINE-THREE</w:t><w:tab/><w:t>COLUMN</w:t></w:r></w:p>",
            "LINE-ONE\nLINE-TWO\nLINE-THREE\tCOLUMN",
            1,
            id="breaks and tabs",
        ),
    ],
)
def test_should_extract_word_structure_text(body: str, marker: str, count: int) -> None:
    data = docx(document(paragraph(LONG_LINE) + body))

    reply = cv_worker.extract(".docx", data)

    assert reply["text"].count(marker) == count


def test_should_keep_blank_word_paragraphs_as_line_breaks() -> None:
    data = docx(
        document(paragraph("ONE") + "<w:p><w:r><w:t/></w:r></w:p>" + paragraph("TWO"))
    )

    reply = cv_worker.extract(".docx", data)

    assert reply == {"text": "ONE\n\nTWO"}


def test_should_read_word_parts_in_page_order() -> None:
    parts = [
        (f"word/{kind}{number}.xml", word_part(root, paragraph(f"{kind}-{number}")))
        for kind, root in (("footer", "ftr"), ("header", "hdr"))
        for number in (10, 2, 1)
    ]
    data = docx(document(paragraph("BODY")), parts)

    reply = cv_worker.extract(".docx", data)

    assert reply == {
        "text": "header-1\nheader-2\nheader-10\nBODY\nfooter-1\nfooter-2\nfooter-10"
    }


@pytest.mark.parametrize(
    "data",
    [
        pytest.param(
            docx(
                document(paragraph(LONG_LINE)).replace(
                    b"?>", b"?><!DOCTYPE w:document>", 1
                )
            ),
            id="doctype",
        ),
        pytest.param(
            docx(None, [("word/header1.xml", word_part("hdr", paragraph(LONG_LINE)))]),
            id="no document part",
        ),
    ],
)
def test_should_report_unreadable_word_file(data: bytes) -> None:
    reply = cv_worker.extract(".docx", data)

    assert reply == UNREADABLE


def test_should_report_unreadable_when_extraction_runs_out_of_memory(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setitem(cv_worker.EXTRACTORS, ".pdf", _raise_memory_error)

    reply = cv_worker.extract(".pdf", b"")

    assert reply == UNREADABLE


def test_should_report_too_large_when_pdf_stream_passes_decompression_limit() -> None:
    limit = get_configuration().zlib_maximum_output_length
    data = pdf([flate_stream(b" " * (limit + 1))])

    reply = cv_worker.extract(".pdf", data)

    assert reply == TOO_LARGE


def test_should_report_too_large_when_pdf_stream_passes_decoding_work_limit() -> None:
    data = pdf_with_text("x")

    with apply_configuration(stream_decoding_work_maximum_length=0):
        reply = cv_worker.extract(".pdf", data)

    assert reply == TOO_LARGE


def test_should_report_too_large_when_pdf_contents_array_passes_output_limit() -> None:
    half_limit = get_configuration().array_based_stream_maximum_output_length // 2
    data = pdf_with_contents_array([b" " * (half_limit + 1)] * 2)

    reply = cv_worker.extract(".pdf", data)

    assert reply == TOO_LARGE


@pytest.mark.parametrize(
    ("body", "expected"),
    [
        pytest.param(
            paragraph("x" * cv.MAX_CV_TEXT_CHARS),
            {"text": "x" * cv.MAX_CV_TEXT_CHARS},
            id="at cap",
        ),
        pytest.param(
            paragraph("x" * HALF_TEXT_CAP) * 2, TOO_LARGE, id="over cap across lines"
        ),
    ],
)
def test_should_cap_word_text_at_100_000_chars(
    body: str, expected: dict[str, str]
) -> None:
    data = docx(document(body))

    reply = cv_worker.extract(".docx", data)

    assert reply == expected


@pytest.mark.parametrize(
    ("blank_paragraphs", "expected"),
    [
        pytest.param(cv.MAX_CV_TEXT_CHARS - 1, {"text": "x"}, id="at cap"),
        pytest.param(cv.MAX_CV_TEXT_CHARS, TOO_LARGE, id="over cap"),
    ],
)
def test_should_cap_pending_word_line_breaks(
    blank_paragraphs: int, expected: dict[str, str]
) -> None:
    data = docx(document(paragraph("x") + "<w:p/>" * blank_paragraphs))

    reply = cv_worker.extract(".docx", data)

    assert reply == expected


@pytest.mark.parametrize(
    ("size", "expected"),
    [
        pytest.param(cv_worker.DOCX_MAX_UNPACKED_BYTES, {"text": "x"}, id="at limit"),
        pytest.param(cv_worker.DOCX_MAX_UNPACKED_BYTES + 1, TOO_LARGE, id="over limit"),
    ],
)
def test_should_cap_unpacked_word_bytes(size: int, expected: dict[str, str]) -> None:
    data = _docx_unpacked_to(size)

    reply = cv_worker.extract(".docx", data)

    assert reply == expected


def test_should_read_notes_after_body_and_hyphens_inline() -> None:
    parts = [
        ("word/footer1.xml", word_part("ftr", paragraph("FOOTER"))),
        ("word/endnotes.xml", word_part("endnotes", paragraph("ENDNOTE"))),
        ("word/footnotes.xml", word_part("footnotes", paragraph("FOOTNOTE"))),
    ]
    body = (
        "<w:p><w:r><w:t>e</w:t><w:noBreakHyphen/><w:t>post</w:t></w:r></w:p>"
        "<w:p><w:r><w:t>data</w:t><w:softHyphen/><w:t>base</w:t></w:r></w:p>"
    )
    data = docx(document(body), parts)

    reply = cv_worker.extract(".docx", data)

    assert reply == {"text": "e-post\ndatabase\nFOOTNOTE\nENDNOTE\nFOOTER"}


def test_should_join_pdf_pages_with_line_breaks() -> None:
    data = pdf([page_stream("ONE"), page_stream("TWO")])

    reply = cv_worker.extract(".pdf", data)

    assert reply == {"text": "ONE\nTWO"}


@pytest.mark.parametrize(("pages", "readable"), [(50, True), (51, False)])
def test_should_reject_pdf_over_50_pages(pages: int, readable: bool) -> None:
    data = repeated_cv_pdf(pages)

    reply = cv_worker.extract(".pdf", data)

    assert reply.get("reason") == (None if readable else "too_large")


def test_should_stop_reading_pdf_pages_once_text_passes_cap() -> None:
    broken_page = flate_stream(PDF_TEXT_LINE).replace(b"/FlateDecode", b"/FlateDekode")
    data = pdf([flate_stream(b"BT (" + b"x" * 100_001 + b") Tj ET"), broken_page])

    reply = cv_worker.extract(".pdf", data)

    assert reply == TOO_LARGE


def test_should_lower_cpu_and_memory_limits_in_worker() -> None:
    result = child_process.run_child(
        fake_cv_workers.command("limits"), b"", REPLY_TIMEOUT_SECONDS
    )
    limits = json.loads(result.stdout)
    memory_soft, _ = resource.getrlimit(resource.RLIMIT_AS)
    expected_memory = (
        cv_worker.MEMORY_LIMIT_BYTES
        if sys.platform.startswith("linux")
        else memory_soft
    )

    assert limits == [cv_worker.CPU_LIMIT_SECONDS, expected_memory]


def test_should_pass_only_system_root_to_worker(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv(SENTINEL_VARIABLE, "secret")
    monkeypatch.setenv(SYSTEM_ROOT_VARIABLE, FAKE_SYSTEM_ROOT)

    result = child_process.run_child(
        fake_cv_workers.command("environment"), b"", REPLY_TIMEOUT_SECONDS
    )

    environment = json.loads(result.stdout)
    assert environment.get(SYSTEM_ROOT_VARIABLE) == FAKE_SYSTEM_ROOT
    assert SENTINEL_VARIABLE not in environment


@pytest.mark.parametrize(
    ("age_seconds", "removed"),
    [
        pytest.param(child_process.STALE_WORKDIR_SECONDS + 1, True, id="stale"),
        pytest.param(0, False, id="in use"),
    ],
)
def test_should_remove_workdir_left_by_killed_run(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path, age_seconds: int, removed: bool
) -> None:
    monkeypatch.setenv("TMPDIR", str(tmp_path))
    monkeypatch.setattr(tempfile, "tempdir", None)
    leftover = tmp_path / f"{child_process.WORKDIR_PREFIX}killed"
    leftover.mkdir()
    (leftover / fake_cv_workers.LEAKED_FILE).touch()
    leftover.chmod(child_process.WORKDIR_MODE)
    created = time.time() - age_seconds
    os.utime(leftover, (created, created))

    child_process.run_child(fake_cv_workers.command("echo"), b"", REPLY_TIMEOUT_SECONDS)

    assert leftover.exists() is not removed


def test_should_run_worker_in_a_fresh_directory(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.chdir(tmp_path)

    result = child_process.run_child(
        fake_cv_workers.command("write_cwd"), b"", REPLY_TIMEOUT_SECONDS
    )

    worker_cwd = Path(json.loads(result.stdout)["cwd"])
    assert worker_cwd.name.startswith(child_process.WORKDIR_PREFIX)
    assert worker_cwd.resolve() != tmp_path.resolve()
    assert not worker_cwd.exists()
    assert list(tmp_path.iterdir()) == []


@pytest.mark.skipif(os.geteuid() == 0, reason="root ignores directory permissions")
def test_should_deny_worker_writes_in_its_directory() -> None:
    result = child_process.run_child(
        fake_cv_workers.command("write_cwd"), b"", REPLY_TIMEOUT_SECONDS
    )

    assert json.loads(result.stdout)["denied"] == ["mkdir", "create"]


def test_should_free_parsed_word_elements_while_reading() -> None:
    data = docx(document(paragraph(LONG_LINE) + "<w:x/>" * EMPTY_ELEMENTS))
    tracemalloc.start()

    cv_worker.extract(".docx", data)

    _, peak = tracemalloc.get_traced_memory()
    tracemalloc.stop()
    assert peak < PEAK_MEMORY_BYTES
