from __future__ import annotations

import contextlib
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence

KEPT_VARIABLES = ("SYSTEMROOT",)
WORKDIR_PREFIX = "jobranger-cv-"
WORKDIR_MODE = 0o500
OWNER_WORKDIR_MODE = 0o700
STALE_WORKDIR_SECONDS = 60
CURRENT_UID = 0 if sys.platform == "win32" else os.getuid()


def run_child(
    argv: Sequence[str], data: bytes, timeout_seconds: float
) -> subprocess.CompletedProcess[bytes]:
    _remove_stale_workdirs()
    with tempfile.TemporaryDirectory(prefix=WORKDIR_PREFIX) as workdir:
        os.chmod(workdir, WORKDIR_MODE)
        return subprocess.run(
            argv,
            input=data,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            timeout=timeout_seconds,
            check=False,
            cwd=workdir,
            env={
                name: os.environ[name] for name in KEPT_VARIABLES if name in os.environ
            },
        )


def _remove_stale_workdirs() -> None:
    cutoff = time.time() - STALE_WORKDIR_SECONDS
    for path in Path(tempfile.gettempdir()).glob(f"{WORKDIR_PREFIX}*"):
        with contextlib.suppress(OSError):
            _remove_if_stale(path, cutoff)


def _remove_if_stale(path: Path, cutoff: float) -> None:
    info = path.lstat()
    if (
        stat.S_ISDIR(info.st_mode)
        and info.st_uid == CURRENT_UID
        and info.st_mtime < cutoff
    ):
        path.chmod(OWNER_WORKDIR_MODE)
        shutil.rmtree(path)
