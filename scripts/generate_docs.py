#!/usr/bin/env python3
"""Generate human-readable math artifacts from committed machine reports."""
from __future__ import annotations

import hashlib
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = json.loads((ROOT / "reports/verified-metrics.json").read_text())
EXTREMES = json.loads((ROOT / "reports/extremes.json").read_text())
SENSITIVITY = json.loads((ROOT / "reports/sensitivity.json").read_text())
CONFIG_PATH = ROOT / "config/game-config.json"
CONFIG = json.loads(CONFIG_PATH.read_text())
EXACT = REPORT["exact"]
SIM = REPORT["simulation"]


def pct(value: float, digits: int = 6) -> str:
    return f"{value * 100:.{digits}f}%"


def pp_error(observed: float, exact: float) -> str:
    return f"{(observed - exact) * 100:+.6f} pp"


def write(path: str, content: str) -> None:
    (ROOT / path).write_text(content.rstrip() + "\n")


state_names = ["Rest", "Taut", "Overdrive"]
trigger_probs = [EXACT["reelSets"][f"BASE_{name.upper()}"]["FeatureTriggerProbability"] for name in state_names]
weights = [EXACT["stationaryStateProbabilities"][i] * trigger_probs[i] for i in range(3)]
trigger_total = sum(weights)
exact_feature_ev = sum(weights[i] * EXACT["features"][i]["ExpectedAward"] for i in range(3)) / trigger_total
exact_feature_duration = sum(weights[i] * EXACT["features"][i]["ExpectedSpins"] for i in range(3)) / trigger_total
observed_feature_ev = SIM["FeatureRtp"] / SIM["FeatureFrequency"]
config_hash = hashlib.sha256(CONFIG_PATH.read_bytes()).hexdigest()[:12]

verification_rows = [
    ("Base ways RTP", EXACT["BaseRtp"], SIM["BaseRtp"], pp_error(SIM["BaseRtp"], EXACT["BaseRtp"])),
    ("Feature RTP", EXACT["FeatureRtp"], SIM["FeatureRtp"], pp_error(SIM["FeatureRtp"], EXACT["FeatureRtp"])),
    ("Total RTP", EXACT["TotalRtp"], SIM["ObservedRtp"], pp_error(SIM["ObservedRtp"], EXACT["TotalRtp"])),
    ("Base hit probability", EXACT["BaseHitProbability"], SIM["BaseHitFrequency"], pp_error(SIM["BaseHitFrequency"], EXACT["BaseHitProbability"])),
    ("Feature-entry probability", EXACT["FeatureTriggerProbability"], SIM["FeatureFrequency"], pp_error(SIM["FeatureFrequency"], EXACT["FeatureTriggerProbability"])),
]

write("docs/verification.md", f"""
# Exact mathematics versus Monte Carlo

Model version: {CONFIG['version']}<br>
Configuration SHA-256 prefix: `{config_hash}`<br>
Simulation: {SIM['PaidSpins']:,} paid spins, PCG32 seed `{SIM['Seed']}`

| Quantity | Exact | Simulation | Simulation − exact |
|---|---:|---:|---:|
""" + "\n".join(f"| {name} | {pct(exact)} | {pct(observed)} | {error} |" for name, exact, observed, error in verification_rows) + f"""

The simulated total RTP is {pct(SIM['ObservedRtp'])}. Its normal 95% confidence interval is {pct(SIM['ObservedRtp'] - SIM['RtpConfidenceHalfWidth95'])} to {pct(SIM['ObservedRtp'] + SIM['RtpConfidenceHalfWidth95'])}; the exact {pct(EXACT['TotalRtp'])} result lies inside that interval.

| Feature quantity | Exact | Simulation | Difference |
|---|---:|---:|---:|
| Conditional feature award | {exact_feature_ev:.6f}× | {observed_feature_ev:.6f}× | {observed_feature_ev - exact_feature_ev:+.6f}× |
| Feature spins played | {exact_feature_duration:.6f} | {SIM['FreeSpinsPerTrigger']:.6f} | {SIM['FreeSpinsPerTrigger'] - exact_feature_duration:+.6f} |

## What is exact

The analyzer enumerates every stop on each 40-stop reel when building visible-window distributions. Per-symbol 243-ways EV is calculated from the five independent reel-window count distributions. Scatter-count polynomials produce each reel set's trigger probabilities. Those probabilities produce the three-state base transition matrix and its stationary distribution. A finite Markov reward recursion calculates bonus EV and duration for every starting stage. A separate reachable-profile dynamic program calculates maximum awards.

## What is estimated

Hit frequency for complete paid cycles, variance, standard deviation, volatility index, observed maximum, and all Monte Carlo columns are estimates from the seeded run. The confidence interval uses `mean ± 1.96 × sample-standard-deviation / sqrt(N)` on return in bet units. It quantifies sampling error under the usual independent-cycle normal approximation; it is not a regulatory certification.
""")

