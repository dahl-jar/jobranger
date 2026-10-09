from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

MUTANTS_DIR = Path(__file__).resolve().parent.parent / "mutants"


def main() -> int:
    shutil.rmtree(MUTANTS_DIR, ignore_errors=True)
    completed = subprocess.run(
        [sys.executable, "-m", "mutmut", "run", *sys.argv[1:]],
        cwd=MUTANTS_DIR.parent,
        check=False,
    )
    return completed.returncode


if __name__ == "__main__":
    sys.exit(main())
