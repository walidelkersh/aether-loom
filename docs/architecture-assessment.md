# Existing-work architecture assessment

Assessment date: 2026-09-08

## Existing slot simulation engine

Repository inspected: <https://github.com/walidelkersh/slot-simulation-engine>

The existing project is a Python/JavaScript simulator with a FastAPI host and a static browser UI. Its useful seams are the separation between symbol/paytable data, reel generation, win evaluators, feature runners, simulation, and reports. It already contains fixed-line and 243-ways evaluation, wild and scatter handling, free spins, cascades, Hold & Spin, fixed and pool-funded jackpots, Welford online variance, confidence intervals, multiprocessing, Web Workers, a deterministic Xoshiro128++ implementation in JavaScript, and a Robbins-Monro pay-scale tuner.

The existing `Reel` abstraction is a weighted sampler. Each visible cell is selected independently, so it does not model a physical circular strip or the dependence among the three symbols shown on one reel. Its analytical report enumerates independent weighted outcomes rather than stop-index combinations on physical strips. The Python feature code also mixes `random` and `secrets`, which prevents full seeded replay. The browser and Python implementations duplicate game rules and have visible configuration drift. These constraints make the code unsuitable as the canonical foundation for this project.

Reusable ideas:

- symbol/paytable/evaluator boundaries;
- deterministic, seedable simulation;
- Welford online variance;
- separate RTP component accounting;
- worker-friendly simulation batches;
- exact-versus-simulation reporting.

Deliberately replaced:

- independent cell sampling with circular, inspectable reel strips and stop indexing;
- duplicated Python/JavaScript math with one canonical C# core shared by the playable build;
- generic free-spin and Hold & Spin features with one game-specific state model;
- global pay scaling with parameter-level sensitivity experiments;
- claims of exactness that do not match the implemented sampling model.

## Existing portfolio

Repository inspected: `walidelkersh/personal-portfolio` (private); live site: <https://walidelkersh.vercel.app/>.

The portfolio is a Vite/React/TypeScript single-page app. Projects are defined in `src/config/hubConfig.ts`, displayed by `PortfolioSection.tsx`, and routed by URL hashes in `App.tsx`. The project cards already support source, live-demo, media, tags, and a small case-study field. A dedicated Aether Loom case-study component and route can be added without replacing the site architecture. The repository's `PRODUCT.md` and `DESIGN.md` govern the visual system and must be followed during portfolio integration.

## Build decision

The new repository will use:

- `SlotGame.Core`: canonical game configuration, physical reel strips, RNG, outcome evaluation, persistent state, bonus state, and exact analysis;
- `SlotGame.Simulation`: seeded Monte Carlo, reports, sensitivity runs, and machine-readable verified metrics;
- `SlotGame.Tests`: deterministic unit, invariant, and mathematical reconciliation tests;
- `SlotGame.Playable`: Blazor WebAssembly UI referencing `SlotGame.Core` directly.

Blazor WebAssembly is chosen over Unity WebGL because it allows the deployed game to execute the same C# assembly as the simulation and tests. This is a portfolio engineering decision, not a claim that Blazor is a typical production slot client.
