# Aether Loom mathematical model

Model version 1.1.0 · configuration SHA-256 `9959f5e5e14a`. Every figure on this page is **exact**
(computed from the reel strips, not sampled) unless it is marked *simulated*.

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | 80.431642% |
| Weave Ascending feature | 16.135273% |
| Scatter cash awards / jackpots | none |
| **Total RTP** | **96.566915%** |

As an exact fraction (BigInteger arithmetic, no floating point), total RTP has a 83-digit
denominator and equals `0.96566914833464226035…`. A Python implementation written from the rules alone
(`scripts/independent_check.py`) produces the identical fraction.

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
| 100+× | 0 (impossible) | — |

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
Full comparison in [verification.md](verification.md).
