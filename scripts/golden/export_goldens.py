from __future__ import annotations

import io
import logging
import subprocess
import tarfile
import tempfile
from pathlib import Path

PINNED_COMMIT = "cb67b6236db52bc278f44469d62a89009cd9c0ef"
PYTHON_CLI = "cli-python"
REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_DIR = Path(__file__).resolve().parent
TESTS = REPO_ROOT / "cli/tests/Jobranger.Cli.Tests"
GOLDEN = TESTS / "Golden"
STORE_DB = TESTS / "TestData/Db/python-v2.db"
STORE_ROWS = GOLDEN / "Store/rows.json"
LINK_KEYS = TESTS / "TestData/Link"
PROVIDER_PROMPT = TESTS / "TestData/Providers/prompt.json"
PROVIDER_REQUESTS = GOLDEN / "Requests"
BUDGET_ESTIMATES = GOLDEN / "Budget/estimates.json"
MATCHING_INPUTS = TESTS / "TestData/Matching"
SOURCE = GOLDEN / "SOURCE"
DRIVERS = (
    ("store_rows.py", ("--db", STORE_DB, "--out", STORE_ROWS)),
    ("link_keys.py", ("--out", LINK_KEYS)),
    ("provider_requests.py", ("--prompt", PROVIDER_PROMPT, "--out", PROVIDER_REQUESTS)),
    ("budget_estimates.py", ("--out", BUDGET_ESTIMATES)),
    ("matching.py", ("--inputs", MATCHING_INPUTS, "--out", GOLDEN)),
)

log = logging.getLogger("export_goldens")


def extract_pinned(target: Path) -> Path:
    archive = subprocess.run(
        [
            "git",
            "-C",
            str(REPO_ROOT),
            "archive",
            "--format=tar",
            PINNED_COMMIT,
            PYTHON_CLI,
        ],
        check=True,
        capture_output=True,
    ).stdout
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        tar.extractall(target, filter="data")
    return target / PYTHON_CLI


def run_driver(project: Path, driver: str, arguments: tuple[object, ...]) -> None:
    command = [
        "uv",
        "run",
        "--frozen",
        "--no-dev",
        "--project",
        str(project),
        "python",
        str(SCRIPT_DIR / driver),
        *(str(argument) for argument in arguments),
    ]
    log.info("running %s at %s", driver, PINNED_COMMIT)
    subprocess.run(command, check=True, cwd=project)


def write_source() -> None:
    outputs = sorted(
        str(path.relative_to(TESTS))
        for path in (*GOLDEN.rglob("*"), *LINK_KEYS.rglob("*"), STORE_DB)
        if path.is_file() and path != SOURCE and "Embeddings" not in path.parts
    )
    lines = [
        f"{PYTHON_CLI} commit {PINNED_COMMIT}",
        "exported by scripts/golden/export_goldens.py",
        *outputs,
    ]
    SOURCE.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    with tempfile.TemporaryDirectory(prefix="jobranger-golden-") as temp:
        project = extract_pinned(Path(temp))
        for driver, arguments in DRIVERS:
            run_driver(project, driver, arguments)
    write_source()


if __name__ == "__main__":
    main()