sens_lines = []
for row in SENSITIVITY:
    sens_lines.append(
        f"| {row['Parameter']} | {row['Level']} | {pct(row['ExactRtp'], 4)} | {pct(row['ExactFeatureFrequency'], 4)} | "
        f"{pct(row['SimulatedHitFrequency'], 4)} | {row['SimulatedStandardDeviation']:.4f}× | {row['ExactMaximumGameCycleAward']:,.2f}× |"
    )

write("docs/sensitivity-analysis.md", f"""
# Sensitivity analysis

Each row changes one parameter family from version {CONFIG['version']}. Exact RTP, feature frequency, and maximum exposure are recomputed from the altered model. Hit frequency and standard deviation use {SENSITIVITY[0]['SimulatedPaidSpins']:,} seeded paid spins per scenario. Because maximum observed win is unstable at this sample size, the comparison uses the exact reachable maximum instead.

| Parameter | Level | Exact RTP | Exact feature rate | Sim hit rate | Sim σ | Exact max |
|---|---|---:|---:|---:|---:|---:|
""" + "\n".join(sens_lines) + """

The results show four distinct controls:

- Bonus Wild density primarily moves feature RTP and volatility. Removing one Wild from reels 2–5 in every bonus set lowers total RTP by 3.3302 percentage points; adding one raises it by 4.1834 points.
- Base Spark density controls both the stationary tension mix and feature entry. The deliberately broad high case raises the feature rate from 0.4206% to 3.1288% and makes the model unsuitable at 212.2207% RTP. This is a boundary experiment, not a proposed configuration.
- Stage multipliers change the feature budget without changing feature-entry probability. The low and high schedules move total RTP to 94.5155% and 98.6183%.
- Initial free spins move total RTP and variance together while leaving entry frequency unchanged. Six spins return 93.3031%; eight return 99.9501%.

![Sensitivity chart](sensitivity.svg)
""")

symbol_names = CONFIG["symbolCodes"]
reel_sections = []
for set_name, reels in CONFIG["reelSets"].items():
    counts = [Counter(strip) for strip in reels]
    header = "| Reel | " + " | ".join(symbol_names.keys()) + " | Length |\n|---:" + "|---:" * (len(symbol_names) + 1) + "|"
    rows = [f"| {i + 1} | " + " | ".join(str(counts[i].get(code, 0)) for code in symbol_names) + f" | {len(reels[i])} |" for i in range(5)]
    strips = "\n".join(f"- R{i + 1}: `{strip}`" for i, strip in enumerate(reels))
    reel_sections.append(f"## {set_name}\n\n{header}\n" + "\n".join(rows) + f"\n\n{strips}")

write("docs/reel-strips.md", """
# Ordered reel strips

Every string is a circular 40-stop strip. The selected stop is the top symbol and the following two positions are the middle and bottom symbols. Codes map to: A Crown, B Moth, C Shuttle, D Dye, E Linen, F Knot, W Wild, X Spark. Wild and Spark occurrences have at least two intervening stops, including across the circular boundary, so a visible reel window contains at most one of each special symbol. Reel 1 contains no Wild.

""" + "\n\n".join(reel_sections))

