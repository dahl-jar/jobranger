from __future__ import annotations

import json
import os
import resource
import sys
import time

from jobranger import cv_worker

CRASH_EXIT_CODE = 3
VALID_REPLY = json.dumps({"text": "Backend developer at Employer One AS. " * 10})
STDERR_NOISE = "noise from the worker\n"
SLEEP_SECONDS = 60
LEAKED_FILE = "leaked.bin"
LEAKED_DIR = "leaked"
SCRIPT = os.path.abspath(__file__)


def _reply(data: bytes) -> None:
    sys.stdout.buffer.write(data)
    sys.stdout.buffer.flush()


def crash_after_reply() -> None:
    _reply(VALID_REPLY.encode())
    os._exit(CRASH_EXIT_CODE)


def echo_input() -> None:
    _reply(sys.stdin.buffer.read())


def sleep_without_reply() -> None:
    time.sleep(SLEEP_SECONDS)


def write_noise_then_reply() -> None:
    sys.stderr.write(STDERR_NOISE)
    sys.stderr.flush()
    _reply(VALID_REPLY.encode())


def report_environment() -> None:
    _reply(json.dumps(dict(os.environ)).encode())


def write_file_then_reply() -> None:
    cv_worker.limit_resources()
    with open(sys.argv[2], "wb") as leaked:
        leaked.write(sys.stdin.buffer.read())
    _reply(VALID_REPLY.encode())


def try_writes_in_cwd() -> None:
    denied = []
    for name, attempt in (
        ("mkdir", lambda: os.mkdir(LEAKED_DIR)),
        ("create", lambda: open(LEAKED_FILE, "wb").close()),
    ):
        try:
            attempt()
        except PermissionError:
            denied.append(name)
    _reply(json.dumps({"cwd": os.getcwd(), "denied": denied}).encode())


def report_limits() -> None:
    cv_worker.limit_resources()
    cpu_soft, _ = resource.getrlimit(resource.RLIMIT_CPU)
    memory_soft, _ = resource.getrlimit(resource.RLIMIT_AS)
    _reply(json.dumps([cpu_soft, memory_soft]).encode())


MODES = {
    "crash_after_reply": crash_after_reply,
    "echo": echo_input,
    "sleep": sleep_without_reply,
    "noise": write_noise_then_reply,
    "limits": report_limits,
    "environment": report_environment,
    "write_file": write_file_then_reply,
    "write_cwd": try_writes_in_cwd,
}


def command(mode: str, *args: str) -> tuple[str, ...]:
    return (sys.executable, SCRIPT, mode, *args)


if __name__ == "__main__":
    MODES[sys.argv[1]]()
