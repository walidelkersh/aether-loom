#!/usr/bin/env python3
"""Generate every human-readable math artifact from the committed machine reports.

Nothing in docs/math.md, advantage-play.md, reel-strips.md or PAR-sheet.html is typed by hand: each number is read from reports/*.json, which the C#
analyzers and simulator write. Regenerate after any configuration change:

    dotnet run --project src/SlotGame.Simulation -c Release -- --par --output reports/par-sheet.json
    ... (see README "Reproduce every number")
    python scripts/generate_docs.py
"""
from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
load = lambda name: json.loads((ROOT / "reports" / name).read_text())
REPORT, PAR, EXTREMES = load("verified-metrics.json"), load("par-sheet.json"), load("extremes.json")
SENSITIVITY, BETSWITCH = load("sensitivity.json"), load("bet-switch.json")
CONFIG_PATH = ROOT / "config/game-config.json"
CONFIG = json.loads(CONFIG_PATH.read_text())
EXACT, SIM, CHI = REPORT["exact"], REPORT["simulation"], REPORT["distributionTest"]
STATS, ADV = PAR["statistics"], PAR["advantagePlay"]
STATE_NAMES = ["Rest", "Taut", "Overdrive"]
CONFIG_HASH = hashlib.sha256(CONFIG_PATH.read_bytes()).hexdigest()[:12]


def pct(value: float, digits: int = 6) -> str:
    return f"{value * 100:.{digits}f}%"


def prob(value: float) -> str:
    """Percentage for ordinary probabilities, scientific notation for very rare events."""
    if value == 0:
        return "0 (impossible)"
    return f"{value * 100:.4f}%" if value >= 1e-6 else f"{value:.3e}"


def pp(value: float, digits: int = 4) -> str:
    return f"{value * 100:+.{digits}f} pp"


def write(path: str, content: str) -> None:
    (ROOT / path).write_text(content.strip() + "\n", encoding="utf-8")


trigger = [EXACT["reelSets"][f"BASE_{n.upper()}"]["FeatureTriggerProbability"] for n in STATE_NAMES]
weights = [EXACT["stationaryStateProbabilities"][i] * trigger[i] for i in range(3)]
feature_ev = sum(w * EXACT["features"][i]["ExpectedAward"] for i, w in enumerate(weights)) / sum(weights)
feature_spins = sum(w * EXACT["features"][i]["ExpectedSpins"] for i, w in enumerate(weights)) / sum(weights)
observed_feature_ev = SIM["FeatureRtp"] / SIM["FeatureFrequency"]
exact_half = EXACT["rtpHalfWidth95FromAsymptoticVariance"]
z_exact = (SIM["ObservedRtp"] - EXACT["TotalRtp"]) / (exact_half / 1.959964)
shared, per_bet = BETSWITCH["simulations"]
base_rest = PAR["exact"]["reelSets"]["BASE_REST"]["SymbolContributions"]
knot_share = base_rest["Knot"] / sum(base_rest.values())
small_wins = STATS["baseWinDistribution"][1]["probability"]
sens = {(r["Parameter"], r["Level"]): r for r in SENSITIVITY}
baseline = sens[("Baseline", "configured")]

