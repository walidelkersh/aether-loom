# Sensitivity analysis

Each row changes one parameter family. Exact RTP, feature frequency and **reachable** maximum exposure are recomputed
from the altered model; hit frequency and SD use 500,000 seeded paid spins per scenario.

| Parameter | Level | Exact RTP | Exact feature rate | Sim hit rate | Sim σ | Reachable max |
|---|---|---:|---:|---:|---:|---:|
| Baseline | configured | 96.5669% | 0.4206% | 65.0204% | 3.8526× | 11,227.60× |
| Bonus wilds per reel | -1 | 93.2367% | 0.4206% | 65.1110% | 3.1828× | 11,227.60× |
| Bonus wilds per reel | +1 | 100.7503% | 0.4206% | 65.1908% | 4.6621× | 11,563.60× |
| Base Spark density | lower | 95.3858% | 0.0000% | 70.0614% | 1.5150× | 49.60× |
| Base Spark density | higher | 212.2207% | 3.1288% | 61.7292% | 11.0465× | 11,247.30× |
| Stage multipliers | 1x / 1.5x / 3x | 94.5155% | 0.4206% | 65.0882% | 3.3200× | 9,626.60× |
| Stage multipliers | 1x / 2x / 4x | 98.6183% | 0.4206% | 65.1176% | 4.3843× | 12,828.60× |
| Initial free spins | 6 | 93.3031% | 0.4206% | 65.0942% | 3.3410× | 10,058.60× |
| Initial free spins | 8 | 99.9501% | 0.4206% | 65.1670% | 4.5126× | 12,396.60× |

- **Bonus Wild density** moves feature RTP and volatility: −1 Wild per bonus reel gives -3.33 pp, +1 gives +4.18 pp.
- **Base Spark density** drives both the state mix and feature entry. The high case raises feature frequency to 3.13% and RTP to 212.22%: a boundary experiment, not a candidate. In the low case BASE_REST has no Sparks, Rest is absorbing, and the feature can never trigger, so the reachable maximum collapses to 49.60×. *v1.0 reported 11,227.15× here because it added the feature maximum to an impossible 3-Spark branch; the analyzer now propagates impossibility and checks state reachability.*
- **Stage multipliers** change the feature budget without changing entry frequency: 94.52% and 98.62%.
- **Initial free spins** move RTP and variance together: six spins return 93.30%, eight 99.95%.

Rebalancing example: to double feature frequency at constant RTP, feature RTP = entry × average award
(0.4206% × 38.36 = 16.14%) means the average award must fall to about
19.2×. Because Sparks also drive the base state, adding Sparks shifts π toward the richer base sets as well,
so the whole model is re-solved rather than one term scaled.

![Sensitivity chart](sensitivity.svg)
