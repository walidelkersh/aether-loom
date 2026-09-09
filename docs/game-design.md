# Aether Loom — game design and math specification

Status: pre-implementation specification, version 0.1. Numeric outputs in this document are design inputs or targets until replaced by generated verification results.

## Game overview

**Aether Loom** is an original 5×3, 243-ways slot about a clockwork loom weaving light into fabric. Every paid spin can leave the loom in a different tension state. Higher tension selects a different physical reel set; the bonus inherits the current tension and can climb through increasingly volatile stages.

The bet is one fictional-credit amount per spin. Available demo bets are 0.20, 0.50, 1, 2, 5, and 10 credits. All paytable entries are multipliers of the total bet. There are no deposits, purchases, withdrawals, or real-money functions.

## Intended player experience

The base game should feel legible and relatively steady at Rest, show anticipation at Taut, and create a brief higher-risk opportunity at Overdrive. The feature should start with the state earned in the base game and become increasingly volatile as the loom tightens.

The relationship between experience and mathematics is explicit:

| Player experience | Mathematical control |
|---|---|
| Rest feels stable | Rest reel-set symbol counts and low wild density |
| Two Sparks create visible anticipation | Exact two-scatter probability in the active strip set controls upward state transitions |
| Overdrive is brief and punchy | State 2 always vents to Rest after its paid spin |
| Bonus intensity rises | Stage-specific wild counts and win multipliers increase at each stage |
| Bonus escalation is uncertain but bounded | Two-or-more-scatter stage-transition probability and a 12-spin hard cap |

## Grid and win evaluation

- Format: five reels, three visible rows.
- Win system: 243 ways.
- Direction: consecutive reels from the leftmost reel.
- A symbol pays when it appears at least once on each of three, four, or five consecutive reels. The number of ways is the product of matching symbol counts on those reels.
- Only the longest occurrence of a symbol pays on a spin.
- Different paying symbols are evaluated independently and their wins are added.
- The Aether Wild substitutes for every regular paying symbol. It does not substitute for the Spark scatter.
- Aether Wild has no separate pay. Reel 1 contains no wild, preventing an all-wild ambiguity at the start of a way.
- Spark is an anywhere scatter used for state and bonus rules. It has no direct cash award.

## Symbols and initial paytable

| Code | Symbol | 3 reels | 4 reels | 5 reels | Role |
|---|---|---:|---:|---:|---|
| CROWN | Crown Spool | 0.40 | 1.20 | 4.00 | High |
| MOTH | Silk Moth | 0.30 | 0.90 | 2.50 | High |
| SHUTTLE | Copper Shuttle | 0.20 | 0.60 | 1.50 | Medium |
| DYE | Dye Vial | 0.15 | 0.40 | 1.00 | Medium |
| LINEN | Linen Roll | 0.10 | 0.25 | 0.60 | Low |
| KNOT | Weaver's Knot | 0.07 | 0.15 | 0.40 | Low |
| WILD | Aether Wild | — | — | — | Substitute |
| SPARK | Spark | — | — | — | Scatter/state |

The final paytable and strip counts may be tuned before version 1.0. Any tuning must be committed in inspectable configuration and all reports regenerated from that configuration.

## Reel architecture

Every reel is a circular ordered strip. A uniformly selected stop is the top visible symbol; the next two strip positions form the middle and bottom symbols. Base states and feature stages use separate five-reel sets of equal-length strips so state changes are auditable.

Planned reel sets:

| Set | Use | Length per reel | Constraint |
|---|---|---:|---|
| BASE_REST | Base state 0 | 48 | One Spark per reel; no Wild on reel 1 |
| BASE_TAUT | Base state 1 | 48 | One or two Sparks per reel; no Wild on reel 1 |
| BASE_OVERDRIVE | Base state 2 | 48 | Two Sparks per reel; increased Wilds on reels 2–5 |
| BONUS_REST | Feature stage 0 | 48 | Increased Wilds; no Wild on reel 1 |
| BONUS_TAUT | Feature stage 1 | 48 | Further increased Wilds; no Wild on reel 1 |
| BONUS_OVERDRIVE | Feature stage 2 | 48 | Highest Wild density; no Wild on reel 1 |

