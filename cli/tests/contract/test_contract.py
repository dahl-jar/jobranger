from __future__ import annotations

import base64
import gc
import json
import math
import re
import shutil
import subprocess
import time
from datetime import UTC, datetime, timedelta
from functools import cache
from pathlib import Path
from typing import Any

import pytest
from jsonschema import Draft202012Validator
from pydantic import ValidationError
from ruamel.yaml import YAML

from jobranger.contract import limits
from jobranger.contract.codec import (
    ContractError,
    ContractValidator,
    decode_envelope,
    decode_payload,
    encode,
    oversize,
)
from jobranger.contract.envelope import Envelope, EnvelopeData
from jobranger.contract.fields import decode_base64, encode_url, parse_timestamp
from jobranger.contract.payload import Chunk, Payload

CONTRACTS_DIR = "contracts"
CONTRACTS_MARKER = Path(CONTRACTS_DIR) / "payload.schema.json"


def find_contracts(start: Path) -> Path:
    root = next(
        (parent for parent in start.parents if (parent / CONTRACTS_MARKER).is_file()),
        None,
    )
    assert root is not None, f"no folder above {start} holds {CONTRACTS_MARKER}"
    return root / CONTRACTS_DIR


CONTRACTS = find_contracts(Path(__file__).resolve())
ROOT = CONTRACTS.parent
FIXTURES = CONTRACTS / "fixtures"
WEB_CONTRACT = ROOT / "web" / "src" / "features" / "relay" / "contract"
CLI_CONTRACT = ROOT / "cli" / "src" / "jobranger" / "contract"
WORKFLOWS = ROOT / ".github" / "workflows"
RELAY_LIMITS = ROOT / "relay" / "src" / "Jobranger.Relay.Core" / "ContractLimits.cs"
BOUNDARY_BUDGET_SECONDS = 1.0
INVALID_SUFFIX = "-invalid"
GENERATED = [
    CLI_CONTRACT / "payload.py",
    CLI_CONTRACT / "envelope.py",
    CLI_CONTRACT / "payload.schema.json",
    CLI_CONTRACT / "envelope.schema.json",
    *sorted(WEB_CONTRACT.glob("*.gen.*")),
]
HAND_WRITTEN = [
    CLI_CONTRACT / "__init__.py",
    CLI_CONTRACT / "codec.py",
    CLI_CONTRACT / "fields.py",
    CLI_CONTRACT / "limits.py",
    WEB_CONTRACT / "limits.ts",
    WEB_CONTRACT / "validate.ts",
    WEB_CONTRACT / "scan.ts",
    WEB_CONTRACT / "contract.test.ts",
]
SIGNALR_INVOCATION = (
    '{{"type":1,"invocationId":"1","target":"Send","arguments":[{}]}}\x1e'
)
MAX_SEQ = 2**53 - 1
CHANNEL = "A" * 43
SID = "A" * 22
TEXT_FILLERS = {"escaped control": "\u0001", "astral": "\U0001f600"}
WORST_NAME_CHAR = "\U0001f600"
WORST_URL_CHAR = '"'
WORST_MONEY = 1.2345678901234567e-07
WORST_TIMESTAMP = "2026-10-09T03:00:00.123456789+02:00"
WORST_DAY = "2026-10-09"
WORST_MONTH = "2026-10"
USERINFO_MESSAGE = "invalid url: url userinfo is not printable ASCII without backslash"
URL_PREFIX = "https://jobs.example.test/"
MAX_LENGTH_URL = URL_PREFIX + "a" * (limits.URL_MAX_CHARS - len(URL_PREFIX))
PATTERN_LENGTH = re.compile(r"\{[0-9]+,([0-9]+)\}\$$")
RELAY_LIMIT = re.compile(r"public const (?:int|long) ([A-Za-z0-9]+) = ([0-9_]+);")
PASCAL_WORD = re.compile(r"(?<!^)(?=[A-Z])")
WEB_LIMIT = re.compile(r"^export const ([A-Z][A-Z0-9_]*) = ([0-9_]+)$", re.MULTILINE)


