from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Mapping

    from jobranger.matching.rules import HiddenKind

HIDDEN_LABELS: dict[HiddenKind, str] = {
    "senior": "senior",
    "masters": "master's",
    "role": "other areas",
    "place": "other places",
}
UNKNOWN_YEARS_LABEL = "need more yrs"
MIDDLE_DOT = " · "


def hidden_line(hidden: Mapping[HiddenKind, int], max_years: float | None) -> str:
    counts = sorted(
        (
            (count, _label(kind, max_years))
            for kind, count in hidden.items()
            if count > 0 and kind != "other"
        ),
        key=lambda part: -part[0],
    )
    if not counts:
        return ""
    total = sum(count for count, _ in counts)
    parts = MIDDLE_DOT.join(f"{count} {label}" for count, label in counts)
    return f"{total} hidden: {parts}"


def _label(kind: HiddenKind, max_years: float | None) -> str:
    if kind == "years":
        if max_years is None:
            return UNKNOWN_YEARS_LABEL
        return f"need {round(max_years)}+ yrs"
    return HIDDEN_LABELS[kind]