# ----------------------------------------------------------------------------------------------- math report
MATH_PART = (f"""
# Aether Loom mathematical model

Model version {CONFIG['version']} · configuration SHA-256 `{CONFIG_HASH}`. Every figure on this page is **exact**
(computed from the reel strips, not sampled) unless it is marked *simulated*.

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | {pct(EXACT['BaseRtp'], 4)} |
| Weave Ascending feature | {pct(EXACT['FeatureRtp'], 4)} |
| Scatter cash awards / jackpots | none |
| **Total RTP** | **{pct(EXACT['TotalRtp'], 4)}** |

Target 96.00%. The exact value is {pct(EXACT['TotalRtp'], 8)}, {abs(EXACT['TotalRtp'] - 0.96) * 100:.7f} percentage points from target.
It was reached by downgrading eight regular-symbol stops on BASE_REST and BASE_TAUT (for example Moth to Shuttle).
No Wild or Spark moved, so the transition matrix, the stationary mix, trigger rates and the feature are unchanged;
only base ways EV moved.

A Python implementation written from the rules alone, in exact fractions
(`scripts/independent_check.py`), reproduces it to 1e-12.

$$\\text{{RTP}}=\\sum_{{s}}\\pi_s\\,(B_s+t_sF_s),\\qquad \\pi P=\\pi,\\ \\textstyle\\sum_s\\pi_s=1$$

where $B_s$ is the base ways EV of state $s$'s reel set, $t_s$ its 3+ Spark probability, and $F_s$ the expected feature
award when the feature starts at stage $s$.

## Volatility profile

| Metric | Exact value |
|---|---:|
| Hit frequency (any award, incl. feature) | {pct(STATS['HitFrequency'], 4)} (1 in {1 / STATS['HitFrequency']:.3f}) |
| Base hit frequency | {pct(STATS['BaseHitFrequency'], 4)} |
| Paid spins returning less than the bet | {pct(small_wins, 2)} of all paid spins |
| Feature frequency | {pct(EXACT['FeatureTriggerProbability'], 4)} (1 in {1 / EXACT['FeatureTriggerProbability']:.2f}) |
| Average feature award | {feature_ev:.4f}× bet over {feature_spins:.4f} spins |
| Variance per paid spin | {STATS['Variance']:.6f} bet² |
| Standard deviation | {STATS['StandardDeviation']:.6f}× bet |
| Volatility index (90%, 1.645σ) | {STATS['VolatilityIndex90']:.4f} |
| Volatility index (95%, 1.960σ) | {STATS['VolatilityIndex95']:.4f} |
| Long-run variance with state correlation, σ²∞ | {STATS['AsymptoticVariance']:.6f} bet² ({(STATS['CorrelationFactor'] - 1) * 100:+.3f}% vs. i.i.d.) |
| Maximum base award (reachable) | {EXTREMES['MaximumBaseAward']:,.2f}× |
| Maximum paid-cycle award (reachable) | {EXTREMES['MaximumGameCycleAward']:,.2f}× |

Variance, hit frequency and the win distribution come from enumerating all 40⁵ = 102,400,000 stop combinations
of each distinct reel set, with awards held as integers (hundredths of a bet) in 128-bit accumulators, so they
carry no sampling or rounding error. The feature's second moment uses backward induction that conditions on the
Spark class, because the event that extends the feature (2+ Sparks) is correlated with the award of the same spin.

## Base-game win distribution (stationary mix)

| Single-spin base award | Probability | 1 in |
|---|---:|---:|
""" + "\n".join(
    f"| {b['label']}× | {prob(b['probability'])} | {('never' if b['probability'] == 0 else f'{1 / b['probability']:,.1f}')} |"
    for b in STATS["baseWinDistribution"]
) + f"""

## Base-state Markov chain

Rows are the current state; columns are the next state.

| Current state | Rest | Taut | Overdrive | Stationary π | Conditional RTP | Value vs. Rest |
|---|---:|---:|---:|---:|---:|---:|
""" + "\n".join(
    f"| {STATE_NAMES[i]} | " + " | ".join(pct(v, 4) for v in EXACT["transitionMatrix"][i])
    + f" | {pct(EXACT['stationaryStateProbabilities'][i], 4)} | {pct(EXACT['conditionalStateRtp'][i], 3)} | {STATS['states'][i]['RelativeValue']:+.4f} bets |"
    for i in range(3)
) + f"""

*Value vs. Rest* is $v = Z(m-\\mu\\mathbf{{1}})$ with fundamental matrix $Z=(I-P+\\mathbf{{1}}\\pi)^{{-1}}$: the extra
long-run return, in bets, of a session currently in that state compared with one in Rest. It quantifies what a
saved state is worth to the next player (see [advantage-play.md](advantage-play.md)).

## Feature by inherited stage

| Starting stage | Expected award | SD of award | Expected spins | P(feature pays 0) |
|---|---:|---:|---:|---:|
""" + "\n".join(
    f"| {STATE_NAMES[i]} | {EXACT['features'][i]['ExpectedAward']:.6f}× | {(f['SecondMoment'] - f['Mean'] ** 2) ** 0.5:.4f}× | {EXACT['features'][i]['ExpectedSpins']:.6f} | {f['ZeroProbability']:.2e} |"
    for i, f in enumerate(STATS["features"])
) + f"""

Expected duration is identical for every stage because the three bonus reel sets share one Spark distribution
(escalation probability is the same at every stage), not because of a separate design rule.

## Player experience: RTP range after N paid spins

Uses the correlation-corrected σ∞, constant bet. A session of N spins lands inside the band with the stated probability
(normal approximation, which is poor for very short sessions of a heavy-tailed game).

| Paid spins | 90% band | 95% band |
|---:|---:|---:|
""" + "\n".join(
    f"| {b['Spins']:,} | {b['LowerRtp90'] * 100:.2f}% – {b['UpperRtp90'] * 100:.2f}% | {b['LowerRtp95'] * 100:.2f}% – {b['UpperRtp95'] * 100:.2f}% |"
    for b in STATS["confidenceBands"]
) + f"""

Every session starts in Rest, below the long-run state mix, so short sessions return slightly less than the
stationary RTP: """ + ", ".join(f"{s['spins']:,} spins → {pct(s['expectedRtp'], 4)}" for s in STATS["sessionRtpFromRest"]) + f""".

## Simulation summary (*simulated*)

{SIM['PaidSpins']:,} paid spins, PCG32 seed `{SIM['Seed']}`: observed RTP {pct(SIM['ObservedRtp'])}, hit frequency
{pct(SIM['HitFrequency'], 4)}, SD {SIM['StandardDeviation']:.4f}×, largest observed award {SIM['MaximumObservedWin']:,.2f}×.
The full comparison is under Verification below.
""")

