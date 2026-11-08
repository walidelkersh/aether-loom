# Aether Loom mathematical model

Model version 1.2.0 · configuration SHA-256 `3856935804e7`. Every figure on this page is **exact**
(computed from the reel strips, not sampled) unless it is marked *simulated*.

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | 79.8647% |
| Weave Ascending feature | 16.1353% |
| Scatter cash awards / jackpots | none |
| **Total RTP** | **96.0000%** |

Target 96.00%. The exact value is 95.99999339%, 0.0000066 percentage points from target.
It was reached by downgrading eight regular-symbol stops on BASE_REST and BASE_TAUT (for example Moth to Shuttle).
No Wild or Spark moved, so the transition matrix, the stationary mix, trigger rates and the feature are unchanged;
only base ways EV moved.

A Python implementation written from the rules alone, in exact fractions
(`scripts/independent_check.py`), reproduces it to 1e-12.

$$\text{RTP}=\sum_{s}\pi_s\,(B_s+t_sF_s),\qquad \pi P=\pi,\ \textstyle\sum_s\pi_s=1$$

where $B_s$ is the base ways EV of state $s$'s reel set, $t_s$ its 3+ Spark probability, and $F_s$ the expected feature
award when the feature starts at stage $s$.

## Volatility profile

| Metric | Exact value |
|---|---:|
| Hit frequency (any award, incl. feature) | 64.0889% (1 in 1.560) |
| Base hit frequency | 63.8152% |
| Paid spins returning less than the bet | 38.58% of all paid spins |
| Feature frequency | 0.4206% (1 in 237.74) |
| Average feature award | 38.3603× bet over 8.3705 spins |
| Variance per paid spin | 14.931998 bet² |
| Standard deviation | 3.864194× bet |
| Volatility index (90%, 1.645σ) | 6.3560 |
| Volatility index (95%, 1.960σ) | 7.5737 |
| Long-run variance with state correlation, σ²∞ | 14.851265 bet² (-0.541% vs. i.i.d.) |
| Maximum base award (reachable) | 88.00× |
| Maximum paid-cycle award (reachable) | 11,227.60× |

Variance, hit frequency and the win distribution come from enumerating all 40⁵ = 102,400,000 stop combinations
of each distinct reel set, with awards held as integers (hundredths of a bet) in 128-bit accumulators, so they
carry no sampling or rounding error. The feature's second moment uses backward induction that conditions on the
Spark class, because the event that extends the feature (2+ Sparks) is correlated with the award of the same spin.

## Base-game win distribution (stationary mix)

| Single-spin base award | Probability | 1 in |
|---|---:|---:|
| 0× | 36.1848% | 2.8 |
| (0, 1)× | 38.5812% | 2.6 |
| [1, 2)× | 13.3746% | 7.5 |
| [2, 5)× | 9.7117% | 10.3 |
| [5, 10)× | 1.9047% | 52.5 |
| [10, 20)× | 0.2333% | 428.7 |
| [20, 50)× | 0.0097% | 10,335.5 |
| [50, 100)× | 1.639e-08 | 61,008,686.8 |
| 100+× | 0 (impossible) | never |

## Base-state Markov chain

Rows are the current state; columns are the next state.

| Current state | Rest | Taut | Overdrive | Stationary π | Conditional RTP | Value vs. Rest |
|---|---:|---:|---:|---:|---:|---:|
| Rest | 95.5481% | 4.4519% | 0.0000% | 93.2116% | 90.688% | +0.0000 bets |
| Taut | 58.0910% | 34.0913% | 7.8177% | 6.2962% | 153.061% | +1.1932 bets |
| Overdrive | 100.0000% | 0.0000% | 0.0000% | 0.4922% | 372.091% | +2.7609 bets |

*Value vs. Rest* is $v = Z(m-\mu\mathbf{1})$ with fundamental matrix $Z=(I-P+\mathbf{1}\pi)^{-1}$: the extra
long-run return, in bets, of a session currently in that state compared with one in Rest. It quantifies what a
saved state is worth to the next player (see [advantage-play.md](advantage-play.md)).

## Feature by inherited stage

