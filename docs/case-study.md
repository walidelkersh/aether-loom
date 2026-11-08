# Aether Loom case study

## The design problem

My previous slot project could simulate paylines, free spins, Hold & Spin, and jackpots, but it sampled each visible cell from a weight table. That made it useful as an experiment and weak as evidence that I could design physical strips, connect a mechanic to an RTP budget, and verify one implemented model from several directions.

For Aether Loom I started with a stricter question: can a stateful mechanic be clear enough to understand during one spin while still creating a real mathematical control problem?

## The player experience

The loom has three tension states. Rest should feel stable, Taut should create anticipation, and Overdrive should be brief and more volatile. Two Sparks tighten the loom; zero lets tension fall; one holds; and three or more start the feature. Overdrive always vents after one paid spin.

The experience maps directly to parameters. Exact two-Spark probability controls how quickly tension rises. State-specific Wild counts control conditional EV. The forced Overdrive reset limits high-state occupancy. During the bonus, stage-specific reel sets and the 1×/1.75×/3.5× schedule control how sharply volatility rises.

## The mathematical model

The paid game is a three-state Markov chain. Each state's scatter-count distribution produces one row of the transition matrix. Its stationary distribution is 93.2116% Rest, 6.2962% Taut, and 0.4922% Overdrive. I use that distribution to weight state-specific base EV and feature triggers.

The bonus is a finite Markov reward process. Its state contains the tension stage, queued spins, and spins already played. A two-Spark outcome raises the stage and preserves the queue by replacing the consumed spin; any other outcome reduces the queue. The 12-spin limit guarantees termination and bounds exposure.

## The reel design

The model uses six named reel sets (five distinct strip configurations, because BASE_OVERDRIVE and BONUS_REST share strips) with five circular 40-stop strips each. A stop determines the top symbol and the following two entries determine the rest of the window. Wild and Spark symbols have at least two intervening positions, including around the circular boundary, so neither can stack in a three-row window. Reel 1 has no Wild, which removes all-Wild ambiguity in the ways evaluator.

I stored the strips as compact ordered strings in the canonical JSON rather than only as symbol counts. That makes both distribution and adjacency available for review.

## RTP allocation

The exact long-run RTP is 96.566915%. Base ways wins contribute 80.431642%; Weave Ascending contributes 16.135273%. Spark pays nothing directly and there is no jackpot contribution.

I reached this result by adjusting meaningful game inputs: strip composition, individual symbol pays, and feature multipliers. I did not add a global pay-scale value to make the final number look exact.

## The original feature

A feature trigger inherits the paid-spin tension state. Seven free spins therefore begin on Rest, Taut, or Overdrive bonus strips rather than resetting to a generic free-game mode. Two Sparks add a spin and advance the stage. Expected feature awards are 32.431583×, 61.467090×, and 96.017658× for the three starting stages.

This inheritance gives the persistent meter a payoff the player can see: reaching Taut or Overdrive before a trigger changes the bonus immediately.

## Balancing

The final multiplier schedule is 1×, 1.75×, and 3.5×. A 1×/2×/4× schedule produced 98.6183% in the controlled sensitivity run; the selected schedule kept the same escalation shape while returning the total to 96.566915%.

The exact feature rate is 0.420624%, or 1 in 237.74 paid spins. That rate is mostly determined by Rest because Rest occupies more than 93% of paid spins. This made Rest strip scatter placement more influential than a quick glance at the Overdrive set would suggest.

## Simulation

The final Monte Carlo run used 10,000,000 paid spins and PCG32 seed 20260908. It produced 96.638977% RTP. Because the tension state links consecutive spins, I report three 95% intervals: the textbook i.i.d. interval (±0.2402 pp), a batch-means interval (±0.2582 pp), and one from the exact long-run variance of the Markov model (±0.2387 pp). The exact RTP is 0.59 correlation-corrected standard errors from the simulated value, and a chi-square test of the simulated base win distribution against the exact enumerated distribution gives p = 0.46.