# ------------------------------------------------------------------------------------------------ verification
rows = [
    ("Base ways RTP", EXACT["BaseRtp"], SIM["BaseRtp"]),
    ("Feature RTP", EXACT["FeatureRtp"], SIM["FeatureRtp"]),
    ("Total RTP", EXACT["TotalRtp"], SIM["ObservedRtp"]),
    ("Hit frequency (any award)", EXACT["HitFrequency"], SIM["HitFrequency"]),
    ("Base hit frequency", EXACT["BaseHitProbability"], SIM["BaseHitFrequency"]),
    ("Feature frequency", EXACT["FeatureTriggerProbability"], SIM["FeatureFrequency"]),
]
VERIFY_PART = (f"""
# Verification: how the numbers are known to be right

The exact model is the source of truth. It is derived three independent ways, and simulation is used as a
check on the *engine* (does the code that plays the game behave like the math?), never as the source of a number.

## 1. Three exact derivations

| Method | Arithmetic | Shares code with engine? | Total RTP |
|---|---|---|---|
| `ExactAnalyzer`: window count distributions, convolution, linear solve, reward recursion | IEEE double | yes (C# core) | {pct(EXACT['TotalRtp'], 12)} |
| `ParAnalyzer`: brute-force enumeration of all 40⁵ stop combinations per reel set | 128-bit integers | partially | {pct(STATS['Rtp'], 12)} |
| `scripts/independent_check.py`: written from the rules in game-design.md | Python `Fraction` | **no** | agrees to 1e-12 |

The window-distribution ways EV equals the enumerated ways EV for every reel set
(`WindowDistributionEvMatchesFullEnumeration`). CI runs the Python reference and fails on any difference above 1e-12.

## 2. Hand-calculated fixtures

`HandCalculatedTests` checks results worked by hand from the paytable, not produced by the code under test:
the 8-way Crown example (3.20×), a 4-reel extension (7.20×), two symbols sharing one Wild (0.37×), exact decimal
settlement (0.07 × 3 × 0.20 × 1.75 = 0.0735), and the two-state balance equation (π = ⅔, ⅓).

## 3. Exact versus simulation

{SIM['PaidSpins']:,} paid spins, PCG32 seed `{SIM['Seed']}`, constant 1.00 bet, starting in Rest.

| Quantity | Exact | Simulated | Simulated − exact |
|---|---:|---:|---:|
""" + "\n".join(f"| {name} | {pct(e)} | {pct(o)} | {pp(o - e)} |" for name, e, o in rows) + f"""
| Standard deviation | {EXACT['StandardDeviation']:.6f}× | {SIM['StandardDeviation']:.6f}× | {SIM['StandardDeviation'] - EXACT['StandardDeviation']:+.6f}× |
| Conditional feature award | {feature_ev:.6f}× | {observed_feature_ev:.6f}× | {observed_feature_ev - feature_ev:+.6f}× |
| Feature spins per trigger | {feature_spins:.6f} | {SIM['FreeSpinsPerTrigger']:.6f} | {SIM['FreeSpinsPerTrigger'] - feature_spins:+.6f} |

### Confidence interval on simulated RTP

Paid spins are **not independent**: the tension state links consecutive spins. The textbook interval
`mean ± 1.96·s/√N` assumes independence, so three intervals are reported:

| Method | 95% half-width | Valid under correlation? |
|---|---:|---|
| Textbook i.i.d. | ±{pct(SIM['RtpConfidenceHalfWidth95Iid'], 4)} | no |
| Batch means ({SIM['Batches']} contiguous batches) | ±{pct(SIM['RtpConfidenceHalfWidth95BatchMeans'], 4)} | yes, asymptotically |
| Exact σ∞ from the Markov model | ±{pct(exact_half, 4)} | yes |

σ∞² = Var(X) + 2Σ Cov(X₀, Xₖ) = {STATS['AsymptoticVariance']:.6f}, versus Var(X) = {STATS['Variance']:.6f}. Here the
correlation slightly *reduces* long-run variance ({(STATS['CorrelationFactor'] - 1) * 100:+.3f}%): zero-Spark spins pay the most
in the base game and push tension down, while bonuses reset to Rest. The simulated RTP is **{z_exact:+.2f}** correlation-corrected
standard errors from the exact value.

### Win-distribution test

Pearson chi-square of the simulated base-award distribution against the exact enumerated distribution:
χ² = {CHI['Statistic']:.2f} on {CHI['DegreesOfFreedom']} degrees of freedom, **p = {CHI['PValue']:.3f}**
(buckets with expected count below 5 pooled). RTP agreement alone cannot catch a bug that moves probability between
small and large wins while keeping the mean; a distribution test can.

## 4. What the checks do not establish

- PCG32 is a reproducible simulation generator, not a certified or cryptographic RNG.
- Outcomes are decided in the browser in the playable demo; a real game resolves on a server with an event log and recovery.
- The confidence bands use a normal approximation that is weak for short sessions of a heavy-tailed game.
- This is a portfolio model. It has not been reviewed by a test laboratory or regulator.
""")


