from __future__ import annotations

import io
import logging
import subprocess
import tarfile
import tempfile
from pathlib import Path

STORE_V2_COMMIT = "f43517d0060c4a998d8577a55452cced27a0ed5d"
UNSTAGED_MESSAGE = (
    "cli-python has unstaged or untracked changes. "
    "Run git add cli-python, then export again."
)
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
DRIVERS: tuple[tuple[str, tuple[object, ...], str | None], ...] = (
    ("store_rows.py", ("--db", STORE_DB, "--out", STORE_ROWS), STORE_V2_COMMIT),
    ("link_keys.py", ("--out", LINK_KEYS), None),
    (
        "provider_requests.py",
        ("--prompt", PROVIDER_PROMPT, "--out", PROVIDER_REQUESTS),
        None,
    ),
    ("budget_estimates.py", ("--out", BUDGET_ESTIMATES), None),
    ("matching.py", ("--inputs", MATCHING_INPUTS, "--out", GOLDEN), None),
)

log = logging.getLogger("export_goldens")


def git_output(*arguments: str) -> str:
    return subprocess.run(
        ["git", "-C", str(REPO_ROOT), *arguments],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def index_tree() -> str:
    unstaged = subprocess.run(
        ["git", "-C", str(REPO_ROOT), "diff", "--quiet", "--", PYTHON_CLI],
        check=False,
    )
    untracked = git_output(
        "ls-files", "--others", "--exclude-standard", "--", PYTHON_CLI
    )
    if unstaged.returncode != 0 or untracked:
        raise SystemExit(UNSTAGED_MESSAGE)
    return git_output("write-tree", f"--prefix={PYTHON_CLI}/")


def commit_tree(commit: str) -> str:
    return git_output("rev-parse", f"{commit}:{PYTHON_CLI}")


def extract(tree: str, target: Path) -> Path:
    archive = subprocess.run(
        [
            "git",
            "-C",
            str(REPO_ROOT),
            "archive",
            "--format=tar",
            f"--prefix={PYTHON_CLI}/",
            tree,
        ],
        check=True,
        capture_output=True,
    ).stdout
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        tar.extractall(target, filter="data")
    return target / PYTHON_CLI


def run_driver(
    project: Path, driver: str, arguments: tuple[object, ...], tree: str
) -> None:
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
    log.info("running %s at cli-python tree %s", driver, tree)
    subprocess.run(command, check=True, cwd=project)


def write_source(tree: str, store_tree: str) -> None:
    outputs = sorted(
        str(path.relative_to(TESTS))
        for path in (*GOLDEN.rglob("*"), *LINK_KEYS.rglob("*"), STORE_DB)
        if path.is_file() and path != SOURCE and "Embeddings" not in path.parts
    )
    lines = [
        f"{PYTHON_CLI} tree {tree}",
        f"store_rows.py {PYTHON_CLI} tree {store_tree} from commit {STORE_V2_COMMIT}",
        "exported by scripts/golden/export_goldens.py",
        *outputs,
    ]
    SOURCE.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    tree = index_tree()
    store_tree = commit_tree(STORE_V2_COMMIT)
    with tempfile.TemporaryDirectory(prefix="jobranger-golden-") as temp:
        projects = {
            None: (extract(tree, Path(temp) / "index"), tree),
            STORE_V2_COMMIT: (extract(store_tree, Path(temp) / "store"), store_tree),
        }
        for driver, arguments, pinned_commit in DRIVERS:
            project, project_tree = projects[pinned_commit]
            run_driver(project, driver, arguments, project_tree)
    write_source(tree, store_tree)


if __name__ == "__main__":
    main()
