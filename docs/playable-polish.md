# Cabinet interaction pass

The presentation uses the existing original symbol atlas and workshop backdrop. This pass improves the game itself: readable type, sequential reel stops, a tension ladder that distinguishes paid state from free-spin multipliers, and win details showing the actual way counts and awards.

## Settlement and state

`Game.razor.cs` separates the complete-cycle busy flag from rolling animation. A bonus result pause cannot enable another wager or a bet change. The paid state is updated from the canonical engine; the visible ladder returns to that state after the feature. At the highest stage the feature message says that Overdrive holds rather than announcing an impossible stage increase.

The feature counter displays only the currently awarded queue and the spins already played. It does not expose the precomputed final number of feature spins. Analysis clears the current outcome while reels are resolving, then displays its stops once all five have settled.

## Preview and session accounting

Feature Preview is labeled throughout its playback and uses a separate PCG32 generator with seed 42. It starts the canonical bonus at Rest, with the selected bet as a display denomination. It consumes no paid-game RNG, subtracts no wager, credits no balance, and changes no paid state. Previews do not enter session statistics or the five-round history. This lets a reviewer see the mechanic without waiting for a random trigger.

Session return is observed payout divided by paid wager, labeled separately from theoretical RTP. The history records wager, base award, feature award, and before/after paid state for the last five completed rounds. Credits and win details can show four decimal places to retain the model's fractional awards.

## Accessibility and controls

- Space starts one spin when focus is outside interactive controls. Key repeat is ignored.
- Pays & Rules focuses its close button, traps Tab, closes with Escape, and restores focus.
- Reduced-motion preferences remove continuous reel movement and shorten presentation delays.
- Sound is off by default. Optional synthesized tones require user activation and have no role in the mathematical engine.
- Quick play shortens delays; it changes neither RNG consumption nor outcomes.
- Mobile layouts retain full-width controls, readable rules, and no horizontal page overflow.

## Verification

`tests/e2e/game.spec.js` checks the main playable flow, preview isolation and locking, keyboard/modal focus behavior, and mobile overflow. `scripts/capture-playable.cjs` captures cabinet, analysis, feature and mobile screenshots from a running build. `GAME_URL` can point both scripts at a local release or the deployed game.

No reel strip, paytable, state transition, or RTP input changed in this pass. The presentation references the existing canonical C# model; the archived mathematical reports remain the same model's results.
