from __future__ import annotations

from jobranger.ads.titles import title_level


def test_should_label_lead_title_as_lead() -> None:
    assert title_level("Lead Backend Engineer") == "lead"


def test_should_label_senior_title_as_senior() -> None:
    assert title_level("Seniorutvikler") == "senior"


def test_should_not_label_trainee_title_senior() -> None:
    assert title_level("Experienced trainee") is None
