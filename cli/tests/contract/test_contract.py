from __future__ import annotations

import json
from functools import cache
from pathlib import Path

import pytest
from jsonschema import Draft202012Validator

CONTRACTS_DIR = "contracts"
CONTRACTS_MARKER = Path(CONTRACTS_DIR) / "payload.schema.json"
INVALID_SUFFIX = "-invalid"


def find_contracts(start: Path) -> Path:
    root = next(
        (parent for parent in start.parents if (parent / CONTRACTS_MARKER).is_file()),
        None,
    )
    assert root is not None, f"no folder above {start} holds {CONTRACTS_MARKER}"
    return root / CONTRACTS_DIR


CONTRACTS = find_contracts(Path(__file__).resolve())
FIXTURES = CONTRACTS / "fixtures"


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


type Leaf = tuple[str | int, ...]


def changed_leaves(golden: object, other: object, prefix: Leaf = ()) -> list[Leaf]:
    if isinstance(golden, dict) and isinstance(other, dict):
        return _changed_keys(golden, other, prefix)
    if isinstance(golden, list) and isinstance(other, list):
        return _changed_items(golden, other, prefix)
    if golden == other and type(golden) is type(other):
        return []
    return [prefix]


def _changed_keys(
    golden: dict[str, object], other: dict[str, object], prefix: Leaf
) -> list[Leaf]:
    leaves: list[Leaf] = []
    for key in golden.keys() | other.keys():
        if key in golden and key in other:
            leaves += changed_leaves(golden[key], other[key], (*prefix, key))
        else:
            leaves.append((*prefix, key))
    return leaves


def _changed_items(
    golden: list[object], other: list[object], prefix: Leaf
) -> list[Leaf]:
    if len(golden) != len(other):
        return [prefix]
    return [
        leaf
        for index, (left, right) in enumerate(zip(golden, other, strict=True))
        for leaf in changed_leaves(left, right, (*prefix, index))
    ]


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
