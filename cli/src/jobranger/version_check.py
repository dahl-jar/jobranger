from __future__ import annotations

import contextlib
import json
import sys
import threading
from dataclasses import dataclass
from datetime import datetime, timedelta
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path
from typing import TYPE_CHECKING, Literal, TextIO, get_args

import httpx
from packaging.version import Version

from jobranger.paths import state_dir, write_private

if TYPE_CHECKING:
    from collections.abc import Mapping

PACKAGE = "jobranger"
CHECK_INTERVAL = timedelta(days=1)
REQUEST_DEADLINE_S = 2.0
MAX_BODY_BYTES = 8 * 1024 * 1024
PYPI_URL = f"https://pypi.org/pypi/{PACKAGE}/json"
CACHE_FILE = "version_check.json"
OPT_OUT_ENV = "JOBRANGER_NO_UPDATE_CHECK"
FALSE_VALUES = frozenset({"", "0", "false", "no", "off"})
INSTALL_METHOD_ENV = "JOBRANGER_INSTALL_METHOD"
UV_TOOL_DIR_ENV = "UV_TOOL_DIR"
PIPX_HOME_ENV = "PIPX_HOME"
UV_SEGMENTS = ("uv", "tools", PACKAGE)
PIPX_SEGMENTS = ("pipx", "venvs", PACKAGE)

InstallMethod = Literal["uv", "pipx", "pip"]
INSTALL_METHODS: tuple[InstallMethod, ...] = get_args(InstallMethod)


@dataclass(frozen=True)
class _Check:
    latest: Version | None


def install_method(prefix: Path, env: Mapping[str, str]) -> InstallMethod:
    declared = env.get(INSTALL_METHOD_ENV)
    for method in INSTALL_METHODS:
        if declared == method:
            return method
    if _under(prefix, env.get(UV_TOOL_DIR_ENV)) or _has_segments(prefix, UV_SEGMENTS):
        return "uv"
    if _under(prefix, env.get(PIPX_HOME_ENV)) or _has_segments(prefix, PIPX_SEGMENTS):
        return "pipx"
    return "pip"


def upgrade_command(method: InstallMethod) -> str:
    match method:
        case "uv":
            return f"uv tool upgrade {PACKAGE}"
        case "pipx":
            return f"pipx upgrade {PACKAGE}"
        case "pip":
            return f"{sys.executable} -m pip install --upgrade {PACKAGE}"


def latest_version(client: httpx.Client, cache: Path, now: datetime) -> Version | None:
    check = _cached_check(cache, now)
    if check is None:
        check = _Check(_fetch_latest(client))
        _save_check(cache, now, check)
    return check.latest


def maybe_notify(
    out: TextIO,
    client: httpx.Client,
    env: Mapping[str, str],
    now: datetime,
    *,
    interactive: bool,
) -> None:
    if not interactive or _opted_out(env):
        return
    try:
        current = Version(version(PACKAGE))
    except PackageNotFoundError:
        return
    latest = latest_version(client, state_dir() / CACHE_FILE, now)
    if latest is None or latest <= current:
        return
    command = upgrade_command(install_method(Path(sys.prefix), env))
    out.write(f"{PACKAGE} {latest} is out (you have {current}). Upgrade: {command}\n")


def _opted_out(env: Mapping[str, str]) -> bool:
    value = env.get(OPT_OUT_ENV)
    return value is not None and value.strip().lower() not in FALSE_VALUES


def _under(prefix: Path, root: str | None) -> bool:
    if not root:
        return False
    return prefix.is_relative_to(root)


def _has_segments(prefix: Path, segments: tuple[str, ...]) -> bool:
    parts = prefix.parts
    size = len(segments)
    return any(
        parts[start : start + size] == segments
        for start in range(len(parts) - size + 1)
    )


def _cached_check(cache: Path, now: datetime) -> _Check | None:
    try:
        entry = json.loads(cache.read_text(encoding="utf-8"))
        checked_at = datetime.fromisoformat(entry["checked_at"])
        latest = entry["latest"]
        check = _Check(None if latest is None else Version(latest))
        is_fresh = timedelta(0) <= now - checked_at < CHECK_INTERVAL
    except (OSError, ValueError, KeyError, TypeError):
        return None
    return check if is_fresh else None


def _fetch_latest(client: httpx.Client) -> Version | None:
    result: list[Version | None] = []
    worker = threading.Thread(
        target=lambda: result.append(_read_latest(client)), daemon=True
    )
    worker.start()
    worker.join(REQUEST_DEADLINE_S)
    return result[0] if result else None


def _read_latest(client: httpx.Client) -> Version | None:
    try:
        with client.stream("GET", PYPI_URL, timeout=REQUEST_DEADLINE_S) as response:
            response.raise_for_status()
            body = _read_capped(response)
        if body is None:
            return None
        latest = Version(json.loads(body)["info"]["version"])
    except Exception:
        return None
    return None if latest.is_prerelease else latest


def _read_capped(response: httpx.Response) -> bytes | None:
    body = bytearray()
    for chunk in response.iter_bytes():
        body.extend(chunk)
        if len(body) > MAX_BODY_BYTES:
            return None
    return bytes(body)


def _save_check(cache: Path, now: datetime, check: _Check) -> None:
    latest = None if check.latest is None else str(check.latest)
    entry = {"checked_at": now.isoformat(), "latest": latest}
    with contextlib.suppress(OSError):
        write_private(cache, json.dumps(entry))
