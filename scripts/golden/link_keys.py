from __future__ import annotations

import argparse
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


def main() -> None:
    parser = argparse.ArgumentParser(description="Write Python-made link keys")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    keyring.set_keyring(fail.Keyring())
    with tempfile.TemporaryDirectory() as config_dir:
        os.environ[CONFIG_DIR_VARIABLE] = config_dir
        keys.load_or_create_keys()
        args.out.mkdir(parents=True, exist_ok=True)
        for name in EXPORTED:
            shutil.copyfile(Path(config_dir) / name, args.out / name)


if __name__ == "__main__":
    main()