def load(path: Path) -> Any:
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
def schema(name: str) -> dict[str, Any]:
    loaded: dict[str, Any] = load(CONTRACTS / f"{name}.schema.json")
    return loaded


@cache
def validator(name: str) -> Draft202012Validator:
    Draft202012Validator.check_schema(schema(name))
    return Draft202012Validator(
        schema(name), format_checker=Draft202012Validator.FORMAT_CHECKER
    )


def schema_for(path: Path) -> Draft202012Validator:
    return validator(path.parent.name.removesuffix(INVALID_SUFFIX))


def payload_defs() -> dict[str, Any]:
    defs: dict[str, Any] = schema("payload")["$defs"]
    return defs


def message_names() -> list[str]:
    return [ref["$ref"].rsplit("/", 1)[1] for ref in schema("payload")["oneOf"]]


def command_names() -> list[str]:
    return [
        name for name in message_names() if "id" in payload_defs()[name]["properties"]
    ]


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


def canonical(document: object) -> str:
    return json.dumps(document, sort_keys=True, ensure_ascii=False)


def merge(base: dict[str, Any], change: dict[str, Any]) -> dict[str, Any]:
    merged = dict(base)
    for key, value in change.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            merged[key] = merge(base[key], value)
        else:
            merged[key] = value
    return merged


def ad_mark_with_extra(extra: str) -> bytes:
    raw = (FIXTURES / "payload" / "ad.mark.json").read_text(encoding="utf-8")
    return raw.replace('"v": 1,', f'"v": 1, "extra": {extra},').encode("utf-8")


def git_attribute(path: Path, name: str) -> str:
    git = shutil.which("git")
    assert git is not None
    completed = subprocess.run(
        [git, "-C", str(ROOT), "check-attr", name, "--", str(path.relative_to(ROOT))],
        capture_output=True,
        text=True,
        check=True,
    )
    return completed.stdout.rsplit(": ", 1)[1].strip()


def path_id(path: Path) -> str:
    return str(path.relative_to(ROOT))


def base64_chars(byte_count: int) -> int:
    return 4 * math.ceil(byte_count / 3)


def frame_bytes(plaintext_bytes: int) -> int:
    ciphertext = base64.urlsafe_b64encode(bytes(plaintext_bytes + limits.GCM_TAG_BYTES))
    envelope = EnvelopeData(
        v=1,
        channel=CHANNEL,
        sid=SID,
        seq=MAX_SEQ,
        kind="data",
        ciphertext=ciphertext.decode("ascii").rstrip("="),
    )
    frame = SIGNALR_INVOCATION.format(encode(envelope).decode("utf-8"))
    return len(frame.encode("utf-8"))


def non_null(node: dict[str, Any]) -> dict[str, Any]:
    branch: dict[str, Any] = next(
        item for item in node["anyOf"] if item != {"type": "null"}
    )
    return branch


def ref_name(node: dict[str, Any]) -> str | None:
    if "anyOf" in node:
        return ref_name(non_null(node))
    if "$ref" in node:
        name: str = node["$ref"].rsplit("/", 1)[1]
        return name
    return None


def resolve(node: dict[str, Any]) -> dict[str, Any]:
    if "anyOf" in node:
        return resolve(non_null(node))
    if "$ref" in node:
        siblings = {key: value for key, value in node.items() if key != "$ref"}
        return {**resolve(payload_defs()[ref_name(node) or ""]), **siblings}
    return node


def worst_string(
    name: str | None, node: dict[str, Any], filler: str, index: int
) -> str:
    size = node.get("maxLength", 0)
    special = {
        "MessageId": f"00000000-0000-4000-8000-{index:012d}",
        "Timestamp": WORST_TIMESTAMP,
        "Day": WORST_DAY,
        "ClockTime": f"{index:02d}:00",
        "Url": "https://" + WORST_URL_CHAR * (size - len("https://")),
        "Email": "a" * (size - len("@a.bc")) + "@a.bc",
        "FileName": WORST_NAME_CHAR * (size - len(".pdf")) + ".pdf",
        "DocId": ("doc_" + "a" * size)[:size],
        "Base64Data": "A" * (size - size % 4),
    }
    if name in special:
        return special[name]
    pattern = node.get("pattern", "")
    if pattern.startswith("^[0-9]{4}"):
        return WORST_MONTH
    bounded = PATTERN_LENGTH.search(pattern)
    if bounded:
        return WORST_URL_CHAR * int(bounded.group(1))
    assert "maxLength" in node, f"unbounded string {node}"
    return filler * size