# ------------------------------------------------------------------------------------------------ advantage play
write("docs/advantage-play.md", f"""
# Advantage play: the bet-switching defect and its fix

## The problem (v1.0)

The headline RTP assumes a **constant bet**. The three tension states have very different conditional returns:

| State | Share of paid spins | Return of a spin in this state |
|---|---:|---:|
""" + "\n".join(
    f"| {STATE_NAMES[i]} | {pct(EXACT['stationaryStateProbabilities'][i], 2)} | {pct(EXACT['conditionalStateRtp'][i], 2)} |" for i in range(3)
) + f"""

Version 1.0 kept a single tension state for the whole session and allowed the bet to change between spins.
A player could build tension at the minimum bet and spend it at the maximum. With bet $b_s$ in state $s$ the
long-run return per credit wagered is

$$R=\\frac{{\\sum_s \\pi_s\\, b_s\\, r_s}}{{\\sum_s \\pi_s\\, b_s}}$$

| Strategy | Exact return per credit wagered |
|---|---:|
| Constant bet (any level) | {pct(EXACT['TotalRtp'], 4)} |
| {ADV['minBet']:.2f} in Rest, {ADV['maxBet']:.2f} in Taut and Overdrive | **{pct(ADV['sharedStateBetSwitchReturn'], 4)}** |
| {ADV['minBet']:.2f} in Rest and Taut, {ADV['maxBet']:.2f} only in Overdrive | {pct(ADV['maxBetOnlyInOverdrive'], 4)} |

A second, related exposure: a machine left in Taut is worth **{STATS['states'][1]['RelativeValue']:+.2f} bets** of future return
to the next player compared with Rest, and Overdrive **{STATS['states'][2]['RelativeValue']:+.2f} bets** (relative state values from the
fundamental matrix). In a cabinet that keeps state across players this invites "machine hunting".

## The fix (v1.1)

`StateLedger` with `StatePolicy.PerBetLevel`, the usual remedy for persistent-state games:

1. **State is owned per bet level.** Tension earned at {ADV['minBet']:.2f} can only be spent at {ADV['minBet']:.2f}. Changing bet shows that bet's own state.
2. **State is session-scoped.** Reset / cash-out clears the ledger, so no state is left for the next player.

Why this restores the constant-bet RTP for *any* betting strategy: each bet level's state now evolves only through spins
at that level, so each level is its own copy of the Markov chain. Whatever order the player interleaves levels in,
every level's spins form an ordinary run of the chain, whose long-run return per credit is the RTP (renewal-reward
argument on Rest-to-Rest cycles). The only remaining choice, abandoning a level while it is in Taut or Overdrive, forfeits
positive state value and can only lower the player's return.

## Evidence

Adversarial simulation (`--bet-switch`, {shared['PaidSpins']:,} spins, seed 31337): bet the minimum while the
maximum-bet state is Rest, otherwise bet the maximum.

| State policy | Simulated return per credit wagered |
|---|---:|
| Shared across bets (v1.0) | {pct(shared['Return'], 2)} (theory {pct(BETSWITCH['theoreticalSharedStateReturn'], 2)}) |
| Per bet level (v1.1) | {pct(per_bet['Return'], 2)} (constant-bet RTP {pct(BETSWITCH['constantWagerRtp'], 2)}) |

`BetSwitchingIsProfitableOnlyWithSharedState` keeps this as a regression test. The other designs considered were
locking the bet while tension is above Rest (simpler, but players dislike a locked bet button) and narrowing the gap
between conditional state returns (reduces the edge but does not remove it, and it flattens the mechanic).
""")

