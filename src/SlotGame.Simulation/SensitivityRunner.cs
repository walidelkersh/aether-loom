using SlotGame.Core;

namespace SlotGame.Simulation;

public sealed record SensitivityRow(
    string Parameter,
    string Level,
    double ExactRtp,
    double ExactBaseRtp,
    double ExactFeatureRtp,
    double ExactFeatureFrequency,
    double SimulatedHitFrequency,
    double SimulatedStandardDeviation,
    double ExactMaximumGameCycleAward,
    double MaximumObservedWin,
    long SimulatedPaidSpins,
    ulong Seed);

public static class SensitivityRunner
{
    public static IReadOnlyList<SensitivityRow> Run(GameConfiguration baseline, long spinsPerScenario, ulong seed)
    {
        var scenarios = new (string Parameter, string Level, GameConfiguration Configuration)[]
        {
            ("Baseline", "configured", baseline),
            ("Bonus wilds per reel", "-1", WithBonusWildDelta(baseline, -1)),
            ("Bonus wilds per reel", "+1", WithBonusWildDelta(baseline, 1)),
            ("Base Spark density", "lower", WithBaseSparkDelta(baseline, -1)),
            ("Base Spark density", "higher", WithBaseSparkDelta(baseline, 1)),
            ("Stage multipliers", "1x / 1.5x / 3x", baseline.With(stageMultipliers: [1, 1.5, 3])),
            ("Stage multipliers", "1x / 2x / 4x", baseline.With(stageMultipliers: [1, 2, 4])),
            ("Initial free spins", "6", baseline.With(initialFreeSpins: 6)),
            ("Initial free spins", "8", baseline.With(initialFreeSpins: 8))
        };

        var results = new List<SensitivityRow>(scenarios.Length);
        for (var index = 0; index < scenarios.Length; index++)
        {
            var scenario = scenarios[index];
            var exact = new ExactAnalyzer(scenario.Configuration).Analyze();
            var exactMaximum = new ExtremeAnalyzer(scenario.Configuration).Analyze().MaximumGameCycleAward;
            var scenarioSeed = seed + (ulong)(index * 1_000_003);
            var simulation = new SimulationRunner(scenario.Configuration).Run(spinsPerScenario, scenarioSeed);
            results.Add(new SensitivityRow(
                scenario.Parameter, scenario.Level,
                exact.TotalRtp, exact.BaseRtp, exact.FeatureRtp, exact.FeatureTriggerProbability,
                simulation.HitFrequency, simulation.StandardDeviation, exactMaximum, simulation.MaximumObservedWin,
                spinsPerScenario, scenarioSeed));
        }
        return results;
    }

    private static GameConfiguration WithBonusWildDelta(GameConfiguration config, int delta)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "BONUS_REST", "BONUS_TAUT", "BONUS_OVERDRIVE" };
        return WithSpecialDelta(config, names, SymbolId.Wild, delta, skipFirstReel: true);
    }

    private static GameConfiguration WithBaseSparkDelta(GameConfiguration config, int delta)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "BASE_REST", "BASE_TAUT", "BASE_OVERDRIVE" };
        return WithSpecialDelta(config, names, SymbolId.Spark, delta, skipFirstReel: false);
    }

    private static GameConfiguration WithSpecialDelta(
        GameConfiguration config, HashSet<string> selectedSets, SymbolId special, int delta, bool skipFirstReel)
    {
        var result = new Dictionary<string, ReelSet>(StringComparer.Ordinal);
        foreach (var pair in config.ReelSets)
        {
            if (!selectedSets.Contains(pair.Key))
            {
                result[pair.Key] = pair.Value;
                continue;
            }

            var reels = pair.Value.Reels.Select((reel, index) =>
                skipFirstReel && index == 0 ? reel : ChangeSpecialCount(reel, special, delta)).ToArray();
            result[pair.Key] = new ReelSet(pair.Key, reels);
        }
        return config.With(reelSets: result);
    }

    private static ReelStrip ChangeSpecialCount(ReelStrip reel, SymbolId special, int delta)
    {
        var stops = reel.Stops.ToArray();
        if (delta < 0)
        {
            var index = Array.IndexOf(stops, special);
            if (index >= 0) stops[index] = SymbolId.Knot;
            return new ReelStrip(stops);
        }

        var existing = stops.Select((symbol, index) => (symbol, index))
            .Where(item => item.symbol == special).Select(item => item.index).ToArray();
        var replacement = Enumerable.Range(0, stops.Length).First(index =>
            stops[index] == SymbolId.Knot && existing.All(position =>
            {
                var distance = Math.Abs(position - index);
                return Math.Min(distance, stops.Length - distance) >= 3;
            }));
        stops[replacement] = special;
        return new ReelStrip(stops);
    }
}
