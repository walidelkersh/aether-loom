#!/usr/bin/env python3
"""Check exported strips and deterministic stop outcomes against C# fixtures."""
from __future__ import annotations

import csv
import json
from pathlib import Path

from adapter import ROOT, OUTPUT, build_model, export_reels, load_source

ENUM_TO_CONFIG = {
    "Crown": "CROWN", "Moth": "MOTH", "Shuttle": "SHUTTLE", "Dye": "DYE",
    "Linen": "LINEN", "Knot": "KNOT", "Wild": "WILD", "Spark": "SPARK",
}


def evaluate_ways(grid: list[list[str]], paytable: dict[tuple[int, str], float]) -> float:
    total = 0.0
    for symbol in {symbol for _, symbol in paytable}:
        ways = 1
        matched = 0
        for reel in range(5):
            count = sum(grid[row][reel] in (symbol, "WILD") for row in range(3))
            if count == 0:
                break
            matched += 1
            ways *= count
        total += ways * paytable.get((matched, symbol), 0.0)
    return total


def next_state(state: str, scatters: int) -> str:
    if scatters >= 3 or state == "Overdrive": return "Rest"
    if scatters == 0: return "Rest" if state == "Taut" else state
    if scatters == 1: return state
    return "Taut" if state == "Rest" else "Overdrive"


def main() -> None:
    export_reels()
    source = load_source()
    model = build_model()

    for set_name, strips in source["reelSets"].items():
        with (OUTPUT / f"{set_name}.csv").open(newline="") as handle:
            rows = list(csv.reader(handle))
        assert len(rows) == 40 and all(len(row) == 5 for row in rows)
        for reel in range(5):
            expected = [source["symbolCodes"][code] for code in strips[reel]]
            assert [row[reel] for row in rows] == expected

    fixtures = json.loads((ROOT / "reports/deterministic-fixtures.json").read_text())
    for fixture in fixtures:
        grid = [[ENUM_TO_CONFIG[symbol] for symbol in row] for row in fixture["grid"]]
        assert abs(evaluate_ways(grid, model.paytable) - fixture["award"]) < 1e-12
        scatters = sum(symbol == "SPARK" for row in grid for symbol in row)
        assert scatters == fixture["ScatterCount"]
        assert next_state(fixture["state"], scatters) == fixture["stateAfter"]

    print(f"PASS: {len(model.reel_files)} reel CSVs and {len(fixtures)} C# stop fixtures match")


if __name__ == "__main__":
    main()
