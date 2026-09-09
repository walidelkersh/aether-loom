
# Exact mathematics versus Monte Carlo

Model version: 1.0.0<br>
Configuration SHA-256 prefix: `6045448975a0`<br>
Simulation: 10,000,000 paid spins, PCG32 seed `20260908`

| Quantity | Exact | Simulation | Simulation − exact |
|---|---:|---:|---:|
| Base ways RTP | 80.431642% | 80.499687% | +0.068046 pp |
| Feature RTP | 16.135273% | 16.139290% | +0.004016 pp |
| Total RTP | 96.566915% | 96.638977% | +0.072062 pp |
| Base hit probability | 64.820986% | 64.838740% | +0.017754 pp |
| Feature-entry probability | 0.420624% | 0.420600% | -0.000024 pp |

The simulated total RTP is 96.638977%. Its normal 95% confidence interval is 96.398778% to 96.879176%; the exact 96.566915% result lies inside that interval.

| Feature quantity | Exact | Simulation | Difference |
|---|---:|---:|---:|
| Conditional feature award | 38.360288× | 38.372063× | +0.011775× |
| Feature spins played | 8.370532 | 8.370447 | -0.000085 |

## What is exact

The analyzer enumerates every stop on each 40-stop reel when building visible-window distributions. Per-symbol 243-ways EV is calculated from the five independent reel-window count distributions. Scatter-count polynomials produce each reel set's trigger probabilities. Those probabilities produce the three-state base transition matrix and its stationary distribution. A finite Markov reward recursion calculates bonus EV and duration for every starting stage. A separate reachable-profile dynamic program calculates maximum awards.

## What is estimated

Hit frequency for complete paid cycles, variance, standard deviation, volatility index, observed maximum, and all Monte Carlo columns are estimates from the seeded run. The confidence interval uses `mean ± 1.96 × sample-standard-deviation / sqrt(N)` on return in bet units. It quantifies sampling error under the usual independent-cycle normal approximation; it is not a regulatory certification.
