#!/usr/bin/env python3
"""Independent reference implementation of the Aether Loom RTP, written from the rules, not from the C# code.

Why this exists: the engine, simulator and analyzers all share SlotGame.Core. If a rule were misread there,
all of them would agree with each other and still be wrong. This script re-derives the headline numbers from
config/game-config.json and the written rules (docs/game-design.md) with exact rational arithmetic
(fractions.Fraction) and compares them with the committed C# report.

Rules implemented here:
  * 5 reels x 3 rows, each reel a circular 40-stop strip; a uniform stop shows that stop and the next two.
  * 243 ways, left to right, 3+ consecutive reels; Wild substitutes for paying symbols; pays are per way
    in multiples of total bet; only the longest run of each symbol pays; different symbols add.
  * Base state (Rest=0, Taut=1, Overdrive=2) selects the base reel set. After a paid spin with k Sparks:
    k>=3 -> Rest and the feature triggers, starting at the pre-spin state; Overdrive -> Rest;
    k=0 -> down one; k=1 -> stay; k=2 -> up one.
  * Feature: 7 free spins at stage = trigger state, stage multipliers 1 / 1.75 / 3.5. A free spin with 2+
    Sparks adds one spin and raises the stage (capped at Overdrive), unless it is spin number 12, which ends
    the feature. At most 12 free spins.

Usage: python scripts/independent_check.py [--report reports/par-sheet.json]
Exit status 1 if any compared quantity differs from the C# report by more than 1e-12.
"""
from __future__ import annotations

import argparse
import json
import sys
from fractions import Fraction
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STATES = ["REST", "TAUT", "OVERDRIVE"]


def load_config() -> dict:
    return json.loads((ROOT / "config/game-config.json").read_text())


def windows(strip: str) -> list[str]:
    n = len(strip)
    return [strip[i] + strip[(i + 1) % n] + strip[(i + 2) % n] for i in range(n)]


def ways_ev(reels: list[str], paytable: dict, codes: dict) -> Fraction:
    """Expected ways award of one spin, by the closed form over independent reels.

    For symbol s with C_r = number of s-or-Wild cells on reel r:
      E[award_s] = p3 E[C1]E[C2]E[C3]P(C4=0) + p4 E[C1..C4]P(C5=0) + p5 E[C1..C5].
    The product form is valid because reels are independent and C_r=0 already zeroes the product.
    """
    total = Fraction(0)
    wild = next(k for k, v in codes.items() if v == "WILD")
    for name, pays in paytable.items():
        code = next(k for k, v in codes.items() if v == name)
        means, zeros = [], []
        for strip in reels:
            counts = [sum(ch in (code, wild) for ch in w) for w in windows(strip)]
            means.append(Fraction(sum(counts), len(strip)))
            zeros.append(Fraction(counts.count(0), len(strip)))
        product = means[0] * means[1]
        for length in (3, 4, 5):
            product *= means[length - 1]
            tail = zeros[length] if length < 5 else Fraction(1)
            total += Fraction(str(pays[str(length)])) * product * tail
    return total


def spark_distribution(reels: list[str], codes: dict) -> list[Fraction]:
    spark = next(k for k, v in codes.items() if v == "SPARK")
    dist = [Fraction(1)]
    for strip in reels:
        per = [Fraction(0)] * 4
        for w in windows(strip):
            per[w.count(spark)] += Fraction(1, len(strip))
        new = [Fraction(0)] * (len(dist) + 3)
        for i, p in enumerate(dist):
            for k, q in enumerate(per):
                new[i + k] += p * q
        dist = new
    return dist


def next_state(state: int, sparks: int) -> int:
    if sparks >= 3 or state == 2:
        return 0
    return {0: max(0, state - 1), 1: state, 2: min(2, state + 1)}[sparks]


