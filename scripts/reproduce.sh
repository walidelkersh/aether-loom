#!/usr/bin/env bash
# Regenerate every machine report and every generated document from config/game-config.json.
# Takes about a minute on a modern laptop. Requires .NET 8, Python 3.10+, and a
# Chromium (Edge/Chrome, or `npx playwright install chromium`) for the PDF.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build AetherLoom.sln -c Release
run() { dotnet run --project src/SlotGame.Simulation -c Release --no-build -- "$@"; }
run --par --output reports/par-sheet.json
run --spins 10000000 --seed 20260908 --output reports/verified-metrics.json
run --extremes --output reports/extremes.json
run --sensitivity --spins 500000 --seed 77001 --output reports/sensitivity.json
run --bet-switch --spins 5000000 --seed 31337 --output reports/bet-switch.json
run --fixtures
python scripts/independent_check.py
python scripts/generate_docs.py
python scripts/render_par_pdf.py
