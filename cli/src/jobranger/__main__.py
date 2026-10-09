from __future__ import annotations

import logging
import sys

from jobranger.config import Settings
from jobranger.main import run
from jobranger.run_logs import LOG_FORMAT

HTTP_CLIENT_LOGGERS = ("httpx", "httpcore")

logging.basicConfig(level=logging.INFO, format=LOG_FORMAT)
for logger_name in HTTP_CLIENT_LOGGERS:
    logging.getLogger(logger_name).setLevel(logging.WARNING)
sys.exit(run(Settings.from_env()))
