from __future__ import annotations

import argparse
import hashlib
import json
import logging
import urllib.request
from pathlib import Path

import numpy as np
import onnxruntime as ort
from sentence_transformers import SentenceTransformer
from tokenizers import Tokenizer

REPOSITORY = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
REVISION = "e8f8c211226b894fcb81acc59f3b34ba3efd5f42"
BASE_URL = f"https://huggingface.co/{REPOSITORY}/resolve/{REVISION}"
MODEL_NAME = "model.onnx"
TOKENIZER_NAME = "tokenizer.json"
ONNX_TOLERANCE = 1e-5
MODEL_FILES = (
    (
        MODEL_NAME,
        "onnx/model.onnx",
        "10f7a088420252b26caf819236ca2c9d2987afd0fc06fec7553b542a5655a05a",
        470301610,
    ),
    (
        TOKENIZER_NAME,
        TOKENIZER_NAME,
        "2c3387be76557bd40970cec13153b3bbf80407865484b209e655e5e4729076b8",
        9081518,
    ),
)
MAX_TOKENS = 128
DIMENSION = 384
BATCH_SIZE = 16
CHUNK_BYTES = 1 << 20
PAD_ID = 1
MASK_FLOOR = 1e-9
NORM_FLOOR = 1e-12
DESCRIPTION_LIMIT = 600
REPO_ROOT = Path(__file__).resolve().parents[3]
GOLDEN_PATH = (
    REPO_ROOT / "cli/tests/Jobranger.Cli.Tests/Golden/Embeddings/embeddings.json"
)
LONG_DESCRIPTION = (
    "Vi ser etter en erfaren utvikler som trives med å bygge robuste tjenester i "
    "skyen. Du vil jobbe tett med produkteiere, designere og andre utviklere i et "
    "tverrfaglig team. Hos oss får du ansvar for hele livsløpet til tjenestene, "
    "fra idé og arkitektur til drift og videreutvikling. Vi bruker C#, .NET, "
    "TypeScript, React, PostgreSQL og Kubernetes, og vi deployer flere ganger "
    "om dagen. Søkere må ha relevant utdanning fra universitet eller høyskole, "
    "minst tre års erfaring og gode kommunikasjonsevner på norsk og engelsk. "
    "Vi tilbyr fleksibel arbeidstid, gode pensjonsordninger og et sosialt miljø."
)
TEXTS = (
    "Utvikler. Vi søker en backend-utvikler med erfaring i C# og .NET til vårt "
    "team i Bergen.",
    "Senior Software Engineer. Join our platform team building distributed "
    "systems in Go and Kotlin.",
    "Systemutvikler – Java/Spring. Fast stilling, hjemmekontor to dager i uka.",
    "Frontend developer (React, TypeScript)",
    "DATAINGENIØR I OSLO",
    "Sykepleier, natt. 100 % stilling ved medisinsk avdeling.",
    "  Leading and trailing   spaces\tand\ttabs\nand newlines  ",
    "",
    "Ærlig, åpen og ødeleggende? ÆØÅ æøå",
    "Café naïve résumé façade",
    "ﬁnance ﬂow ① ＡＢＣ",
    "Emoji 🚀 og 👩\u200d💻 i annonsen",
    "日本語のソフトウェアエンジニア",
    "Разработчик программного обеспечения",
    "C++/C# .NET 8, ASP.NET Core, SQL Server & Azure DevOps \u2014 5+ års erfaring",
    "https://example.test/jobs/123?ref=abc&utm=x",
    " ".join([LONG_DESCRIPTION] * 3),
    "Utvikler. " + LONG_DESCRIPTION[:DESCRIPTION_LIMIT],
    "zero\u200bwidth\u00a0nbsp \u0378 unassigned",
    "Lærling innen IKT-servicefag",
)

log = logging.getLogger("export_golden")


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(CHUNK_BYTES):
            digest.update(chunk)
    return digest.hexdigest()


