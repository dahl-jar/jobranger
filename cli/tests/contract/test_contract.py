from __future__ import annotations

import json
from functools import cache
from pathlib import Path

import pytest
from jsonschema import Draft202012Validator

CONTRACTS = Path(__file__).resolve().parents[3] / "contracts"
FIXTURES = CONTRACTS / "fixtures"
INVALID_SUFFIX = "-invalid"


def load(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def fixtures(*folders: str) -> list[Path]:
    return [
        path
        for folder in folders
        for path in sorted((FIXTURES / folder).glob("*.json"))
    ]


def fixture_id(path: Path) -> str:
    return f"{path.parent.name}/{path.name}"


@cache
def validator(name: str) -> Draft202012Validator:
    schema = load(CONTRACTS / f"{name}.schema.json")
    Draft202012Validator.check_schema(schema)
    return Draft202012Validator(
        schema, format_checker=Draft202012Validator.FORMAT_CHECKER
    )


def schema_for(path: Path) -> Draft202012Validator:
    return validator(path.parent.name.removesuffix(INVALID_SUFFIX))


def golden_twin(path: Path) -> Path:
    stem = path.stem
    while "." in stem:
        stem = stem.rsplit(".", 1)[0]
        twin = FIXTURES / "payload" / f"{stem}.json"
        if twin.exists():
            return twin
    pytest.fail(f"no golden payload fixture is a prefix of {path.name}")


def changed_leaves(
    golden: object, other: object, prefix: tuple[str | int, ...] = ()
) -> list[tuple[str | int, ...]]:
    leaves: list[tuple[str | int, ...]] = []
    if isinstance(golden, dict) and isinstance(other, dict):
        for key in golden.keys() | other.keys():
            if key in golden and key in other:
                leaves += changed_leaves(golden[key], other[key], (*prefix, key))
            else:
                leaves.append((*prefix, key))
        return leaves
    if (
        isinstance(golden, list)
        and isinstance(other, list)
        and len(golden) == len(other)
    ):
        for index, (left, right) in enumerate(zip(golden, other, strict=True)):
            leaves += changed_leaves(left, right, (*prefix, index))
        return leaves
    if golden == other and type(golden) is type(other):
        return leaves
    return [prefix]


@pytest.mark.parametrize("path", fixtures("envelope", "payload"), ids=fixture_id)
def test_should_accept_golden_fixture(path: Path) -> None:
    errors = [error.message for error in schema_for(path).iter_errors(load(path))]

    assert errors == []


@pytest.mark.parametrize(
    "path", fixtures("envelope-invalid", "payload-invalid"), ids=fixture_id
)
def test_should_reject_invalid_fixture(path: Path) -> None:
    assert not schema_for(path).is_valid(load(path))


@pytest.mark.parametrize("path", fixtures("payload-invalid"), ids=fixture_id)
def test_should_change_one_leaf_of_golden_twin(path: Path) -> None:
    leaves = changed_leaves(load(golden_twin(path)), load(path))

    assert len(leaves) == 1, leaves
