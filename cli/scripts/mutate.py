from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

MUTANTS_DIR = Path(__file__).resolve().parent.parent / "mutants"
FORK_SAFETY_ENV = {"OBJC_DISABLE_INITIALIZE_FORK_SAFETY": "YES"}


def main() -> int:
    shutil.rmtree(MUTANTS_DIR, ignore_errors=True)
    completed = subprocess.run(
        [sys.executable, "-m", "mutmut", "run", *sys.argv[1:]],
        cwd=MUTANTS_DIR.parent,
        env={**os.environ, **FORK_SAFETY_ENV},
        check=False,
    )
    return completed.returncode


if __name__ == "__main__":
    sys.exit(main())