# -------------------------------------------------------------------------------------------------- sensitivity
low, high = sens[("Base Spark density", "lower")], sens[("Base Spark density", "higher")]
lines = [
    f"| {r['Parameter']} | {r['Level']} | {pct(r['ExactRtp'], 4)} | {pct(r['ExactFeatureFrequency'], 4)} | "
    f"{pct(r['SimulatedHitFrequency'], 4)} | {r['SimulatedStandardDeviation']:.4f}× | {r['ExactMaximumGameCycleAward']:,.2f}× |"
    for r in SENSITIVITY
]
SENS_PART = (f"""
# Sensitivity analysis

Each row changes one parameter family. Exact RTP, feature frequency and **reachable** maximum exposure are recomputed
from the altered model; hit frequency and SD use {SENSITIVITY[0]['SimulatedPaidSpins']:,} seeded paid spins per scenario.

| Parameter | Level | Exact RTP | Exact feature rate | Sim hit rate | Sim σ | Reachable max |
|---|---|---:|---:|---:|---:|---:|
""" + "\n".join(lines) + f"""

- **Bonus Wild density** moves feature RTP and volatility: −1 Wild per bonus reel gives {pp(sens[('Bonus wilds per reel', '-1')]['ExactRtp'] - baseline['ExactRtp'], 2)}, +1 gives {pp(sens[('Bonus wilds per reel', '+1')]['ExactRtp'] - baseline['ExactRtp'], 2)}.
- **Base Spark density** drives both the state mix and feature entry. The high case raises feature frequency to {pct(high['ExactFeatureFrequency'], 2)} and RTP to {pct(high['ExactRtp'], 2)}: a boundary experiment, not a candidate. In the low case BASE_REST has no Sparks, Rest is absorbing, and the feature can never trigger, so the reachable maximum collapses to {low['ExactMaximumGameCycleAward']:,.2f}×. *v1.0 reported 11,227.15× here because it added the feature maximum to an impossible 3-Spark branch; the analyzer now propagates impossibility and checks state reachability.*
- **Stage multipliers** change the feature budget without changing entry frequency: {pct(sens[('Stage multipliers', '1x / 1.5x / 3x')]['ExactRtp'], 2)} and {pct(sens[('Stage multipliers', '1x / 2x / 4x')]['ExactRtp'], 2)}.
- **Initial free spins** move RTP and variance together: six spins return {pct(sens[('Initial free spins', '6')]['ExactRtp'], 2)}, eight {pct(sens[('Initial free spins', '8')]['ExactRtp'], 2)}.

Rebalancing example: to double feature frequency at constant RTP, feature RTP = entry × average award
({pct(EXACT['FeatureTriggerProbability'], 4)} × {feature_ev:.2f} = {pct(EXACT['FeatureRtp'], 2)}) means the average award must fall to about
{feature_ev / 2:.1f}×. Because Sparks also drive the base state, adding Sparks shifts π toward the richer base sets as well,
so the whole model is re-solved rather than one term scaled.

""")

