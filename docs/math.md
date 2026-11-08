# Aether Loom mathematical model

Model version 1.1.0 · configuration SHA-256 `59f14982727e`. Every figure on this page is **exact**
(computed from the reel strips, not sampled) unless it is marked *simulated*.

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | 80.431642% |
| Weave Ascending feature | 16.135273% |
| Scatter cash awards / jackpots | none |
| **Total RTP** | **96.566915%** |

A Python implementation written from the rules alone, in exact fractions
(`scripts/independent_check.py`), reproduces it to 1e-12.

$$\text{RTP}=\sum_{s}\pi_s\,(B_s+t_sF_s),\qquad \pi P=\pi,\ \textstyle\sum_s\pi_s=1$$

where $B_s$ is the base ways EV of state $s$'s reel set, $t_s$ its 3+ Spark probability, and $F_s$ the expected feature
award when the feature starts at stage $s$.

## Volatility profile

| Metric | Exact value |
|---|---:|
| Hit frequency (any award, incl. feature) | 65.0918% (1 in 1.536) |
| Base hit frequency | 64.8210% |
| Paid spins returning less than the bet | 39.31% of all paid spins |
| Feature frequency | 0.4206% (1 in 237.74) |
| Average feature award | 38.3603× bet over 8.3705 spins |
| Variance per paid spin | 14.912377 bet² |
| Standard deviation | 3.861655× bet |
| Volatility index (90%, 1.645σ) | 6.3519 |
| Volatility index (95%, 1.960σ) | 7.5687 |
| Long-run variance with state correlation, σ²∞ | 14.831976 bet² (-0.539% vs. i.i.d.) |
| Maximum base award (reachable) | 88.00× |
| Maximum paid-cycle award (reachable) | 11,227.60× |

Variance, hit frequency and the win distribution come from enumerating all 40⁵ = 102,400,000 stop combinations
of each distinct reel set, with awards held as integers (hundredths of a bet) in 128-bit accumulators, so they
carry no sampling or rounding error. The feature's second moment uses backward induction that conditions on the
Spark class, because the event that extends the feature (2+ Sparks) is correlated with the award of the same spin.

## Base-game win distribution (stationary mix)

| Single-spin base award | Probability | 1 in |
|---|---:|---:|
| 0× | 35.1790% | 2.8 |
| (0, 1)× | 39.3069% | 2.5 |
| [1, 2)× | 13.7799% | 7.3 |
| [2, 5)× | 9.5891% | 10.4 |
| [5, 10)× | 1.9021% | 52.6 |
| [10, 20)× | 0.2331% | 428.9 |
| [20, 50)× | 0.0098% | 10,191.4 |
| [50, 100)× | 1.639e-08 | 61,008,686.8 |
| 100+× | 0 (impossible) | never |

## Base-state Markov chain

Rows are the current state; columns are the next state.

| Current state | Rest | Taut | Overdrive | Stationary π | Conditional RTP | Value vs. Rest |
|---|---:|---:|---:|---:|---:|---:|
| Rest | 95.5481% | 4.4519% | 0.0000% | 93.2116% | 91.294% | +0.0000 bets |
| Taut | 58.0910% | 34.0913% | 7.8177% | 6.2962% | 153.090% | +1.1844 bets |
| Overdrive | 100.0000% | 0.0000% | 0.0000% | 0.4922% | 372.091% | +2.7552 bets |

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
| 1,000 | 76.53% – 116.60% | 72.70% – 120.44% |
| 10,000 | 90.23% – 102.90% | 89.02% – 104.12% |
| 100,000 | 94.56% – 98.57% | 94.18% – 98.95% |
| 1,000,000 | 95.93% – 97.20% | 95.81% – 97.32% |
| 10,000,000 | 96.37% – 96.77% | 96.33% – 96.81% |

Every session starts in Rest, below the long-run state mix, so short sessions return slightly less than the
stationary RTP: 10 spins → 95.6856%, 100 spins → 96.4788%, 1,000 spins → 96.5581%, 10,000 spins → 96.5660%.

## Simulation summary (*simulated*)

10,000,000 paid spins, PCG32 seed `20260908`: observed RTP 96.638977%, hit frequency
65.1093%, SD 3.8754×, largest observed award 731.79×.
The full comparison is under Verification below.

## Verification

The exact model is the source of truth. It is derived three independent ways, and simulation is used as a
check on the *engine* (does the code that plays the game behave like the math?), never as the source of a number.

### 1. Three exact derivations

| Method | Arithmetic | Shares code with engine? | Total RTP |
|---|---|---|---|
| `ExactAnalyzer`: window count distributions, convolution, linear solve, reward recursion | IEEE double | yes (C# core) | 96.566914833464% |
| `ParAnalyzer`: brute-force enumeration of all 40⁵ stop combinations per reel set | 128-bit integers | partially | 96.566914833464% |
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
| Base ways RTP | 80.431642% | 80.499687% | +0.0680 pp |
| Feature RTP | 16.135273% | 16.139290% | +0.0040 pp |
| Total RTP | 96.566915% | 96.638977% | +0.0721 pp |
| Hit frequency (any award) | 65.091762% | 65.109340% | +0.0176 pp |
| Base hit frequency | 64.820986% | 64.838740% | +0.0178 pp |
| Feature frequency | 0.420624% | 0.420600% | -0.0000 pp |
| Standard deviation | 3.861655× | 3.875392× | +0.013737× |
| Conditional feature award | 38.360288× | 38.372063× | +0.011775× |
| Feature spins per trigger | 8.370532 | 8.370447 | -0.000085 |

### Confidence interval on simulated RTP

Paid spins are **not independent**: the tension state links consecutive spins. The textbook interval
`mean ± 1.96·s/√N` assumes independence, so three intervals are reported:

| Method | 95% half-width | Valid under correlation? |
|---|---:|---|
| Textbook i.i.d. | ±0.2402% | no |
| Batch means (100 contiguous batches) | ±0.2582% | yes, asymptotically |
| Exact σ∞ from the Markov model | ±0.2387% | yes |

σ∞² = Var(X) + 2Σ Cov(X₀, Xₖ) = 14.831976, versus Var(X) = 14.912377. Here the
correlation slightly *reduces* long-run variance (-0.539%): zero-Spark spins pay the most
in the base game and push tension down, while bonuses reset to Rest. The simulated RTP is **+0.59** correlation-corrected
standard errors from the exact value.

### Win-distribution test

Pearson chi-square of the simulated base-award distribution against the exact enumerated distribution:
χ² = 5.64 on 6 degrees of freedom, **p = 0.464**
(buckets with expected count below 5 pooled). RTP agreement alone cannot catch a bug that moves probability between
small and large wins while keeping the mean; a distribution test can.

### 4. What the checks do not establish

- PCG32 is a reproducible simulation generator, not a certified or cryptographic RNG.
- Outcomes are decided in the browser in the playable demo; a real game resolves on a server with an event log and recovery.
- The confidence bands use a normal approximation that is weak for short sessions of a heavy-tailed game.
- This is a portfolio model. It has not been reviewed by a test laboratory or regulator.
