# cli-python

Commands run from this folder.

Needs Python 3.12+ and uv.

```bash
uv sync --locked
uv run ruff check
uv run ruff format --check
uv run mypy
uv run pytest
uv run python scripts/mutate.py
```

The test suite points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and swaps the keyring for an in-memory one, so a test run leaves the real config and keychain alone.

The default matching prompt is `PROMPT_HEADER` and `BASE_RULES` in `src/jobranger/matching/prompt.py`. The tests compare it byte for byte with `tests/fixtures/main_prompt_header.txt` and `tests/fixtures/main_base_rules.txt`, so a prompt change updates both fixtures in the same commit. Any prompt change also changes the assessment version, and every stored ad is checked again on the next run.

After a prompt change, re-record the model replies in `tests/fixtures/matching_replies.json` that the matching-case tests read. The script makes one model call per case in `tests/fixtures/matching_cases.json`.

```bash
uv run python scripts/record_matching_replies.py claude -p --model claude-sonnet-5-5 --output-format json --tools ""
```

CV extraction parses PDF and DOCX files in a child process (`python -I -m jobranger.cv_worker`). The mutation run only sees code that runs inside pytest, so `tests/test_cv_worker.py` calls the worker logic in-process. `tests/test_cv.py` covers the child process path, with the stand-in workers in `tests/fake_cv_workers.py`.

`tests/fixtures/cv/cv_user.pdf` is built from `cv_user.typ`. `cv_user_scan.pdf` is page 1 of it as a 40 ppi image with no text layer.

```bash
typst compile tests/fixtures/cv/cv_user.typ tests/fixtures/cv/cv_user.pdf
```

Tests can only connect to loopback. Any other host fails with `NetworkBlockedError`, so stub HTTP with `httpx.MockTransport` or `httpx2.MockTransport`.