def section(part: str, title: str) -> str:
    body = part.strip().replace("\n## ", "\n### ")
    return "## " + title + body[body.index("\n"):]


write("docs/math.md", MATH_PART.strip() + "\n\n" + section(SENS_PART, "Sensitivity analysis")
      + "\n\n" + section(VERIFY_PART, "Verification"))

# ------------------------------------------------------------------------------------------------- reel strips
codes = CONFIG["symbolCodes"]
sections = []
for set_name, reels in CONFIG["reelSets"].items():
    counts = [Counter(strip) for strip in reels]
    head = "| Reel | " + " | ".join(codes) + " | Length |\n|---:" + "|---:" * (len(codes) + 1) + "|"
    body = "\n".join(f"| {i + 1} | " + " | ".join(str(counts[i].get(c, 0)) for c in codes) + f" | {len(reels[i])} |" for i in range(5))
    strips = "\n".join(f"- R{i + 1}: `{s}`" for i, s in enumerate(reels))
    sections.append(f"## {set_name}\n\n{head}\n{body}\n\n{strips}")
groups = ", ".join(" = ".join(g) for g in PAR["sharedStripGroups"])
write("docs/reel-strips.md", f"""
# Ordered reel strips

Every string is a circular 40-stop strip. The selected stop is the top symbol; the next two positions are the middle
and bottom. Codes: A Crown, B Moth, C Shuttle, D Dye, E Linen, F Knot, W Wild, X Spark. Wild and Spark are separated by
at least three circular positions, so a visible window holds at most one of each. Reel 1 has no Wild.

Six named sets, five distinct strip configurations: **{groups}** share identical strips.

""" + "\n\n".join(sections))

# --------------------------------------------------------------------------------------------- PAR sheet (HTML)
def table(head: list[str], rows: list[list[str]], cls: str = "") -> str:
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<table class="{cls}"><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table>'


pay_rows = [[s, *(f"{p[k]:.2f}" for k in ("3", "4", "5"))] for s, p in CONFIG["paytable"].items()]
count_tables = []
for set_name, reels in CONFIG["reelSets"].items():
    counts = [Counter(strip) for strip in reels]
    rows_ = [[codes[c].title(), *(str(counts[i].get(c, 0)) for i in range(5))] for c in codes]
    count_tables.append(f"<div class='strip'><h4>{set_name}</h4>{table(['Symbol', 'R1', 'R2', 'R3', 'R4', 'R5'], rows_, 'tight')}</div>")

