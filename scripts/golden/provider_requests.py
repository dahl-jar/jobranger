from __future__ import annotations

import argparse
import json
from pathlib import Path

import anthropic
import httpx2
import openai
from jobranger.matching.assessment import AdAssessment
from jobranger.providers.anthropic import AnthropicProvider, _output_config
from jobranger.providers.base import ClassifyPrompt
from jobranger.providers.openai_compat import OpenAICompatProvider, base_url_for
from jobranger.providers.prices import spec_for

from golden_io import write_json

FAKE_KEY = "sk-fake-golden-key-0001"
OLLAMA_URL = "http://box:11434"
ANTHROPIC_MODEL = "claude-sonnet-5-5"
COMPAT_MODELS = {
    "openai": "gpt-5-mini",
    "openrouter": "vendor/model-one",
    "ollama": "llama3.2",
}
ANTHROPIC_REPLY = {
    "content": [{"type": "text", "text": "{}"}],
    "usage": {"input_tokens": 0, "output_tokens": 0},
    "stop_reason": "end_turn",
}
COMPAT_REPLY = {"choices": [{"message": {"content": "{}"}, "finish_reason": "stop"}]}


class Recorder:
    def __init__(self, reply: object) -> None:
        self.reply = reply
        self.bodies: list[bytes] = []

    def __call__(self, request: httpx2.Request) -> httpx2.Response:
        self.bodies.append(request.content)
        return httpx2.Response(200, json=self.reply)


def http_client(recorder: Recorder) -> httpx2.Client:
    return httpx2.Client(transport=httpx2.MockTransport(recorder))


def anthropic_body(prompt: ClassifyPrompt) -> bytes:
    recorder = Recorder(ANTHROPIC_REPLY)
    client = anthropic.Anthropic(
        api_key=FAKE_KEY, max_retries=0, http_client=http_client(recorder)
    )
    provider = AnthropicProvider(
        api_key=FAKE_KEY,
        model=ANTHROPIC_MODEL,
        spec=spec_for("anthropic", ANTHROPIC_MODEL, None),
        client=client,
    )
    provider.classify(prompt, AdAssessment)
    return recorder.bodies[-1]


def compat_body(kind: str, prompt: ClassifyPrompt) -> bytes:
    recorder = Recorder(COMPAT_REPLY)
    base_url = base_url_for(kind, OLLAMA_URL)
    client = openai.OpenAI(
        api_key=FAKE_KEY,
        base_url=base_url,
        max_retries=0,
        http_client=http_client(recorder),
    )
    model = COMPAT_MODELS[kind]
    provider = OpenAICompatProvider(
        kind=kind,
        api_key=FAKE_KEY,
        model=model,
        base_url=base_url,
        spec=spec_for(kind, model, None),
        client=client,
    )
    provider.classify(prompt, AdAssessment)
    return recorder.bodies[-1]


def compat_schema(body: bytes) -> object:
    return json.loads(body)["response_format"]["json_schema"]["schema"]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--prompt", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    arguments = parser.parse_args()
    fields = json.loads(arguments.prompt.read_text(encoding="utf-8"))
    prompt = ClassifyPrompt(system=fields["system"], user=fields["user"])
    out: Path = arguments.out
    out.mkdir(parents=True, exist_ok=True)
    (out / "anthropic.json").write_bytes(anthropic_body(prompt))
    anthropic_schema = _output_config(AdAssessment, None)["format"]["schema"]
    write_json(out / "anthropic-schema.json", anthropic_schema)
    for kind in COMPAT_MODELS:
        body = compat_body(kind, prompt)
        (out / f"{kind}.json").write_bytes(body)
    write_json(out / "openai-schema.json", compat_schema(body))


if __name__ == "__main__":
    main()
