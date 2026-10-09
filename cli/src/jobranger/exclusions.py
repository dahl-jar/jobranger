from __future__ import annotations

import re
from typing import TYPE_CHECKING

from jobranger.ads.duplicates import companies_match

if TYPE_CHECKING:
    from jobranger.ads.models import Ad

EXCLUDED_COMPANIES: tuple[str, ...] = ()
INTERNSHIP_TITLE_PATTERN = re.compile(
    r"\b(?:internship\b|intern(?=\s*(?:$|[-–,:(|/0-9]))|sommerjobb|praktikant)",
    re.IGNORECASE,
)


def is_excluded(ad: Ad) -> bool:
    if any(companies_match(ad.company, company) for company in EXCLUDED_COMPANIES):
        return True
    titles = (ad.title, ad.display_title or "")
    return any(INTERNSHIP_TITLE_PATTERN.search(title) for title in titles)