def fetch(cache: Path) -> dict[str, Path]:
    cache.mkdir(parents=True, exist_ok=True)
    paths: dict[str, Path] = {}
    for name, remote, sha256, _size in MODEL_FILES:
        path = cache / name
        if not path.exists():
            log.info("downloading %s", remote)
            urllib.request.urlretrieve(f"{BASE_URL}/{remote}", path)
        actual = sha256_of(path)
        if actual != sha256:
            path.unlink()
            message = f"{name} sha256 {actual} differs from pinned {sha256}, deleted"
            raise ValueError(message)
        paths[name] = path
    return paths


def token_ids(tokenizer_path: Path) -> list[list[int]]:
    tokenizer = Tokenizer.from_file(str(tokenizer_path))
    tokenizer.enable_truncation(MAX_TOKENS)
    return [tokenizer.encode(text).ids for text in TEXTS]


def reference_embeddings(model: SentenceTransformer) -> np.ndarray:
    return model.encode(
        list(TEXTS),
        batch_size=BATCH_SIZE,
        normalize_embeddings=True,
        convert_to_numpy=True,
    )


def reference_token_ids(model: SentenceTransformer) -> list[list[int]]:
    return [
        model.tokenizer(text, truncation=True, max_length=model.max_seq_length)[
            "input_ids"
        ]
        for text in TEXTS
    ]


def onnx_embeddings(model_path: Path, ids: list[list[int]]) -> np.ndarray:
    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    input_names = {node.name for node in session.get_inputs()}
    rows: list[np.ndarray] = []
    for start in range(0, len(ids), BATCH_SIZE):
        batch = ids[start : start + BATCH_SIZE]
        width = max(len(row) for row in batch)
        input_ids = np.full((len(batch), width), PAD_ID, dtype=np.int64)
        mask = np.zeros((len(batch), width), dtype=np.int64)
        for index, row in enumerate(batch):
            input_ids[index, : len(row)] = row
            mask[index, : len(row)] = 1
        feeds = {"input_ids": input_ids, "attention_mask": mask}
        if "token_type_ids" in input_names:
            feeds["token_type_ids"] = np.zeros_like(input_ids)
        hidden = session.run(None, feeds)[0]
        weights = mask[:, :, None].astype(np.float32)
        counts = np.clip(weights.sum(axis=1), MASK_FLOOR, None)
        pooled = (hidden * weights).sum(axis=1) / counts
        norms = np.linalg.norm(pooled, axis=1, keepdims=True)
        rows.append(pooled / np.clip(norms, NORM_FLOOR, None))
    return np.concatenate(rows).astype(np.float32)


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    logging.getLogger("httpx").setLevel(logging.WARNING)
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--out", type=Path, default=GOLDEN_PATH)
    arguments = parser.parse_args()

    paths = fetch(arguments.cache)
    ids = token_ids(paths[TOKENIZER_NAME])
    model = SentenceTransformer(REPOSITORY, revision=REVISION, device="cpu")
    if ids != reference_token_ids(model):
        message = "tokenizers ids differ from sentence-transformers ids"
        raise ValueError(message)
    reference = reference_embeddings(model)
    onnx = onnx_embeddings(paths[MODEL_NAME], ids)
    difference = float(np.abs(reference - onnx).max())
    log.info("max difference between onnx and sentence-transformers: %g", difference)
    if difference > ONNX_TOLERANCE:
        message = (
            f"onnx differs from sentence-transformers by {difference}, "
            f"above ONNX_TOLERANCE {ONNX_TOLERANCE}"
        )
        raise ValueError(message)

    golden = {
        "repository": REPOSITORY,
        "revision": REVISION,
        "files": [
            {
                "name": name,
                "url": f"{BASE_URL}/{remote}",
                "sha256": sha256,
                "size": size,
            }
            for name, remote, sha256, size in MODEL_FILES
        ],
        "maxTokens": MAX_TOKENS,
        "dimension": DIMENSION,
        "onnxMaxDifference": difference,
        "cases": [
            {
                "text": text,
                "ids": row_ids,
                "embedding": [float(value) for value in row],
            }
            for text, row_ids, row in zip(TEXTS, ids, reference, strict=True)
        ],
    }
    arguments.out.parent.mkdir(parents=True, exist_ok=True)
    arguments.out.write_text(
        json.dumps(golden, ensure_ascii=False, indent=1) + "\n", encoding="utf-8"
    )
    log.info("wrote %s", arguments.out)


if __name__ == "__main__":
    main()