def worst_scalar(
    name: str | None, node: dict[str, Any], filler: str, index: int
) -> object:
    if "const" in node:
        return node["const"]
    if "enum" in node:
        return max(node["enum"], key=len)
    kind = node.get("type")
    if kind == "string":
        return worst_string(name, node, filler, index)
    if kind == "integer":
        return node["maximum"]
    if kind == "number":
        return WORST_MONEY
    assert kind == "boolean", f"unhandled schema {node}"
    return False


def worst_array(node: dict[str, Any], filler: str) -> list[object]:
    items = node["items"]
    item_node = resolve(items)
    if node.get("uniqueItems") and "enum" in item_node:
        return list(item_node["enum"])
    assert "maxItems" in node, f"unbounded array {node}"
    return [worst_case(items, filler, position) for position in range(node["maxItems"])]


def worst_case(node: dict[str, Any], filler: str, index: int = 0) -> object:
    resolved = resolve(node)
    if "const" in resolved or "enum" in resolved:
        return worst_scalar(ref_name(node), resolved, filler, index)
    if resolved.get("type") == "object":
        return {
            key: worst_case(value, filler, index)
            for key, value in resolved.get("properties", {}).items()
        }
    if resolved.get("type") == "array":
        return worst_array(resolved, filler)
    return worst_scalar(ref_name(node), resolved, filler, index)


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


@pytest.mark.parametrize("path", fixtures("payload-invalid"), ids=fixture_id)
def test_should_reject_invalid_payload_at_boundary(path: Path) -> None:
    with pytest.raises(ContractError):
        decode_payload(path.read_bytes())


@pytest.mark.parametrize("path", fixtures("envelope-invalid"), ids=fixture_id)
def test_should_reject_invalid_envelope_at_boundary(path: Path) -> None:
    with pytest.raises(ContractError):
        decode_envelope(path.read_bytes())


@pytest.mark.parametrize("path", fixtures("payload"), ids=fixture_id)
def test_should_round_trip_golden_payload(path: Path) -> None:
    message = decode_payload(path.read_bytes())

    assert canonical(json.loads(encode(message))) == canonical(load(path))


@pytest.mark.parametrize("path", fixtures("envelope"), ids=fixture_id)
def test_should_round_trip_golden_envelope(path: Path) -> None:
    envelope = decode_envelope(path.read_bytes())

    assert canonical(json.loads(encode(envelope))) == canonical(load(path))


def test_should_have_one_golden_per_payload_type() -> None:
    schema_types = {
        payload_defs()[name]["properties"]["type"]["const"] for name in message_names()
    }

    golden_types = {load(path)["type"] for path in fixtures("payload")}

    assert golden_types == schema_types


def test_should_report_errors_only_for_tagged_message() -> None:
    raw = load(FIXTURES / "payload-invalid" / "ad.mark.bad-mark.json")

    with pytest.raises(ValidationError) as caught:
        Payload.model_validate(raw)

    assert {error["loc"][0] for error in caught.value.errors()} == {"ad.mark"}


def test_should_drop_unknown_fields() -> None:
    raw = {**load(FIXTURES / "payload" / "ad.mark.json"), "extra": 1}

    message = decode_payload(json.dumps(raw).encode("utf-8"))

    assert "extra" not in message.model_dump(mode="json")


