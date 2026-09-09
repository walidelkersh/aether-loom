# Experimental Stake Engine representation

Status checked: 2026-09-08.

Stake Engine's public [Math SDK](https://github.com/engineio/math-sdk) is an active MIT-licensed Python project requiring Python 3.12 or newer. Its current examples support ways evaluation, wilds, scatters, free games, CSV reel inputs, event books, simulations, and optimized lookup-table generation. The project is therefore publicly usable for an experiment.

This directory maps Aether Loom's canonical `config/game-config.json` into the SDK's current paytable and CSV reel conventions. Run:

```bash
dotnet run --project src/SlotGame.Simulation -- --fixtures
python integrations/stake-engine/adapter.py
python integrations/stake-engine/parity_check.py
```

The parity check transposes all six physical reel sets into the SDK's row-oriented CSV format, reconstructs nine deterministic C# stop outcomes, and independently compares ways payout, scatter count, and base-state transition.

The adapter does not claim production Stake Engine compatibility. Aether Loom's persistent paid-spin state and inherited bonus stage require a custom SDK `GameState`, event schema, book generation, and platform-side review. Those pieces are intentionally left as future integration work because the public SDK's standard ways example does not implement a persistent state shared across paid rounds. The C# model, reports, and playable remain the verified implementation.
