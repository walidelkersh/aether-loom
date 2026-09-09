#!/usr/bin/env python3
"""Export the canonical Aether Loom model to Stake Engine-style inputs.

This is an experimental representation adapter. SlotGame.Core remains canonical.
"""
from __future__ import annotations

import csv
import json
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = ROOT / "config/game-config.json"
OUTPUT = Path(__file__).resolve().parent / "reels"


@dataclass(frozen=True)
class StakeModel:
    game_id: str
    win_type: str
    num_reels: int
    num_rows: list[int]
    paytable: dict[tuple[int, str], float]
    special_symbols: dict[str, list[str]]
    reel_files: dict[str, str]
    stage_multipliers: list[float]


def load_source() -> dict:
    return json.loads(CONFIG_PATH.read_text())


def build_model() -> StakeModel:
    source = load_source()
    pays = {
        (count, symbol): value
        for symbol, counts in source["paytable"].items()
        for count_text, value in counts.items()
        for count in [int(count_text)]
    }
    reel_files = {name: f"{name}.csv" for name in source["reelSets"]}
    return StakeModel(
        game_id="aether_loom_experimental",
        win_type="ways",
        num_reels=source["reels"],
        num_rows=[source["rows"]] * source["reels"],
        paytable=pays,
        special_symbols={"wild": ["WILD"], "scatter": ["SPARK"], "multiplier": []},
        reel_files=reel_files,
        stage_multipliers=source["stageMultipliers"],
    )


def export_reels() -> None:
    source = load_source()
    code_names = source["symbolCodes"]
    OUTPUT.mkdir(exist_ok=True)
    for set_name, strips in source["reelSets"].items():
        if len({len(strip) for strip in strips}) != 1:
            raise ValueError(f"Stake CSV export requires equal strip lengths in {set_name}")
        with (OUTPUT / f"{set_name}.csv").open("w", newline="") as handle:
            writer = csv.writer(handle, lineterminator="\n")
            for stop in range(len(strips[0])):
                writer.writerow([code_names[strip[stop]] for strip in strips])


if __name__ == "__main__":
    export_reels()
    model = build_model()
    print(f"Exported {len(model.reel_files)} reel sets for {model.game_id}")
