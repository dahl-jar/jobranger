from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

import keyring
from jobranger import credentials
from jobranger.link import keys
from keyring.backends import fail

CONFIG_DIR_VARIABLE = "JOBRANGER_CONFIG_DIR"
EXPORTED = (credentials.SECRETS_FILE_NAME, keys.METADATA_FILE_NAME)
SIGNING_SEED = b"golden-signing"
AGREEMENT_SEED = b"golden-agreement"


def seeded_key(seed: bytes) -> str:
    return keys._encode(hashlib.sha256(seed).digest())


def seeded_pair() -> str:
    return json.dumps(
        {
            keys.SIGNING_FIELD: seeded_key(SIGNING_SEED),
            keys.AGREEMENT_FIELD: seeded_key(AGREEMENT_SEED),
        }
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Write Python-made link keys")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    keyring.set_keyring(fail.Keyring())
    keys._new_pair = seeded_pair
    with tempfile.TemporaryDirectory() as config_dir:
        os.environ[CONFIG_DIR_VARIABLE] = config_dir
        keys.load_or_create_keys()
        args.out.mkdir(parents=True, exist_ok=True)
        for name in EXPORTED:
            shutil.copyfile(Path(config_dir) / name, args.out / name)


if __name__ == "__main__":
    main()
