from __future__ import annotations

from typing import TYPE_CHECKING

import anthropic
from openai.lib._parsing._completions import type_to_response_format_param

from jobranger.matching.assessment import AdAssessment

if TYPE_CHECKING:
    from collections.abc import Iterator

ROLE_TYPES = {
    "backend",
    "frontend",
    "fullstack",
    "mobile",
    "data_engineering",
    "devops_platform",
    "ml_ai",
    "embedded",
    "game",
    "general_software",
}


def _objects(schema: dict[str, object]) -> Iterator[dict[str, object]]:
    if schema.get("type") == "object":
        yield schema
    for value in schema.values():
        if isinstance(value, dict):
            yield from _objects(value)
        elif isinstance(value, list):
            for item in value:
                if isinstance(item, dict):
                    yield from _objects(item)


def _role_type_values(schema: dict[str, object]) -> set[str]:
    definitions = schema["$defs"]
    branches = schema["properties"]["role_type"]["anyOf"]
    named = [
        definitions[branch["$ref"].rsplit("/", 1)[-1]] if "$ref" in branch else branch
        for branch in branches
    ]
    return {value for branch in named for value in branch.get("enum", [])}


def test_should_require_every_field_in_anthropic_schema() -> None:
    schema = anthropic.transform_schema(AdAssessment)

    assert all(
        sorted(item["required"]) == sorted(item["properties"])
        for item in _objects(schema)
    )


def test_should_constrain_role_type_to_enum_in_anthropic_schema() -> None:
    schema = anthropic.transform_schema(AdAssessment)

    assert _role_type_values(schema) == ROLE_TYPES


def test_should_close_every_object_in_openai_schema() -> None:
    response_format = type_to_response_format_param(AdAssessment)
    schema = response_format["json_schema"]["schema"]

    assert all(item["additionalProperties"] is False for item in _objects(schema))
