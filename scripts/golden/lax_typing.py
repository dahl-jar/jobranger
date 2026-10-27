from __future__ import annotations

import argparse
import json
import tempfile
from pathlib import Path

from jobranger.matching.assessment import AdAssessment
from jobranger.matching.profile import Profile
from jobranger.settings import read_yaml
from pydantic import ValidationError

from matching import write_json

LAX_FOLDER = "Lax"
REPLY_YEARS_FILE = "reply-years.json"
PROFILE_FILE = "profile.json"
PROFILE_NAME = "profile.yaml"
CLASSIFICATION = "classification.json"
LOCATION_SEPARATOR = "."
DOCUMENT_FIELD = "document"
EVIDENCE_PREFIX = "evidence."
LIST_FIELDS = ("languages", "stack")
BLOCK_ITEM_FORM = "- x"
YEARS_FIELD = "professional_years"
EVIDENCE_LIST = "projects"
EVIDENCE_LINE_FIELDS = ("start", "end")

REPLY_YEARS = (
    "2",
    " 2 ",
    "\t2\n",
    "\xa02",
    "\u20032",
    "\u30002",
    "\x1c2",
    "\x1f2",
    "\x1f",
    "\x852",
    "\u200b2",
    "2\x00",
    "\x002",
    "2\x0b",
    "2\x0c",
    "2\r",
    "1_000",
    "1_0",
    "1.5_0",
    "1e5_0",
    "_1",
    "1_",
    "1__0",
    "1_.5",
    "1._5",
    "1e_5",
    "1e5",
    "1E5",
    "1e+5",
    ".5",
    "5.",
    "+2",
    "-2",
    "--2",
    "+",
    "",
    "  ",
    "inf",
    "Inf",
    "INF",
    "+inf",
    "-inf",
    "infinity",
    "Infinity",
    "-Infinity",
    "+INFINITY",
    "nan",
    "NaN",
    "-nan",
    "+nan",
    "nan_",
    "in_f",
    "infin",
    "0x10",
    "0o17",
    "0b1",
    "1e400",
    "-1e400",
    "1,5",
    "\uff11\uff12",
    "\u0663",
    "2 3",
    "2f",
    "2d",
    "1e5\x00",
    "true",
    "false",
    "None",
    "null",
    "0.1e-2",
    "1_000.5",
    "1e1_0",
    "\u0661\u0662",
    "2\u2028",
    "2\u2029",
    "\u2028 2",
    "\x1d2",
    "\x1e2",
    "\x1c",
    " 1_0 ",
    "\t1_0",
    "1_0\n",
    "\xa01_0",
    "2\x00\x00",
    "\u200b",
    "2\xa0",
    "\ufeff2",
    "\u180e2",
    "2\x1f",
    "1e5\x1c",
    "\t\x0b2",
    "nan ",
    "\u2003inf\u2003",
    "- inf",
    "i_nf",
    "na_n",
    "1e",
    "e5",
    "1.2.3",
    "0_0",
    "00_1",
    "1_e5",
    "\ud7ff",
)

FORMS = (
    "1",
    "0",
    "2",
    "-1",
    "+1",
    "1_000",
    "1_0",
    "0x10",
    "0X1F",
    "0o17",
    "017",
    "0b11",
    "08",
    "1:30",
    "1.0",
    "1.5",
    "3.",
    "'.5'",
    ".5",
    "1e3",
    "1E3",
    "1e-7",
    "1.5e3",
    "1e30",
    "+.5",
    "true",
    "True",
    "TRUE",
    "tRue",
    "false",
    "False",
    "yes",
    "Yes",
    "YES",
    "no",
    "No",
    "NO",
    "on",
    "On",
    "off",
    "Off",
    "y",
    "n",
    "t",
    "f",
    "null",
    "Null",
    "NULL",
    "~",
    "",
    "''",
    ".inf",
    ".Inf",
    ".INF",
    "-.inf",
    "+.inf",
    ".nan",
    ".NaN",
    ".NAN",
    "inf",
    "nan",
    "2024-01",
    "2024-01-01",
    "2024-01-01 10:00:00",
    "2024-01-01T10:00:00Z",
    "2024-1-1",
    "'1'",
    "'1.5'",
    "'0x10'",
    "'true'",
    "'yes'",
    "'null'",
    "'~'",
    "'.inf'",
    "'.nan'",
    "'2024-01-01'",
    "'2024-01'",
    "'1_000'",
    "' 3 '",
    "!!str 5",
    "!!int 5",
    "!!float 5",
    "!!bool true",
    "!!null",
    "!!int '5'",
    "!!timestamp 2024-01-01",
    "[1]",
    "{a: 1}",
    "abc",
    "- x",
)

