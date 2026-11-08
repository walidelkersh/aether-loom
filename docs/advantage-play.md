# Advantage play: the bet-switching defect and its fix

## The problem (v1.0)

The headline RTP assumes a **constant bet**. The three tension states have very different conditional returns:

| State | Share of paid spins | Return of a spin in this state |
|---|---:|---:|
| Rest | 93.21% | 90.69% |
| Taut | 6.30% | 153.06% |
| Overdrive | 0.49% | 372.09% |

Version 1.0 kept a single tension state for the whole session and allowed the bet to change between spins.
A player could build tension at the minimum bet and spend it at the maximum. With bet $b_s$ in state $s$ the
long-run return per credit wagered is

$$R=\frac{\sum_s \pi_s\, b_s\, r_s}{\sum_s \pi_s\, b_s}$$

| Strategy | Exact return per credit wagered |
|---|---:|
| Constant bet (any level) | 96.0000% |
| 0.20 in Rest, 10.00 in Taut and Overdrive | **152.0823%** |
| 0.20 in Rest and Taut, 10.00 only in Overdrive | 149.6495% |

A second, related exposure: a machine left in Taut is worth **+1.19 bets** of future return
to the next player compared with Rest, and Overdrive **+2.76 bets** (relative state values from the
fundamental matrix). In a cabinet that keeps state across players this invites "machine hunting".

## The fix (v1.1)

`StateLedger` with `StatePolicy.PerBetLevel`, the usual remedy for persistent-state games:

1. **State is owned per bet level.** Tension earned at 0.20 can only be spent at 0.20. Changing bet shows that bet's own state.
2. **State is session-scoped.** Reset / cash-out clears the ledger, so no state is left for the next player.

Why this restores the constant-bet RTP for *any* betting strategy: each bet level's state now evolves only through spins
at that level, so each level is its own copy of the Markov chain. Whatever order the player interleaves levels in,
every level's spins form an ordinary run of the chain, whose long-run return per credit is the RTP (renewal-reward
argument on Rest-to-Rest cycles). The only remaining choice, abandoning a level while it is in Taut or Overdrive, forfeits
positive state value and can only lower the player's return.

## Evidence

Adversarial simulation (`--bet-switch`, 5,000,000 spins, seed 31337): bet the minimum while the
maximum-bet state is Rest, otherwise bet the maximum.

| State policy | Simulated return per credit wagered |
|---|---:|
| Shared across bets (v1.0) | 153.30% (theory 152.08%) |
| Per bet level (v1.1) | 96.18% (constant-bet RTP 96.00%) |

`BetSwitchingIsProfitableOnlyWithSharedState` keeps this as a regression test. The other designs considered were
locking the bet while tension is above Rest (simpler, but players dislike a locked bet button) and narrowing the gap
between conditional state returns (reduces the edge but does not remove it, and it flattens the mechanic).