Spark positions must be separated by at least three strip indices, including across the circular boundary, so one reel cannot show two Sparks in the same 3-row window. Wilds are also spaced to avoid unintended stacked-wild windows. The exact ordered strips and symbol-count tables will live in `config/reel-strips.json` and be reproduced in the generated PAR sheet.

## Persistent Tension mechanic

The base game has three states: Rest (0), Taut (1), and Overdrive (2). The state before a paid spin selects its reel set.

After evaluating the paid spin:

1. Three or more Sparks trigger the feature. After the feature, the base state resets to Rest.
2. An Overdrive paid spin vents and resets to Rest, regardless of its non-triggering Spark count.
3. In Rest or Taut, exactly two Sparks increase tension by one state.
4. Zero Sparks decrease tension by one state, with Rest as the floor.
5. Exactly one Spark holds the current state.

This creates a three-state Markov chain whose transition matrix is calculated from the exact scatter-count distribution of each physical reel set. The stationary distribution determines how frequently each base reel set is used and therefore contributes directly to long-run RTP.

## Weave Ascending bonus

- Trigger: three or more Sparks anywhere on a paid spin.
- Initial award: 7 free spins.
- Starting stage: the feature inherits the base tension state that triggered it.
- Stage reel set: BONUS_REST, BONUS_TAUT, or BONUS_OVERDRIVE according to stage 0, 1, or 2.
- Win multipliers: 1×, 1.75×, and 3.5× at stages 0, 1, and 2 respectively.
- Escalation: two or more Sparks on a feature spin advance the stage by one, to a maximum of stage 2, and award one additional free spin.
- Bound: at most 12 feature spins are played, including awarded spins.
- Feature Sparks do not pay cash directly.
- There is no separate arbitrary win cap in the demonstration model. The finite strips, paytable, seven initial spins, and 12-spin feature bound produce an exact maximum game-cycle award of 11,227.60× bet in version 1.0. The `ExtremeAnalyzer` derives this value from reachable window profiles and bonus transitions.

The bonus is evaluated as a finite Markov reward process. Exact per-stage transition probabilities and conditional expected awards feed a dynamic program indexed by stage, spins remaining, and spins already played. Simulation independently exercises the state loop.

## RTP budget and mathematical goals

The intended total RTP range is 94%–97%. This is a design range, not a result. The implemented model will report:

\[
RTP_{total}=RTP_{base\ ways}+RTP_{feature}.
\]

Base ways RTP is averaged across the stationary base-state distribution. Feature RTP weights the exact trigger probability and exact expected feature value for each possible inherited starting stage. Spark has no direct cash RTP and there is no jackpot component.

Other goals:

- feature frequency should be infrequent enough to feel distinct while still observable in a short demo session;
- the Overdrive reel set should have higher conditional RTP and higher variance than Rest;
- the stage multipliers and wild density should make late feature spins materially more volatile;
- long-run maximum exposure must remain bounded by the finite feature duration and derived from reachable strip windows;
- reported theoretical quantities must be generated from the exact strips and rules;
- simulation estimates must include sample size, seed, and a 95% normal confidence interval for RTP.

## Analysis classification

Planned exact calculations:

- per-reel stop and visible-window distributions;
- scatter-count distribution per reel set;
- base-state transition matrix and stationary distribution;
- per-symbol ways expected value per reel set;
- feature-entry probability;
- finite-state expected feature value and expected duration;
- maximum award implied by the paytable, ways rules, multipliers, and cap.

Planned simulation estimates:

- total and component RTP as an independent implementation check;
- hit frequency;
- payout variance, standard deviation, and volatility index;
- empirical feature frequency and duration;
- observed maximum win;
- RTP confidence interval;
- sensitivity effects on hit rate and volatility where exact calculation is not practical.

No result is considered verified until the C# test suite, exact analyzer, and seeded simulation agree within the documented statistical tolerance.