@pytest.mark.parametrize(
    ("fixture", "field", "value"),
    [
        ("payload/ad.mark.json", "v", True),
        ("envelope/hello.json", "seq", False),
        ("payload/run.cancel.json", "run_id", "42"),
        ("payload/run.finished.json", "budget_paused", 0),
        ("payload/run.finished.json", "cost_usd", "0.41"),
    ],
)
def test_should_reject_coerced_value_in_model(
    fixture: str, field: str, value: object
) -> None:
    raw = {**load(FIXTURES / fixture), field: value}
    model = Envelope if fixture.startswith("envelope") else Payload

    with pytest.raises(ValidationError):
        model.model_validate(raw)


@pytest.mark.parametrize(
    "extra",
    [
        "[" * (limits.MAX_JSON_DEPTH - 1) + "]" * (limits.MAX_JSON_DEPTH - 1),
        "-" + "9" * limits.MAX_INTEGER_DIGITS,
    ],
    ids=["nesting at max depth", "integer at max digits"],
)
def test_should_accept_unknown_field_at_limit(extra: str) -> None:
    message = decode_payload(ad_mark_with_extra(extra))

    assert message.root.type == "ad.mark"


@pytest.mark.parametrize(
    ("extra", "reason"),
    [
        ("NaN", "non-standard number"),
        ("[" * limits.MAX_JSON_DEPTH + "]" * limits.MAX_JSON_DEPTH, "nesting deeper"),
        ("[" * 1_000_000 + "]" * 1_000_000, "nesting deeper"),
        ("1" + "0" * limits.MAX_INTEGER_DIGITS, "integer longer than"),
        ('"\\ud800"', "string holds a lone surrogate"),
        ('{"\\ud800": 1}', "string holds a lone surrogate"),
        ("1e400", "non-finite number"),
        ("}", "Expecting"),
    ],
    ids=[
        "nan",
        "depth",
        "recursion",
        "long integer",
        "lone surrogate",
        "lone surrogate key",
        "infinite",
        "syntax",
    ],
)
def test_should_name_parse_failure_reason(extra: str, reason: str) -> None:
    with pytest.raises(ContractError, match=f"invalid JSON: {reason}"):
        decode_payload(ad_mark_with_extra(extra))


def test_should_reject_non_finite_top_level_number() -> None:
    with pytest.raises(ContractError, match="^invalid JSON: non-finite number$"):
        decode_payload(b"1e400")


@pytest.mark.parametrize(
    ("document", "message_type"),
    [({"type": "bogus", "v": 1}, "bogus"), ({"type": 5, "v": 1}, None)],
    ids=["unknown type", "non-string type"],
)
def test_should_reject_untagged_message(
    document: dict[str, object], message_type: str | None
) -> None:
    with pytest.raises(ContractError) as caught:
        decode_payload(json.dumps(document).encode("utf-8"))

    assert (str(caught.value), caught.value.message_type) == (
        "payload $.type fails const",
        message_type,
    )


@pytest.mark.parametrize("value", [[1], {"a": 1}], ids=["array", "object"])
def test_should_reject_container_in_string_field(value: object) -> None:
    raw = {**load(FIXTURES / "payload" / "ad.mark.json"), "ad_id": value}

    with pytest.raises(ContractError, match=r"^payload \$\.ad_id fails type$"):
        decode_payload(json.dumps(raw).encode("utf-8"))


@pytest.mark.parametrize(
    ("node", "instance", "expected"),
    [
        ({"$ref": "#/$defs/AdId"}, "a" * 201, ("$", "maxLength")),
        ({"$ref": "#/$defs/Places"}, ["a"] * 20, None),
        ({"$ref": "#/$defs/Places"}, ["a"] * 21, ("$", "maxItems")),
        ({"$ref": "#/$defs/Places"}, ["a" * 81], ("$[0]", "maxLength")),
        (
            {"anyOf": [{"$ref": "#/$defs/Url"}, {"type": "null"}]},
            "a" * (limits.URL_MAX_CHARS + 1),
            ("$", "maxLength"),
        ),
        ({"anyOf": [{"type": "null"}]}, 5, None),
        ({"anyOf": [{"maxLength": 1}]}, "ab", ("$", "maxLength")),
    ],
    ids=[
        "string",
        "array at max items",
        "array over max items",
        "array item",
        "nullable branch",
        "no matching branch",
        "untyped branch",
    ],
)
def test_should_find_oversize_value(
    node: dict[str, Any], instance: object, expected: tuple[str, str] | None
) -> None:
    assert oversize(instance, node, payload_defs(), "$") == expected