def solve_stationary(p: list[list[Fraction]]) -> list[Fraction]:
    # Equations: pi_j = sum_i pi_i p_ij for j = 0, 1 and pi_0 + pi_1 + pi_2 = 1. Gauss-Jordan over Q.
    a = [[p[i][j] - (1 if i == j else 0) for i in range(3)] + [Fraction(0)] for j in range(2)]
    a.append([Fraction(1)] * 3 + [Fraction(1)])
    for col in range(3):
        pivot = next(r for r in range(col, 3) if a[r][col] != 0)
        a[col], a[pivot] = a[pivot], a[col]
        for r in range(3):
            if r != col and a[r][col] != 0:
                f = a[r][col] / a[col][col]
                a[r] = [x - f * y for x, y in zip(a[r], a[col])]
    return [a[i][3] / a[i][i] for i in range(3)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", default=str(ROOT / "reports/par-sheet.json"))
    args = parser.parse_args()

    cfg = load_config()
    codes, paytable, sets = cfg["symbolCodes"], cfg["paytable"], cfg["reelSets"]
    mult = [Fraction(str(m)) for m in cfg["stageMultipliers"]]
    initial, cap = cfg["initialFreeSpins"], cfg["maximumFeatureSpins"]

    ev = {name: ways_ev(reels, paytable, codes) for name, reels in sets.items()}
    sparks = {name: spark_distribution(reels, codes) for name, reels in sets.items()}

    p = [[Fraction(0)] * 3 for _ in range(3)]
    for s in range(3):
        for k, q in enumerate(sparks[f"BASE_{STATES[s]}"]):
            p[s][next_state(s, k)] += q
    pi = solve_stationary(p)

    def feature_value(start: int) -> Fraction:
        @lru_cache(maxsize=None)
        def value(stage: int, remaining: int, played: int) -> Fraction:
            if remaining == 0 or played >= cap:
                return Fraction(0)
            name = f"BONUS_{STATES[stage]}"
            award = ev[name] * mult[stage]
            esc = sum(sparks[name][2:], Fraction(0)) if played + 1 < cap else Fraction(0)
            return award + esc * value(min(2, stage + 1), remaining, played + 1) \
                + (1 - esc) * value(stage, remaining - 1, played + 1)
        return value(start, initial, 0)

    feature = [feature_value(s) for s in range(3)]
    trig = [sum(sparks[f"BASE_{STATES[s]}"][3:], Fraction(0)) for s in range(3)]
    base_rtp = sum(pi[s] * ev[f"BASE_{STATES[s]}"] for s in range(3))
    feature_rtp = sum(pi[s] * trig[s] * feature[s] for s in range(3))
    total = base_rtp + feature_rtp
    conditional = [ev[f"BASE_{STATES[s]}"] + trig[s] * feature[s] for s in range(3)]
    lo, hi = Fraction(str(min(cfg["bets"]))), Fraction(str(max(cfg["bets"])))
    bets = [lo, hi, hi]
    switch = sum(pi[s] * bets[s] * conditional[s] for s in range(3)) / sum(pi[s] * bets[s] for s in range(3))

    ours = {
        "baseRtp": base_rtp, "featureRtp": feature_rtp, "totalRtp": total,
        "featureTriggerProbability": sum(pi[s] * trig[s] for s in range(3)),
        "stationaryRest": pi[0], "stationaryTaut": pi[1], "stationaryOverdrive": pi[2],
        "conditionalRest": conditional[0], "conditionalTaut": conditional[1], "conditionalOverdrive": conditional[2],
        "betSwitchReturn": switch,
    }
    print("Independent Python reference (exact rationals)")
    for key, value in ours.items():
        print(f"  {key:28s} {float(value):.15f}")

    report_path = Path(args.report)
    if not report_path.exists():
        print(f"No C# report at {report_path}; nothing to compare.")
        return 0
    report = json.loads(report_path.read_text())
    exact = report["exact"]
    theirs = {
        "baseRtp": exact["BaseRtp"], "featureRtp": exact["FeatureRtp"], "totalRtp": exact["TotalRtp"],
        "featureTriggerProbability": exact["FeatureTriggerProbability"],
        "stationaryRest": exact["stationaryStateProbabilities"][0],
        "stationaryTaut": exact["stationaryStateProbabilities"][1],
        "stationaryOverdrive": exact["stationaryStateProbabilities"][2],
        "conditionalRest": exact["conditionalStateRtp"][0],
        "conditionalTaut": exact["conditionalStateRtp"][1],
        "conditionalOverdrive": exact["conditionalStateRtp"][2],
        "betSwitchReturn": report["advantagePlay"]["sharedStateBetSwitchReturn"],
    }
    failures = 0
    print(f"\nComparison with {report_path.relative_to(ROOT) if report_path.is_relative_to(ROOT) else report_path}")
    for key, value in theirs.items():
        diff = abs(float(ours[key]) - value)
        status = "ok" if diff <= 1e-12 else "MISMATCH"
        failures += status != "ok"
        print(f"  {key:28s} |diff| = {diff:.2e}  {status}")
    print("PASS" if failures == 0 else f"FAIL ({failures})")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