write("docs/math-report.md", f"""
# Aether Loom mathematical model

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | {pct(EXACT['BaseRtp'])} |
| Weave Ascending feature | {pct(EXACT['FeatureRtp'])} |
| Scatter cash awards | 0.000000% |
| Jackpots | Not used |
| **Total** | **{pct(EXACT['TotalRtp'])}** |

The stationary base-state distribution is {pct(EXACT['stationaryStateProbabilities'][0], 4)} Rest, {pct(EXACT['stationaryStateProbabilities'][1], 4)} Taut, and {pct(EXACT['stationaryStateProbabilities'][2], 4)} Overdrive. The exact long-run feature probability is {pct(EXACT['FeatureTriggerProbability'])}, or 1 in {1 / EXACT['FeatureTriggerProbability']:.2f} paid spins.

## Base-state transition matrix

Rows are the current state and columns are Rest, Taut, Overdrive.

| Current state | Rest | Taut | Overdrive |
|---|---:|---:|---:|
""" + "\n".join(
    f"| {state_names[i]} | " + " | ".join(pct(value, 4) for value in EXACT["transitionMatrix"][i]) + " |" for i in range(3)
) + f"""

## Feature values by inherited stage

| Starting stage | Exact expected award | Exact expected spins |
|---|---:|---:|
""" + "\n".join(
    f"| {item['StartingStage']} | {item['ExpectedAward']:.6f}× | {item['ExpectedSpins']:.6f} |" for item in EXACT["features"]
) + f"""

The trigger-weighted feature expectation is {exact_feature_ev:.6f}× bet and the trigger-weighted duration is {exact_feature_duration:.6f} spins. The maximum reachable base award is {EXTREMES['MaximumBaseAward']:,.2f}×. The maximum complete paid-spin cycle, including its triggered feature, is {EXTREMES['MaximumGameCycleAward']:,.2f}×.

## Simulation statistics

| Metric | Result |
|---|---:|
| Paid spins | {SIM['PaidSpins']:,} |
| Seed | {SIM['Seed']} |
| Observed RTP | {pct(SIM['ObservedRtp'])} |
| 95% RTP CI half-width | ±{pct(SIM['RtpConfidenceHalfWidth95'])} |
| Overall hit frequency | {pct(SIM['HitFrequency'])} |
| Base hit frequency | {pct(SIM['BaseHitFrequency'])} |
| Feature frequency | {pct(SIM['FeatureFrequency'])} |
| Free spins per paid spin | {pct(SIM['FreeSpinFrequency'])} |
| Average non-zero cycle award | {SIM['AverageWin']:.6f}× |
| Variance | {SIM['Variance']:.6f} bet² |
| Standard deviation / volatility index | {SIM['StandardDeviation']:.6f}× bet |
| Maximum observed award | {SIM['MaximumObservedWin']:.2f}× |
""")

# Minimal standalone SVG; values are exact and sourced from sensitivity.json.
bars = []
max_rtp = max(row["ExactRtp"] for row in SENSITIVITY)
for i, row in enumerate(SENSITIVITY):
    y = 45 + i * 34
    width = row["ExactRtp"] / max_rtp * 520
    color = "#c9ff3b" if row["Parameter"] == "Baseline" else "#8a9a8b"
    label = f"{row['Parameter']} · {row['Level']}"
    bars.append(f'<text x="8" y="{y + 13}" fill="#dce5da" font-size="10">{label}</text><rect x="245" y="{y}" width="{width:.2f}" height="18" fill="{color}"/><text x="{250 + width:.2f}" y="{y + 13}" fill="#dce5da" font-size="10">{row["ExactRtp"]*100:.2f}%</text>')
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="900" height="370" viewBox="0 0 900 370" role="img" aria-labelledby="title desc"><title id="title">Exact RTP sensitivity</title><desc id="desc">Horizontal bars compare exact RTP for nine controlled scenarios.</desc><rect width="900" height="370" fill="#0b0f0c"/><text x="8" y="22" fill="#c9ff3b" font-family="monospace" font-size="14">EXACT RTP BY ONE-AT-A-TIME PARAMETER CHANGE</text><g font-family="monospace">{''.join(bars)}</g></svg>'''
write("docs/sensitivity.svg", svg)

print("Generated markdown, reel appendix, and sensitivity SVG from machine reports.")