def test_should_reject_duplicate_message_key() -> None:
    raw = (FIXTURES / "payload" / "ad.mark.json").read_text(encoding="utf-8")
    duplicated = raw.replace('"v": 1,', '"v": 1, "mark": "bogus",')

    with pytest.raises(ContractError, match="invalid JSON: duplicate object key"):
        decode_payload(duplicated.encode("utf-8"))


@pytest.mark.parametrize(("field", "size"), [("subfields", 40_000), ("places", 10_000)])
def test_should_reject_oversize_array_within_time_budget(field: str, size: int) -> None:
    raw = load(FIXTURES / "payload" / "config.update.full.json")
    raw["patch"]["targets"][field] = [{"a": index} for index in range(size)]
    encoded = json.dumps(raw).encode("utf-8")
    gc.collect()
    started = time.perf_counter()

    with pytest.raises(ContractError):
        decode_payload(encoded)
    elapsed = time.perf_counter() - started

    assert elapsed < BOUNDARY_BUDGET_SECONDS


@pytest.mark.parametrize(
    ("items", "unique"),
    [
        ([{"a": index} for index in range(20_000)], True),
        ([{"a": index} for index in range(20_000)] + [{"a": 1.0}], False),
        ([1, True], True),
        ([[1], [2]], True),
        ([{"a": 1, "b": 2}, {"b": 2, "a": 1}], False),
    ],
    ids=[
        "distinct objects",
        "equal numbers",
        "bool and int",
        "distinct nested arrays",
        "reordered keys",
    ],
)
def test_should_check_unique_items_in_linear_time(
    items: list[object], unique: bool
) -> None:
    validator = ContractValidator({"uniqueItems": True})
    gc.collect()
    started = time.perf_counter()

    valid = validator.is_valid(items)
    elapsed = time.perf_counter() - started

    assert (valid, elapsed < BOUNDARY_BUDGET_SECONDS) == (unique, True)


def test_should_allow_repeated_items_if_unique_items_false() -> None:
    assert ContractValidator({"uniqueItems": False}).is_valid([1, 1])


def test_should_reject_duplicate_places() -> None:
    raw = load(FIXTURES / "payload" / "config.update.full.json")
    raw["patch"]["targets"]["places"] = ["Bergen", "Oslo", "Bergen"]

    with pytest.raises(ContractError, match="uniqueItems"):
        decode_payload(json.dumps(raw).encode("utf-8"))


@pytest.mark.parametrize(
    ("folder", "fixture", "change", "message", "message_type"),
    [
        (
            "envelope",
            "data.json",
            {"seq": 2**53},
            "envelope $.seq fails maximum",
            "data",
        ),
        (
            "payload",
            "config.update.full.json",
            {"patch": {"targets": {"subfields": [{"a": 1}] * 10}}},
            "payload $.patch.targets.subfields fails maxItems",
            "config.update",
        ),
    ],
)
def test_should_name_failing_path(
    folder: str, fixture: str, change: dict[str, Any], message: str, message_type: str
) -> None:
    raw = merge(load(FIXTURES / folder / fixture), change)
    decode = decode_envelope if folder == "envelope" else decode_payload

    with pytest.raises(ContractError) as caught:
        decode(json.dumps(raw).encode("utf-8"))

    assert (str(caught.value), caught.value.message_type) == (message, message_type)


def test_should_reject_whole_float_in_integer_field() -> None:
    raw = (FIXTURES / "payload" / "run.cancel.json").read_text(encoding="utf-8")
    whole_float = raw.replace('"run_id": 42', '"run_id": 42.0').encode("utf-8")

    with pytest.raises(ContractError) as caught:
        decode_payload(whole_float)

    assert (str(caught.value), caught.value.message_type) == (
        "payload ('run.cancel', 'run_id') fails int_type",
        "run.cancel",
    )


