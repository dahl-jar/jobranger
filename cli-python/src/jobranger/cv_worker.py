from __future__ import annotations

import io
import json
import logging
import re
import sys
import zipfile
from typing import IO, TYPE_CHECKING

from defusedxml import ElementTree
from pypdf import PdfReader
from pypdf.errors import LimitReachedError

from jobranger.cv import (
    DOCX_SUFFIX,
    EXTRACT_DEADLINE_SECONDS,
    MAX_CV_TEXT_CHARS,
    PDF_SUFFIX,
    REASON_KEY,
    TEXT_KEY,
)

if sys.platform != "win32":
    import resource

if TYPE_CHECKING:
    from collections.abc import Callable
    from xml.etree.ElementTree import Element

MAX_PDF_PAGES = 50
DOCX_MAX_UNPACKED_BYTES = 52_428_800
MEMORY_LIMIT_BYTES = 1 << 30
FILE_SIZE_LIMIT_BYTES = 0
CPU_MARGIN_SECONDS = 5
CPU_LIMIT_SECONDS = EXTRACT_DEADLINE_SECONDS + CPU_MARGIN_SECONDS
LINUX_PLATFORM = "linux"
WORD_NS = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
MC_FALLBACK = "{http://schemas.openxmlformats.org/markup-compatibility/2006}Fallback"
TEXT_TAG = f"{WORD_NS}t"
TAG_CHARACTERS = {f"{WORD_NS}noBreakHyphen": "-"}
TAG_SEPARATORS = {
    f"{WORD_NS}p": "\n",
    f"{WORD_NS}tab": "\t",
    f"{WORD_NS}br": "\n",
    f"{WORD_NS}cr": "\n",
}
DOCUMENT_PART = "word/document.xml"
NOTE_PARTS = ("word/footnotes.xml", "word/endnotes.xml")
HEADER_PART = re.compile(r"word/header\d*\.xml")
FOOTER_PART = re.compile(r"word/footer\d*\.xml")
XML_EVENTS = ("start", "end")
WORK_LIMIT_MESSAGES = (
    "Reached maximum number of tokens",
    "Limit reached while decompressing",
    "Stream decoding exceeded the maximum accumulated",
    "Array-based stream has",
    "Maximum page tree page entry limit",
)


class TooLargeError(Exception):
    pass


def main() -> None:
    logging.disable(logging.CRITICAL)
    limit_resources()
    reply = extract(sys.argv[-1], sys.stdin.buffer.read())
    sys.stdout.buffer.write(json.dumps(reply).encode())


def extract(suffix: str, data: bytes) -> dict[str, str]:
    try:
        return {TEXT_KEY: EXTRACTORS[suffix](data)}
    except TooLargeError:
        return {REASON_KEY: "too_large"}
    except Exception:
        return {REASON_KEY: "unreadable"}


def limit_resources() -> None:
    if sys.platform == "win32":
        return
    _lower_soft_limit(resource.RLIMIT_CPU, CPU_LIMIT_SECONDS)
    _lower_soft_limit(resource.RLIMIT_FSIZE, FILE_SIZE_LIMIT_BYTES)
    if sys.platform.startswith(LINUX_PLATFORM):
        _lower_soft_limit(resource.RLIMIT_AS, MEMORY_LIMIT_BYTES)


def _lower_soft_limit(kind: int, value: int) -> None:
    soft, hard = resource.getrlimit(kind)
    finite = [limit for limit in (soft, hard) if limit != resource.RLIM_INFINITY]
    resource.setrlimit(kind, (min([value, *finite]), hard))


class _TextBuffer:
    def __init__(self) -> None:
        self._parts: list[str] = []
        self._separators: list[str] = []
        self._length = 0

    def separate(self, separator: str) -> None:
        self._separators.append(separator)
        if len(self._separators) > MAX_CV_TEXT_CHARS:
            raise TooLargeError

    def append(self, text: str) -> None:
        if not text:
            return
        self._add("".join(self._separators))
        self._separators.clear()
        self._add(text)

    def _add(self, text: str) -> None:
        self._length += len(text)
        if self._length > MAX_CV_TEXT_CHARS:
            raise TooLargeError
        self._parts.append(text)

    def value(self) -> str:
        return "".join(self._parts)


class _UnpackBudget:
    def __init__(self, limit: int) -> None:
        self._remaining = limit

    def spend(self, size: int) -> None:
        self._remaining -= size
        if self._remaining < 0:
            raise TooLargeError


class _CappedReader:
    def __init__(self, member: IO[bytes], budget: _UnpackBudget) -> None:
        self._member = member
        self._budget = budget

    def read(self, size: int = -1) -> bytes:
        chunk = self._member.read(size)
        self._budget.spend(len(chunk))
        return chunk


def _pdf_text(data: bytes) -> str:
    try:
        return _read_pdf_text(data)
    except LimitReachedError as error:
        if str(error).startswith(WORK_LIMIT_MESSAGES):
            raise TooLargeError from error
        raise


def _read_pdf_text(data: bytes) -> str:
    reader = PdfReader(io.BytesIO(data))
    if len(reader.pages) > MAX_PDF_PAGES:
        raise TooLargeError
    text = _TextBuffer()
    for index, page in enumerate(reader.pages):
        if index:
            text.separate("\n")
        text.append(page.extract_text())
    return text.value()


def _docx_text(data: bytes) -> str:
    text = _TextBuffer()
    budget = _UnpackBudget(DOCX_MAX_UNPACKED_BYTES)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in _docx_parts(archive.namelist()):
            with archive.open(name) as member:
                _append_part_text(_CappedReader(member, budget), text)
    return text.value()


def _docx_parts(names: list[str]) -> list[str]:
    def ordered(pattern: re.Pattern[str]) -> list[str]:
        return sorted(
            (name for name in names if pattern.fullmatch(name)),
            key=lambda name: (len(name), name),
        )

    notes = [name for name in NOTE_PARTS if name in names]
    return [*ordered(HEADER_PART), DOCUMENT_PART, *notes, *ordered(FOOTER_PART)]


def _append_part_text(source: _CappedReader, text: _TextBuffer) -> None:
    open_elements: list[Element] = []
    fallback_depth = 0
    for event, element in ElementTree.iterparse(
        source, events=XML_EVENTS, forbid_dtd=True
    ):
        if event == "start":
            open_elements.append(element)
            if element.tag == MC_FALLBACK:
                fallback_depth += 1
            continue
        open_elements.pop()
        if element.tag == MC_FALLBACK:
            fallback_depth -= 1
        elif not fallback_depth:
            _append_element_text(element, text)
        if open_elements:
            open_elements[-1].remove(element)


def _append_element_text(element: Element, text: _TextBuffer) -> None:
    if element.tag == TEXT_TAG:
        text.append(element.text or "")
    elif element.tag in TAG_CHARACTERS:
        text.append(TAG_CHARACTERS[element.tag])
    elif element.tag in TAG_SEPARATORS:
        text.separate(TAG_SEPARATORS[element.tag])


EXTRACTORS: dict[str, Callable[[bytes], str]] = {
    PDF_SUFFIX: _pdf_text,
    DOCX_SUFFIX: _docx_text,
}


if __name__ == "__main__":
    main()
