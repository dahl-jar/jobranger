from __future__ import annotations

import json
import math
from collections import Counter
from collections.abc import Iterator, Mapping, Sequence
from functools import cache
from importlib import resources
from typing import Any

from jsonschema import Draft202012Validator, validators
from jsonschema.exceptions import ValidationError as SchemaViolation
from jsonschema.protocols import Validator
from pydantic import BaseModel, ValidationError

from jobranger.contract.envelope import Envelope
from jobranger.contract.limits import (
    JSON_ENCODING,
    JSON_ENSURE_ASCII,
    JSON_SEPARATORS,
    MAX_INTEGER_DIGITS,
    MAX_JSON_DEPTH,
)
from jobranger.contract.payload import Payload

PACKAGE = "jobranger.contract"
DEFS = "$defs"
REF_PREFIX = "#/$defs/"
ROOT_PATH = "$"
INTEGER_LIMIT = 10**MAX_INTEGER_DIGITS
JSON_TYPES: dict[str, tuple[type, ...]] = {
    "null": (type(None),),
    "boolean": (bool,),
    "string": (str,),
    "array": (list,),
    "object": (dict,),
    "integer": (int,),
    "number": (int, float),
}


class ContractError(ValueError):
    def __init__(self, message: str, message_type: str | None = None) -> None:
        super().__init__(message)
        self.message_type = message_type


def canonical_item(value: object) -> object:
    if isinstance(value, float) and value.is_integer():
        return int(value)
    if isinstance(value, Mapping):
        return {key: canonical_item(item) for key, item in value.items()}
    if isinstance(value, list):
        return [canonical_item(item) for item in value]
    return value


def unique_items(
    validator: Validator, unique: bool, instance: object, schema: Mapping[str, Any]
) -> Iterator[SchemaViolation]:
    if not unique or not isinstance(instance, list):
        return
    seen: set[str] = set()
    for item in instance:
        key = json.dumps(canonical_item(item), sort_keys=True)
        if key in seen:
            yield SchemaViolation("array has non-unique elements")
            return
        seen.add(key)


ContractValidator = validators.extend(
    Draft202012Validator, {"uniqueItems": unique_items}
)


@cache
def schema_document(name: str) -> dict[str, Any]:
    text = (
        resources.files(PACKAGE)
        .joinpath(f"{name}.schema.json")
        .read_text(encoding="utf-8")
    )
    document: dict[str, Any] = json.loads(text)
    return document


@cache
def tagged_defs(schema: str, tag: str) -> dict[str, str]:
    document = schema_document(schema)
    names = [ref["$ref"].removeprefix(REF_PREFIX) for ref in document["oneOf"]]
    return {document[DEFS][name]["properties"][tag]["const"]: name for name in names}


@cache
def branch_validator(schema: str, name: str) -> Validator:
    document = schema_document(schema)
    branch = {
        "$schema": document["$schema"],
        DEFS: document[DEFS],
        "$ref": f"{REF_PREFIX}{name}",
    }
    validator: Validator = ContractValidator(
        branch, format_checker=Draft202012Validator.FORMAT_CHECKER
    )
    return validator


def reject_constant(token: str) -> object:
    message = f"non-standard number {token}"
    raise ValueError(message)


def unique_object(pairs: Sequence[tuple[str, object]]) -> dict[str, object]:
    document = dict(pairs)
    if len(document) != len(pairs):
        message = "duplicate object key"
        raise ValueError(message)
    return document


def check_text(value: str) -> None:
    if value.isascii():
        return
    try:
        value.encode(JSON_ENCODING)
    except UnicodeEncodeError as error:
        message = "string holds a lone surrogate"
        raise ValueError(message) from error


def check_scalar(value: object) -> None:
    if isinstance(value, str):
        check_text(value)
    elif isinstance(value, float) and not math.isfinite(value):
        message = "non-finite number"
        raise ValueError(message)
    elif (
        isinstance(value, int)
        and not isinstance(value, bool)
        and abs(value) >= INTEGER_LIMIT
    ):
        message = f"integer longer than {MAX_INTEGER_DIGITS} digits"
        raise ValueError(message)


def check_structure(document: object) -> None:
    if not isinstance(document, dict | list):
        check_scalar(document)
        return
    pending: list[tuple[dict[str, object] | list[object], int]] = [(document, 1)]
    while pending:
        container, depth = pending.pop()
        if depth > MAX_JSON_DEPTH:
            message = f"nesting deeper than {MAX_JSON_DEPTH}"
            raise ValueError(message)
        if isinstance(container, dict):
            for key in container:
                check_text(key)
        children = container.values() if isinstance(container, dict) else container
        for child in children:
            if isinstance(child, dict | list):
                pending.append((child, depth + 1))
            else:
                check_scalar(child)


def parse_json(raw: bytes) -> object:
    try:
        document = json.loads(
            raw.decode(JSON_ENCODING),
            object_pairs_hook=unique_object,
            parse_constant=reject_constant,
        )
        check_structure(document)
    except RecursionError as error:
        message = f"invalid JSON: nesting deeper than {MAX_JSON_DEPTH}"
        raise ContractError(message) from error
    except ValueError as error:
        message = f"invalid JSON: {error}"
        raise ContractError(message) from error
    return document