def test_should_name_message_type_in_contract_error() -> None:
    raw = (FIXTURES / "payload-invalid" / "ad.mark.bad-mark.json").read_bytes()

    with pytest.raises(ContractError) as caught:
        decode_payload(raw)

    assert caught.value.message_type == "ad.mark"


def test_should_encode_whole_money_without_fraction() -> None:
    raw = {**load(FIXTURES / "payload" / "run.finished.json"), "cost_usd": 20}
    message = Payload.model_validate(raw)

    assert b'"cost_usd":20,' in encode(message)


def test_should_encode_compact_utf8() -> None:
    message = Payload.model_validate(
        {**load(FIXTURES / "payload" / "ad.mark.json"), "ad_id": "bø"}
    )

    encoded = encode(message)

    assert encoded == (
        b'{"type":"ad.mark","v":1,"id":"00000000-0000-4000-8000-000000000003",'
        b'"ad_id":"b\xc3\xb8","mark":"not_for_me"}'
    )


@pytest.mark.parametrize("filler", TEXT_FILLERS.values(), ids=TEXT_FILLERS.keys())
@pytest.mark.parametrize("name", message_names())
def test_should_fit_worst_case_message_in_chunk_limit(name: str, filler: str) -> None:
    message = Payload.model_validate(worst_case(payload_defs()[name], filler))

    chunks = math.ceil(len(encode(message)) / limits.CHUNK_DATA_MAX_BYTES)

    assert chunks <= limits.CHUNK_MAX_COUNT


def test_should_fit_largest_chunk_in_one_frame() -> None:
    chunk = Chunk(
        type="chunk",
        v=1,
        chunk_id="00000000-0000-4000-8000-000000000001",
        index=limits.CHUNK_MAX_COUNT - 1,
        count=limits.CHUNK_MAX_COUNT,
        data=base64.b64encode(bytes(limits.CHUNK_DATA_MAX_BYTES)).decode("ascii"),
    )

    size = frame_bytes(len(encode(chunk)))

    assert size <= limits.FRAME_MAX_BYTES


def test_should_fit_largest_single_payload_in_one_frame() -> None:
    size = frame_bytes(limits.CHUNK_THRESHOLD_BYTES)

    assert size <= limits.FRAME_MAX_BYTES


@pytest.mark.parametrize(
    ("actual", "expected"),
    [
        (
            base64_chars(limits.CHUNK_DATA_MAX_BYTES),
            payload_defs()["Chunk"]["properties"]["data"]["maxLength"],
        ),
        (
            limits.CHUNK_MAX_COUNT,
            payload_defs()["Chunk"]["properties"]["count"]["maximum"],
        ),
        (
            limits.CHUNK_MAX_COUNT - 1,
            payload_defs()["Chunk"]["properties"]["index"]["maximum"],
        ),
        (
            limits.CIPHERTEXT_B64_MAX_CHARS,
            schema("envelope")["$defs"]["Base64Url"]["maxLength"],
        ),
        (
            limits.CV_MAX_BYTES,
            payload_defs()["CvUpload"]["properties"]["size_bytes"]["maximum"],
        ),
        (
            base64_chars(limits.CV_MAX_BYTES),
            payload_defs()["CvUpload"]["properties"]["content_b64"]["maxLength"],
        ),
        (
            base64_chars(limits.CV_MAX_BYTES),
            payload_defs()["ContextUpload"]["properties"]["content_b64"]["maxLength"],
        ),
        (limits.URL_MAX_CHARS, payload_defs()["Url"]["maxLength"]),
        (
            limits.CONTRACT_VERSION,
            max(
                payload_defs()[name]["properties"]["v"]["const"]
                for name in message_names()
            ),
        ),
    ],
)
def test_should_match_limits_with_schema(actual: int, expected: int) -> None:
    assert actual == expected