| Starting stage | Expected award | SD of award | Expected spins | P(feature pays 0) |
|---|---:|---:|---:|---:|
| Rest | 32.431583× | 35.9898× | 8.370532 | 5.02e-05 |
| Taut | 61.467090× | 45.9861× | 8.370532 | 1.39e-05 |
| Overdrive | 96.017658× | 50.0321× | 8.370532 | 6.02e-07 |

Expected duration is identical for every stage because the three bonus reel sets share one Spark distribution
(escalation probability is the same at every stage), not because of a separate design rule.

## Player experience: RTP range after N paid spins

Uses the correlation-corrected σ∞, constant bet. A session of N spins lands inside the band with the stated probability
(normal approximation, which is poor for very short sessions of a heavy-tailed game).

| Paid spins | 90% band | 95% band |
|---:|---:|---:|
| 1,000 | 75.95% – 116.05% | 72.11% – 119.89% |
| 10,000 | 89.66% – 102.34% | 88.45% – 103.55% |
| 100,000 | 94.00% – 98.00% | 93.61% – 98.39% |
| 1,000,000 | 95.37% – 96.63% | 95.24% – 96.76% |
| 10,000,000 | 95.80% – 96.20% | 95.76% – 96.24% |

Every session starts in Rest, below the long-run state mix, so short sessions return slightly less than the
stationary RTP: 10 spins → 95.1128%, 100 spins → 95.9113%, 1,000 spins → 95.9911%, 10,000 spins → 95.9991%.

## Simulation summary (*simulated*)

10,000,000 paid spins, PCG32 seed `20260908`: observed RTP 96.057740%, hit frequency
64.1010%, SD 3.8780×, largest observed award 731.79×.
The full comparison is under Verification below.

## Sensitivity analysis

Each row changes one parameter family. Exact RTP, feature frequency and **reachable** maximum exposure are recomputed
from the altered model; hit frequency and SD use 500,000 seeded paid spins per scenario.

| Parameter | Level | Exact RTP | Exact feature rate | Sim hit rate | Sim σ | Reachable max |
|---|---|---:|---:|---:|---:|---:|
| Baseline | configured | 96.0000% | 0.4206% | 64.0102% | 3.8551× | 11,227.60× |
| Bonus wilds per reel | -1 | 92.6698% | 0.4206% | 64.1298% | 3.1858× | 11,227.60× |
| Bonus wilds per reel | +1 | 100.1833% | 0.4206% | 64.2262% | 4.6643× | 11,563.60× |
| Base Spark density | lower | 94.7796% | 0.0000% | 69.1186% | 1.5207× | 49.60× |
| Base Spark density | higher | 211.7402% | 3.1288% | 61.0100% | 11.0464× | 11,247.30× |
| Stage multipliers | 1x / 1.5x / 3x | 93.9486% | 0.4206% | 64.1058% | 3.3242× | 9,626.60× |
| Stage multipliers | 1x / 2x / 4x | 98.0514% | 0.4206% | 64.1030% | 4.3864× | 12,828.60× |
| Initial free spins | 6 | 92.7361% | 0.4206% | 64.1332% | 3.3431× | 10,058.60× |
| Initial free spins | 8 | 99.3832% | 0.4206% | 64.1950% | 4.5136× | 12,396.60× |

- **Bonus Wild density** moves feature RTP and volatility: −1 Wild per bonus reel gives -3.33 pp, +1 gives +4.18 pp.
- **Base Spark density** drives both the state mix and feature entry. The high case raises feature frequency to 3.13% and RTP to 211.74%: a boundary experiment, not a candidate. In the low case BASE_REST has no Sparks, Rest is absorbing, and the feature can never trigger, so the reachable maximum collapses to 49.60×. *v1.0 reported 11,227.15× here because it added the feature maximum to an impossible 3-Spark branch; the analyzer now propagates impossibility and checks state reachability.*
- **Stage multipliers** change the feature budget without changing entry frequency: 93.95% and 98.05%.
- **Initial free spins** move RTP and variance together: six spins return 92.74%, eight 99.38%.