SCALAR_TEMPLATES = {
    "summary": "summary: {v}\nprofessional_years: 1\n",
    "professional_years": "summary: s\nprofessional_years: {v}\n",
    "has_master": "summary: s\nprofessional_years: 1\nhas_master: {v}\n",
    "languages": "summary: s\nprofessional_years: 1\nlanguages: [{v}]\n",
    "stack": "summary: s\nprofessional_years: 1\nstack: [{v}]\n",
}
EVIDENCE_TEMPLATE = (
    "summary: s\nprofessional_years: 1\nprojects:\n"
    "  - id: {id}\n    name: {name}\n    facts: {facts}\n"
)
EVIDENCE_DEFAULTS = {"id": "abc", "name": "n", "facts": "x"}
EVIDENCE_FIELDS = ("id", "name", "facts", "start", "end")

EXTRA_FORMS = {
    "professional_years": (
        "-0",
        "'-0'",
        "-0.0",
        "1__0",
        "_1",
        "1_",
        "0o8",
        "0x",
        "0xg",
        "0b2",
        "1e400",
        "-1e400",
        "1e",
        "e1",
        "'1 000'",
        "'+3'",
        "'1,5'",
        "'1.5.2'",
        "1.5 \x23 c",
        "0.0",
        "'-.5'",
        "'  '",
        "'.'",
        "'1e'",
        "'\u0663'",
        "12345678901234567890",
        "79228162514264337593543950336",
        "'0b11'",
        "'0o17'",
        "'017'",
        "'0e0'",
        "'1_000.5'",
        "1_000.5",
        "'Infinity'",
        "'-inf'",
        "'+inf'",
        "'iNf'",
        "'NaN'",
        "'INF'",
        "'infinity'",
        "'3.'",
        ".5e3",
        "1" + "0" * 400,
        "0x" + "f" * 300,
        "!!null x",
        "!!bool yes",
        "!!bool True",
        "!!float .inf",
        "&a 5",
        "0.0e0",
        "1_e3",
        "1e3_",
        "_1.0",
        "1._5",
        "!!int 1_0",
        "!!int 017",
        "!!int 08",
        "!!float 1e3",
        "!!float .5",
        "!!int +5",
        "!!int -0",
        "! 5",
        "!foo 5",
        "!!int 0x10",
        "!!float '1_0'",
        "!!str",
        "<<",
        "=",
        "0b_1",
        "1.e5",
        "-0x10",
        "!!int abc",
        "!!float x",
        "!!timestamp x",
        "!!int",
        "-_",
        "0b_",
        "._",
        "2024-02-30",
        "0xaf",
        "0xAF",
        "0x1F",
        "!!int 0o8",
        "!!int 0b2",
    ),
    "has_master": (
        "' yes'",
        "'Y'",
        "'YES'",
        "'tRue'",
        "'TRUE'",
        "'On'",
        "'OFF'",
        "'T'",
        "'F'",
        "0.0",
        "-0",
        "-0.0",
        "'0.0'",
        "'1.0'",
        "'01'",
        "'00'",
        "01",
        "00",
        "'false'",
        "'False'",
        "'no'",
        "'N'",
        "'-1'",
        "'+1'",
        "!!bool 1",
        "1.",
        "0.",
        "'0'",
        "! yes",
        "!!bool false",
        "!!bool TRUE",
        "!!bool yes",
        "!!int 1",
        "!!float 1",
        "0b1",
        "0x1",
        "0o1",
        "-1e-400",
        "1e-400",
        "+0",
        "0b0",
        "-0x0",
        "0x01",
    ),
    "summary": (
        "! 5",
        "!foo x",
        "!!str",
        '""',
        "|\n  a\n  b",
        "&a x",
        "'a''b'",
        "? x",
        "<<",
        "=",
        "! 'x'",
        "!!str 'x'",
        "1_",
        ".5e3",
    ),
    "evidence.id": ("a", "ab", "' '", '"abc\\n"', "!!str abc"),
    "evidence.name": ("nULL",),
    "evidence.start": ("2024-13",),
    "evidence.end": ("202401", '"2024-01\\n"', "1999-00"),
}

