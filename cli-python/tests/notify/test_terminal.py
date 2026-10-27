from __future__ import annotations

from jobranger.notify.terminal import hidden_line


def test_should_order_hidden_line_by_count() -> None:
    hidden = {"role": 2, "senior": 10, "masters": 0, "place": 5}

    line = hidden_line(hidden, 3.0)

    assert line == "17 hidden: 10 senior · 5 other places · 2 other areas"


def test_should_leave_out_other_from_hidden_line() -> None:
    line = hidden_line({"senior": 1, "other": 30}, 3.0)

    assert line == "1 hidden: 1 senior"


def test_should_label_years_with_rounded_year_cap() -> None:
    line = hidden_line({"years": 14, "masters": 6}, 2.6)

    assert line == "20 hidden: 14 need 3+ yrs · 6 master's"


def test_should_ask_for_more_years_when_year_cap_is_unset() -> None:
    line = hidden_line({"years": 2}, None)

    assert line == "2 hidden: 2 need more yrs"


def test_should_return_empty_line_when_nothing_is_hidden() -> None:
    line = hidden_line({"other": 4}, 3.0)

    assert line == ""