Rebalancing example: to double feature frequency at constant RTP, feature RTP = entry × average award
(0.4206% × 38.36 = 16.14%) means the average award must fall to about
19.2×. Because Sparks also drive the base state, adding Sparks shifts π toward the richer base sets as well,
so the whole model is re-solved rather than one term scaled.

## Verification

The exact model is the source of truth. It is derived three independent ways, and simulation is used as a
check on the *engine* (does the code that plays the game behave like the math?), never as the source of a number.

### 1. Three exact derivations

| Method | Arithmetic | Shares code with engine? | Total RTP |
|---|---|---|---|
| `ExactAnalyzer`: window count distributions, convolution, linear solve, reward recursion | IEEE double | yes (C# core) | 95.999993390653% |
| `ParAnalyzer`: brute-force enumeration of all 40⁵ stop combinations per reel set | 128-bit integers | partially | 95.999993390654% |
| `scripts/independent_check.py`: written from the rules in game-design.md | Python `Fraction` | **no** | agrees to 1e-12 |

The window-distribution ways EV equals the enumerated ways EV for every reel set
(`WindowDistributionEvMatchesFullEnumeration`). CI runs the Python reference and fails on any difference above 1e-12.

### 2. Hand-calculated fixtures

`HandCalculatedTests` checks results worked by hand from the paytable, not produced by the code under test:
the 8-way Crown example (3.20×), a 4-reel extension (7.20×), two symbols sharing one Wild (0.37×), exact decimal
settlement (0.07 × 3 × 0.20 × 1.75 = 0.0735), and the two-state balance equation (π = ⅔, ⅓).

### 3. Exact versus simulation

10,000,000 paid spins, PCG32 seed `20260908`, constant 1.00 bet, starting in Rest.

| Quantity | Exact | Simulated | Simulated − exact |
|---|---:|---:|---:|
| Base ways RTP | 79.864720% | 79.918450% | +0.0537 pp |
| Feature RTP | 16.135273% | 16.139290% | +0.0040 pp |
| Total RTP | 95.999993% | 96.057740% | +0.0577 pp |
| Hit frequency (any award) | 64.088865% | 64.100960% | +0.0121 pp |
| Base hit frequency | 63.815205% | 63.827490% | +0.0123 pp |
| Feature frequency | 0.420624% | 0.420600% | -0.0000 pp |
| Standard deviation | 3.864194× | 3.878037× | +0.013842× |
| Conditional feature award | 38.360288× | 38.372063× | +0.011775× |
| Feature spins per trigger | 8.370532 | 8.370447 | -0.000085 |

### Confidence interval on simulated RTP

Paid spins are **not independent**: the tension state links consecutive spins. The textbook interval
`mean ± 1.96·s/√N` assumes independence, so three intervals are reported:

| Method | 95% half-width | Valid under correlation? |
|---|---:|---|
| Textbook i.i.d. | ±0.2404% | no |
| Batch means (100 contiguous batches) | ±0.2617% | yes, asymptotically |
| Exact σ∞ from the Markov model | ±0.2389% | yes |

σ∞² = Var(X) + 2Σ Cov(X₀, Xₖ) = 14.851265, versus Var(X) = 14.931998. Here the
correlation slightly *reduces* long-run variance (-0.541%): zero-Spark spins pay the most
in the base game and push tension down, while bonuses reset to Rest. The simulated RTP is **+0.47** correlation-corrected
standard errors from the exact value.

### Win-distribution test

Pearson chi-square of the simulated base-award distribution against the exact enumerated distribution:
χ² = 5.37 on 6 degrees of freedom, **p = 0.497**
(buckets with expected count below 5 pooled). RTP agreement alone cannot catch a bug that moves probability between
small and large wins while keeping the mean; a distribution test can.

### 4. What the checks do not establish

- PCG32 is a reproducible simulation generator, not a certified or cryptographic RNG.
- Outcomes are decided in the browser in the playable demo; a real game resolves on a server with an event log and recovery.
- The confidence bands use a normal approximation that is weak for short sessions of a heavy-tailed game.
- This is a portfolio model. It has not been reviewed by a test laboratory or regulator.
