
# Aether Loom mathematical model

## RTP budget

| Component | Exact contribution |
|---|---:|
| Base 243-ways wins | 80.431642% |
| Weave Ascending feature | 16.135273% |
| Scatter cash awards | 0.000000% |
| Jackpots | Not used |
| **Total** | **96.566915%** |

The stationary base-state distribution is 93.2116% Rest, 6.2962% Taut, and 0.4922% Overdrive. The exact long-run feature probability is 0.420624%, or 1 in 237.74 paid spins.

## Base-state transition matrix

Rows are the current state and columns are Rest, Taut, Overdrive.

| Current state | Rest | Taut | Overdrive |
|---|---:|---:|---:|
| Rest | 95.5481% | 4.4519% | 0.0000% |
| Taut | 58.0910% | 34.0913% | 7.8177% |
| Overdrive | 100.0000% | 0.0000% | 0.0000% |

## Feature values by inherited stage

| Starting stage | Exact expected award | Exact expected spins |
|---|---:|---:|
| Rest | 32.431583× | 8.370532 |
| Taut | 61.467090× | 8.370532 |
| Overdrive | 96.017658× | 8.370532 |

The trigger-weighted feature expectation is 38.360288× bet and the trigger-weighted duration is 8.370532 spins. The maximum reachable base award is 88.00×. The maximum complete paid-spin cycle, including its triggered feature, is 11,227.60×.

## Simulation statistics

| Metric | Result |
|---|---:|
| Paid spins | 10,000,000 |
| Seed | 20260908 |
| Observed RTP | 96.638977% |
| 95% RTP CI half-width | ±0.240199% |
| Overall hit frequency | 65.109340% |
| Base hit frequency | 64.838740% |
| Feature frequency | 0.420600% |
| Free spins per paid spin | 3.520610% |
| Average non-zero cycle award | 1.484257× |
| Variance | 15.018659 bet² |
| Standard deviation / volatility index | 3.875392× bet |
| Maximum observed award | 731.79× |
