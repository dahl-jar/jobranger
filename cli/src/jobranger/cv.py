from __future__ import annotations

import codecs
import json
import os
import stat
import subprocess
import sys
import threading
import unicodedata
from pathlib import Path
from typing import Literal

from jobranger import child_process

type UnreadableReason = Literal["type", "unreadable", "no_text", "too_large"]

PDF_SUFFIX = ".pdf"
DOCX_SUFFIX = ".docx"
MAX_CV_TEXT_CHARS = 100_000
EXTRACT_DEADLINE_SECONDS = 15
TEXT_KEY = "text"
REASON_KEY = "reason"
CV_MAX_BYTES = 5_242_880
MIN_CV_TEXT_CHARS = 200
MAX_WORKER_MESSAGE_BYTES = 16 * MAX_CV_TEXT_CHARS
TEXT_SUFFIXES = frozenset({".txt", ".md"})
WORKER_SUFFIXES = frozenset({PDF_SUFFIX, DOCX_SUFFIX})
READ_SUFFIXES = WORKER_SUFFIXES | TEXT_SUFFIXES
UPLOAD_SUFFIXES = frozenset({PDF_SUFFIX, *TEXT_SUFFIXES})
UTF16_BOMS = (codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)
NUL_BYTE = b"\x00"
PDF_MAGIC = b"%PDF-"
UTF8 = "utf-8"
UTF16 = "utf-16"
CP1252 = "cp1252"
ASCII_MAX = "\x7f"
UNSAFE_CATEGORIES = frozenset({"Cc", "Cf", "Cs"})
KEPT_CHARS = frozenset("\t\n\u200c\u200d")
REPLACEMENT_CHAR = "\ufffd"
NONBLOCKING_FLAG: int = getattr(os, "O_NONBLOCK", 0)
WORKER_COMMAND: tuple[str, ...] = (sys.executable, "-I", "-m", "jobranger.cv_worker")
EXTRACT_LOCK = threading.Lock()


class CvUnreadableError(Exception):
    def __init__(self, reason: UnreadableReason) -> None:
        super().__init__(f"cv file unreadable: {reason}")
        self.reason: UnreadableReason = reason


def extract_text(path: Path) -> str:
    suffix = path.suffix.lower()
    if suffix not in READ_SUFFIXES:
        raise CvUnreadableError("type")
    return _text_from_bytes(suffix, _read_regular_file(path))


def text_from_upload(name: str, data: bytes) -> str:
    suffix = Path(name).suffix.lower()
    if suffix not in UPLOAD_SUFFIXES:
        raise CvUnreadableError("type")
    return _text_from_bytes(suffix, data)


def _text_from_bytes(suffix: str, data: bytes) -> str:
    if len(data) > CV_MAX_BYTES:
        raise CvUnreadableError("too_large")
    raw = _decode_text(data) if suffix in TEXT_SUFFIXES else _run_worker(suffix, data)
    text = _clean_text(raw)
    if len(text) > MAX_CV_TEXT_CHARS:
        raise CvUnreadableError("too_large")
    if sum(not char.isspace() for char in text) < MIN_CV_TEXT_CHARS:
        raise CvUnreadableError("no_text")
    return text


def _clean_text(text: str) -> str:
    lines = text.replace("\r\n", "\n").replace("\r", "\n")
    return "".join(char for char in lines if _is_safe(char))


def _is_safe(char: str) -> bool:
    return char in KEPT_CHARS or unicodedata.category(char) not in UNSAFE_CATEGORIES


def _open_nonblocking(file: str, flags: int) -> int:
    return os.open(file, flags | NONBLOCKING_FLAG)


def _read_regular_file(path: Path) -> bytes:
    try:
        with open(path, "rb", opener=_open_nonblocking) as handle:
            if not stat.S_ISREG(os.fstat(handle.fileno()).st_mode):
                raise CvUnreadableError("unreadable")
            data = handle.read(CV_MAX_BYTES + 1)
    except OSError as error:
        raise CvUnreadableError("unreadable") from error
    return data


def _decode_text(data: bytes) -> str:
    if data.startswith(UTF16_BOMS):
        return _decode_strict(data, UTF16)
    if NUL_BYTE in data or data.lstrip().startswith(PDF_MAGIC):
        raise CvUnreadableError("unreadable")
    body = data.removeprefix(codecs.BOM_UTF8)
    try:
        return body.decode(UTF8)
    except UnicodeDecodeError:
        lenient = body.decode(UTF8, errors="replace")
    invalid = lenient.count(REPLACEMENT_CHAR)
    multibyte = sum(char > ASCII_MAX for char in lenient) - invalid
    decoded = lenient if multibyte > invalid else body.decode(CP1252, errors="replace")
    return decoded.replace(REPLACEMENT_CHAR, "")


def _decode_strict(data: bytes, encoding: str) -> str:
    try:
        return data.decode(encoding)
    except UnicodeDecodeError as error:
        raise CvUnreadableError("unreadable") from error


def _run_worker(suffix: str, data: bytes) -> str:
    try:
        with EXTRACT_LOCK:
            result = child_process.run_child(
                [*WORKER_COMMAND, suffix], data, EXTRACT_DEADLINE_SECONDS
            )
    except subprocess.TimeoutExpired as error:
        raise CvUnreadableError("too_large") from error
    except OSError as error:
        raise CvUnreadableError("unreadable") from error
    if result.returncode != 0 or len(result.stdout) > MAX_WORKER_MESSAGE_BYTES:
        raise CvUnreadableError("unreadable")
    return _text_from_message(result.stdout)


def _text_from_message(message: bytes) -> str:
    try:
        reply = json.loads(message)
    except ValueError as error:
        raise CvUnreadableError("unreadable") from error
    if not isinstance(reply, dict):
        raise CvUnreadableError("unreadable")
    text = reply.get(TEXT_KEY)
    if isinstance(text, str):
        return text
    if reply.get(REASON_KEY) == "too_large":
        raise CvUnreadableError("too_large")
    raise CvUnreadableError("unreadable")