def test_should_match_web_limits_with_cli_limits() -> None:
    text = (WEB_CONTRACT / "limits.ts").read_text(encoding="utf-8")
    cli = {
        name: value
        for name, value in vars(limits).items()
        if name.isupper() and type(value) is int
    }

    web = {name: int(value) for name, value in WEB_LIMIT.findall(text)}

    assert web == cli


def test_should_match_relay_limits_with_cli_limits() -> None:
    text = RELAY_LIMITS.read_text(encoding="utf-8")

    relay = {
        PASCAL_WORD.sub("_", name).upper(): int(value)
        for name, value in RELAY_LIMIT.findall(text)
    }

    assert relay
    assert relay == {name: getattr(limits, name) for name in relay}


def test_should_accept_saved_without_recheck() -> None:
    raw = {
        key: value
        for key, value in load(FIXTURES / "payload" / "saved.json").items()
        if key != "recheck"
    }

    message = decode_payload(json.dumps(raw).encode("utf-8"))

    assert message.model_dump(mode="json", exclude_unset=True) == raw


@pytest.mark.parametrize("name", command_names())
def test_should_require_id_on_every_command(name: str) -> None:
    assert "id" in payload_defs()[name]["required"]


def test_should_type_reply_ref_like_command_id() -> None:
    defs = payload_defs()
    replies = [name for name in message_names() if "ref" in defs[name]["properties"]]

    command_ids = {ref_name(defs[name]["properties"]["id"]) for name in command_names()}
    reply_refs = {ref_name(defs[name]["properties"]["ref"]) for name in replies}

    assert command_ids == reply_refs


@pytest.mark.parametrize(
    "path",
    [
        CLI_CONTRACT / "payload.py",
        CLI_CONTRACT / "envelope.py",
        *sorted(WEB_CONTRACT.glob("*.gen.*")),
    ],
    ids=lambda path: path.name,
)
def test_should_keep_generated_code_free_of_comments(path: Path) -> None:
    lines = path.read_text(encoding="utf-8").splitlines()

    comments = [
        line for line in lines if line.lstrip().startswith(("#", "//", "/*", "*"))
    ]

    assert comments == []


def test_should_decode_canonical_base64() -> None:
    assert decode_base64("AAE=", max_bytes=2) == b"\x00\x01"


@pytest.mark.parametrize(
    ("text", "max_bytes", "reason"),
    [
        ("AAF=", 2, "non-canonical base64"),
        ("AAE", 2, "text is not standard padded base64"),
        ("AA*E=", 2, "text is not standard padded base64"),
        ("AAEC", 2, "decoded 3 bytes, max 2"),
    ],
    ids=[
        "non-zero padding bits",
        "missing padding",
        "non-alphabet char",
        "over max bytes",
    ],
)
def test_should_reject_base64(text: str, max_bytes: int, reason: str) -> None:
    with pytest.raises(ContractError, match=f"^{reason}$"):
        decode_base64(text, max_bytes=max_bytes)


def test_should_parse_timestamp_with_offset() -> None:
    parsed = parse_timestamp("2026-10-09T03:00:00.5+02:00")

    assert parsed == datetime(2026, 10, 9, 1, 0, 0, 500_000, tzinfo=UTC)
    assert parsed.utcoffset() == timedelta(hours=2)


@pytest.mark.parametrize(
    "text",
    ["2026-02-30T00:00:00Z", "2026-10-09 03:00:00Z", "2026-10-09T03:00:00"],
    ids=["impossible date", "space separator", "no offset"],
)
def test_should_reject_timestamp(text: str) -> None:
    with pytest.raises(
        ContractError, match="^timestamp is not an RFC 3339 date-time with offset$"
    ):
        parse_timestamp(text)


