![Aether Loom](docs/images/gameplay.png)

# Aether Loom

A 5x3, 243-ways slot built from physical reel strips, with exact math, a C# engine, a simulator and a playable
browser version. Two Spark symbols move a persistent tension state (Rest, Taut, Overdrive) that picks the next reel
set and becomes the starting stage of a 7 to 12 spin feature.

[Play it](https://walidelkersh.github.io/aether-loom/) · [PAR sheet](docs/PAR-sheet.pdf) · [Math and verification](docs/math.md) · [Rules](docs/game-design.md)

## Numbers

| | |
|---|---:|
| RTP | 96.0000% (base 79.8647%, feature 16.1353%) |
| Hit frequency | 64.09% |
| Feature frequency | 1 in 237.74 |
| Standard deviation | 3.864x bet |
| Volatility index (90%) | 6.36 |
| Max paid cycle | 11,227.60x |
| Simulated RTP, 10M spins | 96.0577% |

Everything except the last row is computed exactly from the strips, not sampled. The RTP was tuned to a 96.00%
target by downgrading eight regular-symbol stops in the two lower base states; the exact value is 95.99999339%.

## How the math works

The base game is a three-state Markov chain. Each state's reel set gives a Spark-count distribution, which gives
one row of the transition matrix; the stationary distribution weights each state's ways EV and feature trigger
rate. The feature is a finite recursion over (stage, spins left, spins played).

RTP and the transition matrix come from per-reel window distributions (`ExactAnalyzer`). Variance, hit rate and
the win distribution come from enumerating all 40^5 stop combinations of each reel set (`ParAnalyzer`, about a
second). Because the state links consecutive spins, the confidence interval on simulated RTP uses the long-run
variance Var(X) + 2 sum Cov(X_0, X_k) rather than the textbook i.i.d. formula.

`scripts/independent_check.py` recomputes the RTP in Python, from the written rules, with exact fractions and no
shared code. CI fails if it disagrees with the C# result.

## What was hard

- **Bet switching.** The states return 90.7%, 153.1% and 372.1% per spin. With one state shared across bets, a
  player who bets 0.20 in Rest and 10 otherwise gets 152% back. State is now kept per bet level and cleared on
  cash-out. [advantage-play.md](docs/advantage-play.md) has the numbers and a simulation of both versions.
- **Max win.** The first max-win analyzer treated "no outcome with 3+ Sparks" as a zero award instead of
  impossible, so a test configuration where the feature could never trigger still reported 11,227x. It now
  checks reachability; the real figure there is 49.60x.
- **Correlated spins.** The correlation turned out to lower long-run variance slightly (-0.54%), because
  zero-Spark spins pay the most in the base game and also push tension down.

## What I would change

- Resolve outcomes on a server with an event log, and use a certified RNG instead of PCG32.
- Bring the three states' returns closer together. Per-bet state removes the exploit, but a gap this large breaks
  again the moment state handling changes.

## Run

.NET 8 SDK. Python 3.10+ for the reference check, Node 22 for the browser tests.

```bash
dotnet test AetherLoom.sln -c Release
dotnet run --project src/SlotGame.Simulation -c Release -- --par
dotnet run --project src/SlotGame.Simulation -c Release -- --spins 1000000
dotnet run --project src/SlotGame.Simulation -c Release -- --bet-switch
python scripts/independent_check.py
dotnet run --project src/SlotGame.Playable
```

`bash scripts/reproduce.sh` regenerates every report and document.

## Layout

- `config/game-config.json`: strips, paytable, bets
- `src/SlotGame.Core`: engine, ways evaluator, state ledger, analyzers
- `src/SlotGame.Simulation`: command-line simulator and report writer
- `src/SlotGame.Playable`: Blazor WebAssembly client using the same core
- `tests/`: xUnit tests and Playwright browser tests

Fictional credits only. MIT license.
