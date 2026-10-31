from __future__ import annotations

import json
import re
import shutil
import sys
from collections.abc import Callable
from pathlib import Path

SCHEMAS = ("payload", "envelope")
PYTHON_OUT = Path("cli-python/src/jobranger/contract")
TYPESCRIPT_OUT = Path("frontend/src/features/relay/contract")
FIXTURE_SETS = (
    ("envelopeFixtures", "envelope", "Envelope"),
    ("payloadFixtures", "payload", "Payload"),
)
INT_LITERAL = re.compile(r"\bLiteral\[(-?[0-9]+)\]")
STRICT_INT_NAMES = ("Annotated", "Field", "StrictInt")
CONSTRAINED_FLOAT = re.compile(
    r"Annotated\[\s*StrictFloat(\s*\|\s*None)?,\s*Field\(([^)]*)\)\s*\]"
)
FLOAT_ROOT = "RootModel[StrictFloat]"
JSDOC_BLOCK = re.compile(r"^[ \t]*/\*\*\n(?:[ \t]*\*.*\n)*?[ \t]*\*/\n", re.MULTILINE)
NON_ASCII = re.compile(r"[^\x00-\x7f]")
INDENT = "  "


def strip_generated_header(text: str) -> str:
    lines = text.splitlines(keepends=True)
    while lines and (lines[0].startswith("#") or not lines[0].strip()):
        lines.pop(0)
    return "".join(lines)


def make_int_literals_strict(text: str) -> str:
    missing = [name for name in STRICT_INT_NAMES if name not in text]
    if INT_LITERAL.search(text) and missing:
        message = f"module lacks imports {missing} for strict int literals"
        raise ValueError(message)
    return INT_LITERAL.sub(r"Annotated[StrictInt, Field(ge=\1, le=\1)]", text)


def keep_whole_numbers(text: str) -> str:
    numbers = CONSTRAINED_FLOAT.sub(
        r"Annotated[StrictInt, Field(\2)] | Annotated[StrictFloat, Field(\2)]\1", text
    )
    return numbers.replace(FLOAT_ROOT, "RootModel[StrictInt | StrictFloat]")


def strip_jsdoc(text: str) -> str:
    return JSDOC_BLOCK.sub("", text)


def utf16_escape(match: re.Match[str]) -> str:
    return json.dumps(match.group())[1:-1]


def escape_non_ascii(text: str) -> str:
    return NON_ASCII.sub(utf16_escape, text)


def rewrite(path: Path, *transforms: Callable[[str], str]) -> None:
    text = path.read_text(encoding="utf-8")
    for transform in transforms:
        try:
            text = transform(text)
        except ValueError as error:
            error.add_note(f"while rewriting {path.name}")
            raise
    path.write_text(text, encoding="utf-8")


def typescript_literal(value: object, depth: int) -> str:
    text = json.dumps(value, indent=len(INDENT), ensure_ascii=True)
    return text.replace("\n", "\n" + INDENT * depth)


def fixture_entries(folder: Path, type_name: str) -> list[str]:
    return [
        f"{INDENT}{json.dumps(path.stem)}: "
        f"{typescript_literal(read_json(path), 1)}"
        f" satisfies {type_name},\n"
        for path in sorted(folder.glob("*.json"))
    ]


def write_ts_fixtures(fixtures_dir: Path, out: Path) -> None:
    imports = [
        f"import type {{ {type_name} }} from './{folder}.gen'\n"
        for _, folder, type_name in FIXTURE_SETS
    ]
    blocks = [
        f"export const {export} = {{\n"
        + "".join(fixture_entries(fixtures_dir / folder, type_name))
        + "}\n"
        for export, folder, type_name in FIXTURE_SETS
    ]
    out.write_text("".join(imports) + "\n" + "\n".join(blocks), encoding="utf-8")


def read_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def write_ts_schemas(messages: Path, out: Path) -> None:
    blocks = [
        f"export const {name}Schema = "
        f"{typescript_literal(read_json(messages / f'{name}.schema.json'), 0)}\n"
        for name in SCHEMAS
    ]
    out.write_text("\n".join(blocks), encoding="utf-8")


def copy_schemas(messages: Path, package: Path) -> None:
    for name in SCHEMAS:
        source = messages / f"{name}.schema.json"
        shutil.copyfile(source, package / source.name)


def main(root: Path) -> None:
    python_out = root / PYTHON_OUT
    typescript_out = root / TYPESCRIPT_OUT
    messages = root / "messages"
    for name in SCHEMAS:
        rewrite(
            python_out / f"{name}.py",
            strip_generated_header,
            make_int_literals_strict,
            keep_whole_numbers,
        )
        rewrite(typescript_out / f"{name}.gen.ts", strip_jsdoc)
    copy_schemas(messages, python_out)
    write_ts_schemas(messages, typescript_out / "schemas.gen.ts")
    rewrite(typescript_out / "validate.gen.js", escape_non_ascii)
    write_ts_fixtures(messages / "fixtures", typescript_out / "fixtures.gen.ts")


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve())