DOCUMENTS = (
    "summary: s\nprofessional_years: 1\nhas_master:\n",
    "summary: s\nprofessional_years: 1\neducation:\nexperience: []\n",
    "summary: s\nprofessional_years: 1\nlanguages:\n",
    "summary: s\n",
    "professional_years: 1\n",
    "",
    "\x23 only\n",
    "- a\n",
    "summary: s\nsummary: t\nprofessional_years: 1\n",
    "summary: s\nprofessional_years: 1\nextra: 1\n",
    "summary: s\nprofessional_years: 1\nlanguages: abc\n",
    "summary: s\nprofessional_years: 1\nlanguages: [a, a]\n",
    "summary: s\nprofessional_years: 1\nlanguages: [a, ~, '']\n",
    "summary: s\nprofessional_years: 1\nprojects: {}\n",
    "summary: s\nprofessional_years: 1\nprojects:\n  -\n",
    "summary: s\nprofessional_years: 1\nprojects:\n  - id: abc\n    name: n\n"
    "    facts: x\n    start: 2025-01\n    end: 2024-01\n",
    "summary: s\nprofessional_years: 1\nprojects:\n  - id: abc\n    name: n\n",
    "summary: s\nprofessional_years: 1\nprojects:\n  - id: ''\n    name: n\n"
    "    facts: x\n  - id: ''\n    name: n\n    facts: x\n",
    "summary: s\nprofessional_years: 1\nstack: 'x'\n",
    "summary: s\nprofessional_years: 1\nstack: 5\n",
    "summary: s\nprofessional_years: 1\nstack: []\n",
    "summary: s\nprofessional_years: 1\nstack: {}\n",
    "summary: s\nprofessional_years: 1\nstack: ~\n",
    "summary: s\nprofessional_years: 1\nstack: ''\n",
)


def unique(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    seen: set[str] = set()
    for row in rows:
        if row["id"] in seen:
            msg = f"duplicate case id {row['id']}"
            raise ValueError(msg)
        seen.add(row["id"])
    return rows


def reply_years(payload: dict[str, object], text: str) -> str | None:
    payload["match"]["required_experience_years"] = text
    try:
        assessment = AdAssessment.model_validate_json(json.dumps(payload))
    except ValidationError:
        return None
    return repr(assessment.match.required_experience_years)


def export_reply_years(inputs: Path, out: Path) -> None:
    payload = json.loads((inputs / CLASSIFICATION).read_text("utf-8"))
    rows = unique(
        [
            {"id": ascii(text), "text": text, "years": reply_years(payload, text)}
            for text in REPLY_YEARS
        ]
    )
    write_json(out / LAX_FOLDER / REPLY_YEARS_FILE, rows)


def field_forms(field: str) -> list[str]:
    forms = [
        form for form in FORMS if not (field in LIST_FIELDS and form == BLOCK_ITEM_FORM)
    ]
    return forms + list(EXTRA_FORMS.get(field, ()))


def field_yaml(field: str, form: str) -> str:
    if field in SCALAR_TEMPLATES:
        return SCALAR_TEMPLATES[field].format(v=form)
    key = field.removeprefix(EVIDENCE_PREFIX)
    if key in EVIDENCE_LINE_FIELDS:
        return EVIDENCE_TEMPLATE.format(**EVIDENCE_DEFAULTS) + f"    {key}: {form}\n"
    return EVIDENCE_TEMPLATE.format(**{**EVIDENCE_DEFAULTS, key: form})


def profile_cases() -> list[dict[str, str]]:
    fields = [*SCALAR_TEMPLATES, *(EVIDENCE_PREFIX + key for key in EVIDENCE_FIELDS)]
    cases = [
        {
            "id": f"{field} {ascii(form)}",
            "field": field,
            "yaml": field_yaml(field, form),
        }
        for field in fields
        for form in field_forms(field)
    ]
    cases += [
        {"id": f"{DOCUMENT_FIELD} {ascii(text)}", "field": DOCUMENT_FIELD, "yaml": text}
        for text in DOCUMENTS
    ]
    return unique(cases)


def field_value(profile: Profile, field: str) -> object:
    if field == DOCUMENT_FIELD:
        return None
    if field == YEARS_FIELD:
        return repr(profile.professional_years)
    if field.startswith(EVIDENCE_PREFIX):
        evidence = getattr(profile, EVIDENCE_LIST)[0]
        return getattr(evidence, field.removeprefix(EVIDENCE_PREFIX))
    value = getattr(profile, field)
    return list(value) if field in LIST_FIELDS else value


def profile_expect(folder: Path, case: dict[str, str]) -> dict[str, object]:
    path = folder / PROFILE_NAME
    path.write_bytes(case["yaml"].encode("utf-8"))
    try:
        profile = Profile.model_validate(read_yaml(path))
    except ValidationError as error:
        return {
            "errors": [
                {
                    "loc": LOCATION_SEPARATOR.join(str(part) for part in item["loc"]),
                    "type": item["type"],
                }
                for item in error.errors()
            ]
        }
    except Exception as error:
        return {"crash": type(error).__name__}
    return {"value": field_value(profile, case["field"])}


def export_profile_forms(out: Path) -> None:
    with tempfile.TemporaryDirectory(prefix="jobranger-lax-") as temp:
        rows = [
            {**case, "expect": profile_expect(Path(temp), case)}
            for case in profile_cases()
        ]
    write_json(out / LAX_FOLDER / PROFILE_FILE, rows)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    arguments = parser.parse_args()
    export_reply_years(arguments.inputs, arguments.out)
    export_profile_forms(arguments.out)


if __name__ == "__main__":
    main()