The largest observed cycle paid 731.79×. That is an observed value, not the maximum; the reachable maximum is 11,227.60×.

## Exact verification

I compute exact ways EV from each reel's enumerated visible-window count distribution. Scatter polynomials give exact trigger probabilities. Feature EV and duration come from finite recursion. A second dynamic program over reachable window profiles calculates the exact 11,227.60× maximum game cycle.

Version 1.1 adds two more exact derivations. A rational analyzer repeats the RTP in BigInteger fractions using the closed form for ways EV, and a full enumeration of all 40⁵ stop combinations per reel set makes variance (14.912377 bet², SD 3.861655×), hit frequency (65.0918%), the volatility index (6.35 at 90%) and the win distribution exact rather than simulated. A Python script written from the rules, sharing no code with the C# core, reproduces the identical RTP fraction, and CI fails if it ever disagrees.

## Sensitivity analysis

I changed four parameter families one at a time. One fewer Wild on reels 2–5 of every bonus set reduced exact total RTP to 93.2367%; one more raised it to 100.7503%. A deliberately broad high-Spark case raised entry frequency to 3.1288% and RTP to 212.2207%, demonstrating how quickly the feature budget breaks when state and trigger probabilities move together. Six initial spins returned 93.3031%; eight returned 99.9501%.

These experiments make the controls explicit. Wild density primarily changes feature EV and variance. Spark density changes state occupancy, trigger rate, and the mix of inherited stages. Multipliers change feature payout without changing entry. Initial spins change duration exposure.

## Implementation

`SlotGame.Core` is independent of UI and contains the physical strips, PCG32 abstraction, ways evaluator, state machine, bonus loop, exact analysis, and maximum analysis. The simulator and Blazor WebAssembly project both reference it. The deployed browser game therefore runs the same C# math assembly that the tests and reports exercise.

Analysis mode exposes the resolved reel set, state before and after, visible Sparks, active multiplier, stop indices, current payout, exact RTP budget, and feature probability. It never displays future outcomes.

## Version 1.1: what a review found and what I changed

A review of version 1.0 found problems that the RTP number alone did not show. I treated them as the most useful part of the project.

1. **Bet switching.** The three states have conditional returns of 91.29%, 153.09% and 372.09%, and v1.0 kept one state for the whole session while allowing bet changes. Building tension at 0.20 and spending it at 10.00 returns 152.23% of money wagered. I moved state into a `StateLedger` owned per bet level and cleared on cash-out, which restores the constant-bet RTP for any betting strategy. An adversarial simulation drops from 153.5% to 96.7%. Details in [advantage-play.md](advantage-play.md).
2. **Maximum-exposure analyzer combined impossible events.** In a reduced-Spark test configuration the feature cannot trigger, but v1.0 still reported an 11,227× maximum because an empty "3+ Sparks" category returned 0 instead of "impossible" and every base state was assumed reachable. The analyzer now propagates impossibility and checks reachability; the correct maximum there is 49.60×.
3. **Tests checked consistency, not correctness.** The RTP test passed for anything from 94% to 97%. Tests now pin 12-digit values from the independent Python reference, compare three exact methods, and include hand-calculated ways fixtures.
4. **Money in `double`.** Credits now settle in `decimal`, so 0.07 × 3 ways × 0.20 is exactly 0.042. The 10M-spin run reproduces v1.0's result to the last digit, which confirms the change did not alter game behaviour.
5. **Documentation drift.** The design spec said 48-stop reels (the build uses 40), and the docs did not mention that BASE_OVERDRIVE and BONUS_REST share strips. Both are now stated and the shared strips are detected in code.

## What I would change next

For a production-oriented client, I would move outcome resolution to a server with an event log and recovery, replace PCG32 with a certified RNG, and add an event-book layer between the math result and presentation.

The Stake Engine adapter exports the exact strips and validates nine deterministic stop fixtures. Completing a current-SDK `GameState` for persistent paid-round state, keyed per bet level, would be the next integration step; I have not represented that partial adapter as production compatible.