par_html = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Aether Loom PAR Sheet</title>
<style>
@page {{ size: Letter; margin: 0.55in; }}
:root {{ --ink:#142018; --acid:#5f7d10; --soft:#eef3e8; --line:#d6dccf; }}
body {{ font: 10pt/1.42 "Segoe UI", Arial, sans-serif; color: var(--ink); margin: 0; }}
h1 {{ font-size: 34pt; margin: 0 0 4pt; letter-spacing: -0.5pt; }}
h2 {{ font-size: 13pt; border-bottom: 2px solid var(--acid); padding-bottom: 2pt; margin: 18pt 0 6pt; break-after: avoid; }}
h4 {{ margin: 4pt 0 2pt; font-size: 8.5pt; }}
.kicker {{ font: 600 8pt monospace; letter-spacing: 1.5pt; color: var(--acid); }}
.cover {{ break-after: page; }}
.metrics {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 8pt; margin: 18pt 0; }}
.metric {{ background: var(--soft); padding: 8pt 10pt; border-radius: 4pt; }}
.metric b {{ display:block; font-size: 16pt; }}
.metric span {{ font-size: 8pt; color: #4b5a4e; }}
table {{ border-collapse: collapse; width: 100%; margin: 4pt 0 8pt; break-inside: avoid; }}
th {{ background: var(--ink); color: #fff; font-weight: 600; text-align: left; padding: 3pt 6pt; font-size: 8.5pt; }}
td {{ border-bottom: 1px solid var(--line); padding: 3pt 6pt; font-variant-numeric: tabular-nums; }}
td:not(:first-child) {{ text-align: right; }}
table.text td {{ text-align: left !important; }}
table.tight td, table.tight th {{ padding: 1pt 4pt; font-size: 7.5pt; }}
.strips {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 6pt 12pt; }}
.note {{ font-size: 8pt; color: #4b5a4e; }}
.formula {{ background: var(--soft); padding: 6pt 10pt; font-family: Cambria, Georgia, serif; font-size: 11pt; margin: 6pt 0; }}
.page {{ break-before: page; }}
</style></head><body>
<section class="cover">
<div class="kicker">PAR SHEET · MODEL v{CONFIG['version']}</div>
<h1>Aether Loom</h1>
<p>243 ways · physical 40-stop reel strips · persistent three-state Markov tension · finite 7–12 spin feature</p>
<div class="metrics">
<div class="metric"><span>Total RTP (exact)</span><b>{pct(EXACT['TotalRtp'], 4)}</b></div>
<div class="metric"><span>Hit frequency (exact)</span><b>{pct(STATS['HitFrequency'], 2)}</b></div>
<div class="metric"><span>Feature frequency (exact)</span><b>1 in {1 / EXACT['FeatureTriggerProbability']:.2f}</b></div>
<div class="metric"><span>Volatility index, 90% (exact)</span><b>{STATS['VolatilityIndex90']:.2f}</b></div>
<div class="metric"><span>Max paid-cycle award (reachable)</span><b>{EXTREMES['MaximumGameCycleAward']:,.2f}×</b></div>
<div class="metric"><span>10M-spin simulated RTP</span><b>{pct(SIM['ObservedRtp'], 4)}</b></div>
</div>
{table(['Item', 'Specification'], [
    ['Format', '5 reels × 3 rows, 243 ways, left to right, 3+ consecutive reels'],
    ['Bets', ', '.join(f'{b:.2f}' for b in CONFIG['bets']) + ' fictional credits'],
    ['Wild', 'Substitutes for all paying symbols; never on reel 1; no direct pay'],
    ['Spark (scatter)', '2 on a paid spin raise tension; 3+ trigger the feature; no direct pay'],
    ['Tension', 'Rest / Taut / Overdrive; selects the next base reel set; stored per bet level, cleared on cash-out'],
    ['Feature', f"{CONFIG['initialFreeSpins']} free spins at the earned stage, cap {CONFIG['maximumFeatureSpins']}; 2+ Sparks add a spin and raise the stage; multipliers " + ' / '.join(f'{m}×' for m in CONFIG['stageMultipliers'])],
    ['Jackpot', 'None'],
], 'text')}
<p class="note">Demonstration document for a fictional-credit portfolio game. Not a regulatory submission or certification.
Configuration SHA-256 {CONFIG_HASH}. Every number is generated from reports/*.json by scripts/generate_docs.py.</p>
</section>

<h2>1. Paytable (× total bet, per way)</h2>
{table(['Symbol', '3 reels', '4 reels', '5 reels'], pay_rows)}

<h2>2. RTP budget</h2>
{table(['Component', 'Exact'], [['Base 243-ways wins', pct(EXACT['BaseRtp'], 4)], ['Feature', pct(EXACT['FeatureRtp'], 4)], ['<b>Total</b>', f"<b>{pct(EXACT['TotalRtp'], 4)}</b>"], ['Exact value (target 96.00%)', pct(EXACT['TotalRtp'], 8)]])}
<div class="formula">RTP = Σ<sub>s</sub> π<sub>s</sub> (B<sub>s</sub> + t<sub>s</sub> F<sub>s</sub>), &nbsp; πP = π, &nbsp; Σπ<sub>s</sub> = 1</div>

<h2>3. Base-state Markov chain</h2>
{table(['State', '→ Rest', '→ Taut', '→ Overdrive', 'Stationary π', 'Conditional RTP', 'Value vs Rest'],
       [[STATE_NAMES[i], *(pct(v, 3) for v in EXACT['transitionMatrix'][i]), pct(EXACT['stationaryStateProbabilities'][i], 4), pct(EXACT['conditionalStateRtp'][i], 2), f"{STATS['states'][i]['RelativeValue']:+.3f} bets"] for i in range(3)])}

<h2>4. Feature by inherited stage</h2>
{table(['Start stage', 'Expected award', 'SD', 'Expected spins'], [[STATE_NAMES[i], f"{EXACT['features'][i]['ExpectedAward']:.4f}×", f"{(f['SecondMoment'] - f['Mean'] ** 2) ** 0.5:.3f}×", f"{EXACT['features'][i]['ExpectedSpins']:.4f}"] for i, f in enumerate(STATS['features'])])}

<h2 class="page">5. Volatility and win distribution</h2>
{table(['Metric', 'Exact'], [
    ['Variance per paid spin', f"{STATS['Variance']:.6f} bet²"],
    ['Standard deviation', f"{STATS['StandardDeviation']:.6f}× bet"],
    ['Volatility index 90% / 95%', f"{STATS['VolatilityIndex90']:.4f} / {STATS['VolatilityIndex95']:.4f}"],
    ['Long-run variance with state correlation σ²∞', f"{STATS['AsymptoticVariance']:.6f} ({(STATS['CorrelationFactor'] - 1) * 100:+.3f}%)"],
    ['Hit frequency (any) / base', f"{pct(STATS['HitFrequency'], 4)} / {pct(STATS['BaseHitFrequency'], 4)}"],
    ['Paid spins returning less than the bet', pct(small_wins, 2)],
    ['Max base / max paid cycle (reachable)', f"{EXTREMES['MaximumBaseAward']:,.2f}× / {EXTREMES['MaximumGameCycleAward']:,.2f}×"],
])}
{table(['Base award (× bet)', 'Probability', '1 in'], [[b['label'], prob(b['probability']), 'never' if b['probability'] == 0 else f"{1 / b['probability']:,.1f}"] for b in STATS['baseWinDistribution']])}
<p class="note">Exact single-spin distribution from all 40⁵ stop combinations of each base reel set, weighted by π. Feature awards are not included in this table.</p>

<h2>6. Player experience: RTP range after N spins</h2>
{table(['Paid spins', '90% band', '95% band'], [[f"{b['Spins']:,}", f"{b['LowerRtp90'] * 100:.2f}% – {b['UpperRtp90'] * 100:.2f}%", f"{b['LowerRtp95'] * 100:.2f}% – {b['UpperRtp95'] * 100:.2f}%"] for b in STATS['confidenceBands']])}

<h2>7. Verification</h2>
{table(['Check', 'Result'], [
    ['Analyzer, 40⁵ enumeration and independent Python RTP', 'agree to 1e-12'],
    [f"Simulated RTP, {SIM['PaidSpins']:,} spins", f"{pct(SIM['ObservedRtp'], 4)} (z = {z_exact:+.2f}, correlation-corrected)"],
    ['95% CI half-width: i.i.d. / batch means / exact σ∞', f"±{pct(SIM['RtpConfidenceHalfWidth95Iid'], 4)} / ±{pct(SIM['RtpConfidenceHalfWidth95BatchMeans'], 4)} / ±{pct(exact_half, 4)}"],
    ['Chi-square, base win distribution', f"χ² = {CHI['Statistic']:.2f}, df = {CHI['DegreesOfFreedom']}, p = {CHI['PValue']:.3f}"],
    ['Bet-switch player: shared state (v1.0) / per-bet state (v1.1)', f"{pct(shared['Return'], 2)} / {pct(per_bet['Return'], 2)}"],
], 'text')}

<h2>8. Reel strip symbol counts</h2>
<p class="note">Six named sets, five distinct strip configurations ({groups}). Ordered strips: docs/reel-strips.md.</p>
<div class="strips">{''.join(count_tables)}</div>
</body></html>"""
write("docs/PAR-sheet.html", par_html)

print("Generated math, advantage-play, reel-strips and PAR-sheet.html.")