def resolve(node: Mapping[str, Any], defs: Mapping[str, Any]) -> Mapping[str, Any]:
    while "$ref" in node:
        siblings = {key: value for key, value in node.items() if key != "$ref"}
        node = {**defs[node["$ref"].removeprefix(REF_PREFIX)], **siblings}
    return node


def matches_type(instance: object, node: Mapping[str, Any]) -> bool:
    kind = node.get("type")
    if kind is None:
        return True
    return isinstance(instance, JSON_TYPES[kind])


def oversize(
    instance: object, node: Mapping[str, Any], defs: Mapping[str, Any], path: str
) -> tuple[str, str] | None:
    node = resolve(node, defs)
    if "anyOf" in node:
        return oversize_branch(instance, node["anyOf"], defs, path)
    if isinstance(instance, str) and len(instance) > node.get("maxLength", math.inf):
        return path, "maxLength"
    if isinstance(instance, list):
        return oversize_array(instance, node, defs, path)
    if isinstance(instance, dict):
        return oversize_object(instance, node, defs, path)
    return None


def oversize_branch(
    instance: object,
    branches: Sequence[Mapping[str, Any]],
    defs: Mapping[str, Any],
    path: str,
) -> tuple[str, str] | None:
    resolved = [resolve(branch, defs) for branch in branches]
    found = [
        oversize(instance, branch, defs, path)
        for branch in resolved
        if matches_type(instance, branch)
    ]
    return None if not found or None in found else found[0]


def oversize_object(
    instance: Mapping[str, object],
    node: Mapping[str, Any],
    defs: Mapping[str, Any],
    path: str,
) -> tuple[str, str] | None:
    for key, child in node.get("properties", {}).items():
        if key in instance:
            found = oversize(instance[key], child, defs, f"{path}.{key}")
            if found is not None:
                return found
    return None


def oversize_array(
    instance: list[object],
    node: Mapping[str, Any],
    defs: Mapping[str, Any],
    path: str,
) -> tuple[str, str] | None:
    if len(instance) > node.get("maxItems", math.inf):
        return path, "maxItems"
    items = node.get("items")
    if items is None:
        return None
    for index, item in enumerate(instance):
        found = oversize(item, items, defs, f"{path}[{index}]")
        if found is not None:
            return found
    return None


def hidden_count_problem(document: Mapping[str, Any]) -> tuple[str, str] | None:
    counts: Mapping[str, int] = document["hidden_counts"]
    kinds: list[str] = schema_document("payload")[DEFS]["HiddenKind"]["enum"]
    if sum(counts[kind] for kind in kinds) != document["hidden_total"]:
        return f"{ROOT_PATH}.hidden_counts", "hidden_total"
    listed = Counter(ad["kind"] for ad in document["hidden"])
    for kind, count in listed.items():
        if counts[kind] < count:
            return f"{ROOT_PATH}.hidden_counts.{kind}", "below_listed"
    return None


def contract_error(
    schema: str, path: object, rule: object, message_type: str | None
) -> ContractError:
    return ContractError(f"{schema} {path} fails {rule}", message_type)


def decode[ModelT: BaseModel](
    raw: bytes, schema: str, model: type[ModelT], tag: str
) -> ModelT:
    document = parse_json(raw)
    message_type = document.get(tag) if isinstance(document, dict) else None
    if not isinstance(message_type, str):
        message_type = None
    name = tagged_defs(schema, tag).get(message_type or "")
    if not isinstance(document, dict) or name is None:
        raise contract_error(schema, f"{ROOT_PATH}.{tag}", "const", message_type)
    defs = schema_document(schema)[DEFS]
    found = oversize(document, defs[name], defs, ROOT_PATH)
    if found is not None:
        raise contract_error(schema, found[0], found[1], message_type)
    violation = next(iter(branch_validator(schema, name).iter_errors(document)), None)
    if violation is not None:
        raise contract_error(
            schema, violation.json_path, violation.validator, message_type
        )
    problem = hidden_count_problem(document) if name == "AdsSnapshot" else None
    if problem is not None:
        raise contract_error(schema, problem[0], problem[1], message_type)
    try:
        return model.model_validate(document)
    except ValidationError as error:
        first = error.errors(include_input=False)[0]
        raise contract_error(
            schema, first["loc"], first["type"], message_type
        ) from error


def decode_payload(raw: bytes) -> Payload:
    return decode(raw, "payload", Payload, "type")


def decode_envelope(raw: bytes) -> Envelope:
    return decode(raw, "envelope", Envelope, "kind")


def encode(message: BaseModel) -> bytes:
    document = message.model_dump(mode="json", exclude_unset=True)
    text = json.dumps(
        document,
        ensure_ascii=JSON_ENSURE_ASCII,
        separators=JSON_SEPARATORS,
        allow_nan=False,
    )
    return text.encode(JSON_ENCODING)