@pytest.mark.parametrize(
    ("url", "expected"),
    [
        (
            "https://jobs.example.test/stilling/utvikler bø",
            "https://jobs.example.test/stilling/utvikler%20b%C3%B8",
        ),
        ("https://bø.example.test/a", "https://xn--b-5ga.example.test/a"),
        ("https://jobs.example.test/a%20b?q=1", "https://jobs.example.test/a%20b?q=1"),
        ("http://[::1]:8080/a", "http://[::1]:8080/a"),
        ("http://user@[::1]/a", "http://user@[::1]/a"),
        ("https://a@b@jobs.example.test/", "https://a@b@jobs.example.test/"),
        (MAX_LENGTH_URL, MAX_LENGTH_URL),
        (
            "https://user:pw@Jobs.Example.test:443/",
            "https://user:pw@jobs.example.test:443/",
        ),
    ],
    ids=[
        "path",
        "host",
        "already encoded",
        "ipv6 host with port",
        "ipv6 host with userinfo",
        "at sign in userinfo",
        "at max length",
        "userinfo",
    ],
)
def test_should_percent_encode_url(url: str, expected: str) -> None:
    assert encode_url(url) == expected


@pytest.mark.parametrize(
    ("url", "message"),
    [
        (
            "javascript:alert(1)",
            "invalid url: url scheme 'javascript' is not http or https",
        ),
        (MAX_LENGTH_URL + "a", "url has 2049 chars, max 2048"),
        ("https:///a", "invalid url: url has no host"),
        ("http://[::1", "invalid url: Invalid IPv6 URL"),
        (
            "https://jobs.example.test/\ud800",
            "invalid url: 'utf-8' codec can't encode character '\\ud800' "
            "in position 26: surrogates not allowed",
        ),
        ("http://exa mple.test/", "invalid url: url host is not a domain name"),
        ("https://user:pa ss@jobs.example.test/", USERINFO_MESSAGE),
        ("https://\u00fc@jobs.example.test/", USERINFO_MESSAGE),
        ("https://attacker.example.test\\@jobs.example.test/", USERINFO_MESSAGE),
        ("https://jobs.example.test:99999/", "invalid url: Port out of range 0-65535"),
        ("https://-a.test/", "invalid url: url host is not a domain name"),
        ("https://a-.test/", "invalid url: url host is not a domain name"),
        ("https://jobs.example.test:0/", "invalid url: url port is zero"),
        ("https://[::1%25eth0]/", "invalid url: url host carries an IPv6 zone id"),
    ],
    ids=[
        "script scheme",
        "over max length",
        "no host",
        "unclosed bracket host",
        "lone surrogate",
        "space in host",
        "space in userinfo",
        "non-ascii userinfo",
        "backslash in userinfo",
        "port over max",
        "label leading hyphen",
        "label trailing hyphen",
        "port zero",
        "ipv6 zone id",
    ],
)
def test_should_reject_url(url: str, message: str) -> None:
    with pytest.raises(ContractError, match=f"^{re.escape(message)}$"):
        encode_url(url)


@pytest.mark.parametrize("path", GENERATED, ids=path_id)
def test_should_mark_generated_output_as_generated(path: Path) -> None:
    assert git_attribute(path, "linguist-generated") == "true"


@pytest.mark.parametrize("path", GENERATED, ids=path_id)
def test_should_show_generated_output_diffs(path: Path) -> None:
    assert git_attribute(path, "diff") == "unspecified"


@pytest.mark.parametrize("path", HAND_WRITTEN, ids=path_id)
def test_should_not_mark_hand_written_code_as_generated(path: Path) -> None:
    assert git_attribute(path, "linguist-generated") == "unspecified"


@pytest.mark.parametrize(
    ("workflow", "path"),
    [
        ("cli.yml", "contracts/**"),
        ("cli.yml", "web/src/features/relay/contract/**"),
        ("web.yml", "contracts/**"),
        ("contracts.yml", "web/scripts/**"),
        ("contracts.yml", "relay/src/Jobranger.Relay.Core/ContractLimits.cs"),
        ("cli.yml", "relay/src/Jobranger.Relay.Core/ContractLimits.cs"),
        ("relay.yml", "contracts/**"),
    ],
)
def test_should_run_workflow_on_contract_change(workflow: str, path: str) -> None:
    triggers = YAML(typ="safe").load((WORKFLOWS / workflow).read_text("utf-8"))["on"]

    paths = [triggers[event]["paths"] for event in ("push", "pull_request")]

    assert all(path in event_paths for event_paths in paths)
