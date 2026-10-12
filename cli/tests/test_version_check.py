from __future__ import annotations

import io
import json
import sys
import threading
import time
from datetime import UTC, datetime, timedelta
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path
from typing import TYPE_CHECKING

import httpx
import pytest
from packaging.version import Version

from jobranger import version_check
from jobranger.version_check import (
    CACHE_FILE,
    INSTALL_METHOD_ENV,
    MAX_BODY_BYTES,
    OPT_OUT_ENV,
    PYPI_URL,
    install_method,
    latest_version,
    maybe_notify,
)

if TYPE_CHECKING:
    from collections.abc import Callable, Iterator

NOW = datetime(2026, 10, 12, 9, 0, tzinfo=UTC)
NEWER = "99.0.0"
CURRENT = version("jobranger")
STALL_S = 5.0


def pypi_reply(latest: str) -> Callable[[httpx.Request], httpx.Response]:
    def respond(request: httpx.Request) -> httpx.Response:
        if (request.method, request.url) != ("GET", PYPI_URL):
            return httpx.Response(404)
        return httpx.Response(200, json={"info": {"version": latest}})

    return respond


class Recorder:
    def __init__(self, respond: Callable[[httpx.Request], httpx.Response]) -> None:
        self.requests: list[httpx.Request] = []
        self.respond = respond

    def __call__(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        return self.respond(request)

    def client(self) -> httpx.Client:
        return httpx.Client(transport=httpx.MockTransport(self))


def time_out(request: httpx.Request) -> httpx.Response:
    raise httpx.ReadTimeout("timed out", request=request)


@pytest.fixture(autouse=True)
def state_dir(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    monkeypatch.setenv("JOBRANGER_STATE_DIR", str(tmp_path))
    return tmp_path


@pytest.fixture
def cache(state_dir: Path) -> Path:
    return state_dir / CACHE_FILE


def notice(recorder: Recorder, env: dict[str, str]) -> str:
    out = io.StringIO()
    maybe_notify(out, recorder.client(), env, NOW, interactive=True)
    return out.getvalue()


@pytest.mark.parametrize(
    ("method", "command"),
    [
        ("uv", "uv tool upgrade jobranger"),
        ("pipx", "pipx upgrade jobranger"),
        ("pip", f"{sys.executable} -m pip install --upgrade jobranger"),
    ],
)
def test_should_print_upgrade_command_for_install_method(
    method: str, command: str
) -> None:
    recorder = Recorder(pypi_reply(NEWER))

    line = notice(recorder, {INSTALL_METHOD_ENV: method})

    assert (
        line == f"jobranger {NEWER} is out (you have {CURRENT}). Upgrade: {command}\n"
    )


@pytest.mark.parametrize(
    ("prefix", "env", "expected"),
    [
        ("/opt/uv-tools/jobranger", {"UV_TOOL_DIR": "/opt/uv-tools"}, "uv"),
        ("/home/user/.local/share/uv/tools/jobranger", {}, "uv"),
        ("/home/user/.local/share/pipx/venvs/jobranger", {}, "pipx"),
        (
            "/opt/tools/pipx-home/venvs/jobranger",
            {"PIPX_HOME": "/opt/tools/pipx-home"},
            "pipx",
        ),
        ("/usr/local", {}, "pip"),
        (
            "/home/user/.local/share/uv/tools/jobranger",
            {INSTALL_METHOD_ENV: "pipx"},
            "pipx",
        ),
        ("/usr/local", {INSTALL_METHOD_ENV: "docker"}, "pip"),
    ],
    ids=[
        "uv-tool-dir",
        "uv-default",
        "pipx-default",
        "pipx-home",
        "pip",
        "env-wins",
        "env-unknown",
    ],
)
def test_should_detect_install_method_from_prefix(
    prefix: str, env: dict[str, str], expected: str
) -> None:
    assert install_method(Path(prefix), env) == expected


@pytest.mark.parametrize(
    "elapsed", [timedelta(0), timedelta(hours=23)], ids=["same-time", "23h"]
)
def test_should_query_pypi_at_most_once_a_day(elapsed: timedelta, cache: Path) -> None:
    recorder = Recorder(pypi_reply(NEWER))
    latest_version(recorder.client(), cache, NOW)

    latest = latest_version(recorder.client(), cache, NOW + elapsed)

    assert (latest, len(recorder.requests)) == (Version(NEWER), 1)


def test_should_query_pypi_again_after_a_day(cache: Path) -> None:
    first = Recorder(pypi_reply("1.0.0"))
    second = Recorder(pypi_reply("2.0.0"))
    latest_version(first.client(), cache, NOW)

    latest = latest_version(second.client(), cache, NOW + timedelta(days=1))

    assert latest == Version("2.0.0")


def test_should_not_query_pypi_again_within_a_day_after_failure(cache: Path) -> None:
    failing = Recorder(time_out)
    recorder = Recorder(pypi_reply(NEWER))
    latest_version(failing.client(), cache, NOW)

    latest_version(recorder.client(), cache, NOW + timedelta(hours=1))

    assert recorder.requests == []


@pytest.mark.parametrize(
    "content",
    [
        "{not json",
        "[]",
        json.dumps({"checked_at": "2026-10-12T09:00:00", "latest": "1.0.0"}),
        json.dumps({"latest": "1.0.0"}),
        json.dumps({"checked_at": NOW.isoformat(), "latest": "not a version"}),
    ],
    ids=["not-json", "list", "naive-date", "no-checked-at", "invalid-version"],
)
def test_should_query_pypi_when_cache_is_corrupt(content: str, cache: Path) -> None:
    recorder = Recorder(pypi_reply(NEWER))
    cache.write_text(content)

    latest = latest_version(recorder.client(), cache, NOW)

    assert latest == Version(NEWER)


def test_should_stay_silent_when_cache_cannot_be_written(state_dir: Path) -> None:
    recorder = Recorder(pypi_reply(CURRENT))
    state_dir.chmod(0o500)

    line = notice(recorder, {})

    assert line == ""


def test_should_skip_check_when_not_interactive() -> None:
    recorder = Recorder(pypi_reply(NEWER))
    out = io.StringIO()

    maybe_notify(out, recorder.client(), {}, NOW, interactive=False)

    assert (out.getvalue(), recorder.requests) == ("", [])


def test_should_stay_silent_when_pypi_times_out() -> None:
    line = notice(Recorder(time_out), {})

    assert line == ""


@pytest.mark.parametrize(
    "body",
    [b"{not json", json.dumps({"info": {}}).encode()],
    ids=["not-json", "no-version"],
)
def test_should_stay_silent_when_pypi_reply_is_unreadable(body: bytes) -> None:
    recorder = Recorder(lambda request: httpx.Response(200, content=body))

    line = notice(recorder, {})

    assert line == ""


@pytest.mark.parametrize(
    "latest",
    ["99.0.0rc1", "0.0.1", CURRENT],
    ids=["prerelease", "older", "same"],
)
def test_should_stay_silent_without_newer_stable_release(latest: str) -> None:
    line = notice(Recorder(pypi_reply(latest)), {})

    assert line == ""


class StalledStream(httpx.SyncByteStream):
    def __init__(self, release: threading.Event) -> None:
        self.release = release

    def __iter__(self) -> Iterator[bytes]:
        yield b'{"info": '
        self.release.wait(STALL_S)
        yield json.dumps({"version": NEWER}).encode() + b"}"


def test_should_give_up_when_pypi_trickles_past_deadline(
    cache: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(version_check, "REQUEST_DEADLINE_S", 0.2)
    release = threading.Event()
    stalled = Recorder(
        lambda request: httpx.Response(200, stream=StalledStream(release))
    )
    started = time.monotonic()

    try:
        latest = latest_version(stalled.client(), cache, NOW)
        elapsed = time.monotonic() - started
    finally:
        release.set()

    assert latest is None
    assert elapsed < STALL_S


@pytest.mark.parametrize(
    ("extra", "expected"),
    [(0, Version(NEWER)), (1, None)],
    ids=["at-cap", "over-cap"],
)
def test_should_read_pypi_body_up_to_size_cap(
    extra: int, expected: Version | None, cache: Path
) -> None:
    head = b'{"info": {"version": "' + NEWER.encode() + b'"}, "padding": "'
    tail = b'"}'
    padding = b"x" * (MAX_BODY_BYTES - len(head) - len(tail) + extra)
    body = head + padding + tail
    recorder = Recorder(lambda request: httpx.Response(200, content=body))

    latest = latest_version(recorder.client(), cache, NOW)

    assert latest == expected


def test_should_leave_no_thread_holding_exit_when_pypi_stalls(
    cache: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(version_check, "REQUEST_DEADLINE_S", 0.2)
    release = threading.Event()
    stalled = Recorder(
        lambda request: httpx.Response(200, stream=StalledStream(release))
    )

    try:
        latest_version(stalled.client(), cache, NOW)
        blocking = [
            thread
            for thread in threading.enumerate()
            if not thread.daemon and thread is not threading.main_thread()
        ]
    finally:
        release.set()

    assert blocking == []


def test_should_query_pypi_when_cache_is_dated_in_future(cache: Path) -> None:
    recorder = Recorder(pypi_reply(NEWER))
    future = (NOW + timedelta(days=3650)).isoformat()
    cache.write_text(json.dumps({"checked_at": future, "latest": "1.0.0"}))

    latest = latest_version(recorder.client(), cache, NOW)

    assert latest == Version(NEWER)


def test_should_stay_silent_when_package_is_not_installed(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def missing(name: str) -> str:
        raise PackageNotFoundError(name)

    monkeypatch.setattr(version_check, "version", missing)

    line = notice(Recorder(pypi_reply(NEWER)), {})

    assert line == ""


def test_should_notify_for_real_sized_pypi_reply() -> None:
    releases = {
        f"0.{minor}.0": [
            {
                "filename": f"jobranger-0.{minor}.0-py3-none-any.whl",
                "packagetype": "bdist_wheel",
                "size": 120_000,
                "yanked": False,
                "digests": {"sha256": "0" * 64},
            }
        ]
        for minor in range(200)
    }
    body = {
        "info": {"version": NEWER, "description": "readme line\n" * 80_000},
        "releases": releases,
        "urls": releases["0.0.0"],
    }
    recorder = Recorder(lambda request: httpx.Response(200, json=body))

    line = notice(recorder, {INSTALL_METHOD_ENV: "uv"})

    assert line.startswith(f"jobranger {NEWER} is out")


def test_should_stay_silent_when_client_is_closed(
    capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(threading, "excepthook", threading.__excepthook__)
    client = Recorder(pypi_reply(NEWER)).client()
    client.close()
    out = io.StringIO()

    maybe_notify(out, client, {}, NOW, interactive=True)

    assert (out.getvalue(), capsys.readouterr().err) == ("", "")


@pytest.mark.parametrize(
    ("value", "requests"),
    [
        ("", 1),
        ("0", 1),
        (" False ", 1),
        ("NO", 1),
        ("off", 1),
        ("1", 0),
        ("true", 0),
        ("yes", 0),
    ],
)
def test_should_read_opt_out_env_as_switch(value: str, requests: int) -> None:
    recorder = Recorder(pypi_reply(NEWER))

    line = notice(recorder, {OPT_OUT_ENV: value})

    assert (len(recorder.requests), line == "") == (requests, requests == 0)
