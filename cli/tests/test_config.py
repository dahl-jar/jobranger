from __future__ import annotations

import pytest

from jobranger.config import Settings


@pytest.mark.parametrize("value", ["1", "true", "TRUE", "yes"])
def test_should_accept_true_values_for_report_all(
    monkeypatch: pytest.MonkeyPatch, value: str
) -> None:
    monkeypatch.setenv("REPORT_ALL", value)

    settings = Settings.from_env()

    assert settings.report_all is True


@pytest.mark.parametrize(
    ("variable", "field"),
    [("REPORT_ALL", "report_all"), ("RECLASSIFY_ALL", "reclassify_all")],
)
def test_should_default_flag_to_false_when_unset(
    monkeypatch: pytest.MonkeyPatch, variable: str, field: str
) -> None:
    monkeypatch.delenv(variable, raising=False)

    settings = Settings.from_env()

    assert getattr(settings, field) is False


def test_should_accept_true_for_reclassify_all(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("RECLASSIFY_ALL", "true")

    settings = Settings.from_env()

    assert settings.reclassify_all is True
