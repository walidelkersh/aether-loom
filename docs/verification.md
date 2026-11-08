# Verification: how the numbers are known to be right

Model 1.1.0 · configuration SHA-256 `9959f5e5e14a`.

The exact model is the source of truth. It is derived three independent ways, and simulation is used as a
check on the *engine* (does the code that plays the game behave like the math?), never as the source of a number.

## 1. Three independent exact derivations

| Method | Arithmetic | Shares code with engine? | Total RTP |
|---|---|---|---|
| `ExactAnalyzer`: window count distributions, convolution, linear solve, reward recursion | IEEE double | yes (C# core) | 96.566914833464% |
| `RationalAnalyzer`: closed-form ways EV, Gaussian elimination over ℚ | BigInteger fractions | partially (strip loader) | identical fraction |
| `ParAnalyzer`: brute-force enumeration of all 40⁵ stop combinations per reel set | 128-bit integers | partially | 96.566914833464% |
| `scripts/independent_check.py`: written from the rules in game-design.md | Python `Fraction` | **no** | identical fraction |

The closed-form ways EV equals the enumerated ways EV as exact fractions for every reel set
(`ClosedFormWaysEvEqualsFullEnumerationExactly`). CI runs the Python reference and fails on any difference above 1e-12.

## 2. Hand-calculated fixtures

`HandCalculatedTests` checks results worked by hand from the paytable, not produced by the code under test:
the 8-way Crown example (3.20×), a 4-reel extension (7.20×), two symbols sharing one Wild (0.37×), exact decimal
settlement (0.07 × 3 × 0.20 × 1.75 = 0.0735), and the two-state balance equation (π = ⅔, ⅓).

## 3. Exact versus simulation

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

## 4. What the checks do not establish

- PCG32 is a reproducible simulation generator, not a certified or cryptographic RNG.
- Outcomes are decided in the browser in the playable demo; a real game resolves on a server with an event log and recovery.
- The confidence bands use a normal approximation that is weak for short sessions of a heavy-tailed game.
- This is a portfolio model. It has not been reviewed by a test laboratory or regulator.
