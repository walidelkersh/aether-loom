namespace SlotGame.Core;

public sealed record ReelSetExtremes(
    string ReelSet,
    double MaximumAnyAward,
    double MaximumBelowTwoScatters,
    double MaximumAtLeastTwoScatters,
    double MaximumBelowThreeScatters,
    double MaximumAtLeastThreeScatters);

public sealed record ExtremeMathReport(
    IReadOnlyDictionary<string, ReelSetExtremes> ReelSets,
    IReadOnlyDictionary<TensionState, double> MaximumFeatureAwards,
    double MaximumBaseAward,
    double MaximumGameCycleAward);

public sealed class ExtremeAnalyzer
{
    private const int ProductRadix = 244;
    private readonly GameConfiguration _configuration;
    private readonly SymbolId[] _symbols;

    public ExtremeAnalyzer(GameConfiguration configuration)
    {
        _configuration = configuration;
        _symbols = configuration.Paytable.PayingSymbols.ToArray();
    }

    public ExtremeMathReport Analyze()
    {
        var sets = _configuration.ReelSets.Values.ToDictionary(set => set.Name, AnalyzeReelSet);
        var featureMemo = new Dictionary<(TensionState Stage, int Remaining, int Played), double>();

        double MaximumFeature(TensionState stage, int remaining, int played)
        {
            if (remaining == 0 || played >= _configuration.MaximumFeatureSpins) return 0;
            var key = (stage, remaining, played);
            if (featureMemo.TryGetValue(key, out var cached)) return cached;
            var extremes = sets[_configuration.BonusReelSet(stage).Name];
            var multiplier = _configuration.StageMultiplier(stage);
            var noEscalation = extremes.MaximumBelowTwoScatters * multiplier
                + MaximumFeature(stage, remaining - 1, played + 1);
            var best = noEscalation;
            if (played + 1 < _configuration.MaximumFeatureSpins)
            {
                var nextStage = (TensionState)Math.Min((int)TensionState.Overdrive, (int)stage + 1);
                best = Math.Max(best, extremes.MaximumAtLeastTwoScatters * multiplier
                    + MaximumFeature(nextStage, remaining, played + 1));
            }
            else
            {
                best = extremes.MaximumAnyAward * multiplier;
            }
            featureMemo[key] = best;
            return best;
        }

        var featureAwards = Enum.GetValues<TensionState>().ToDictionary(
            state => state,
            state => MaximumFeature(state, _configuration.InitialFreeSpins, 0));
        var maximumBase = Enum.GetValues<TensionState>()
            .Max(state => sets[_configuration.BaseReelSet(state).Name].MaximumAnyAward);
        var maximumCycle = Enum.GetValues<TensionState>().Max(state =>
        {
            var baseExtreme = sets[_configuration.BaseReelSet(state).Name];
            return Math.Max(
                baseExtreme.MaximumBelowThreeScatters,
                baseExtreme.MaximumAtLeastThreeScatters + featureAwards[state]);
        });
        return new ExtremeMathReport(sets, featureAwards, maximumBase, maximumCycle);
    }

    public ReelSetExtremes AnalyzeReelSet(ReelSet set)
    {
        var profiles = set.Reels.Select(UniqueProfiles).ToArray();
        var initialProducts = Enumerable.Repeat(1, _symbols.Length).ToArray();
        var states = new Dictionary<ulong, double> { [Encode(initialProducts, 0)] = 0 };

        for (var reelIndex = 0; reelIndex < 5; reelIndex++)
        {
            var next = new Dictionary<ulong, double>();
            foreach (var state in states)
            {
                Decode(state.Key, out var products, out var scatters);
                foreach (var profile in profiles[reelIndex])
                {
                    var newProducts = new int[_symbols.Length];
                    var completed = state.Value;
                    for (var symbol = 0; symbol < _symbols.Length; symbol++)
                    {
                        if (products[symbol] == 0) continue;
                        if (profile.Matches[symbol] > 0)
                            newProducts[symbol] = products[symbol] * profile.Matches[symbol];
                        else if (reelIndex >= 3)
                            completed += products[symbol] * _configuration.Paytable.Get(_symbols[symbol], reelIndex);
                    }
                    var key = Encode(newProducts, scatters + profile.Scatters);
                    if (!next.TryGetValue(key, out var previous) || completed > previous) next[key] = completed;
                }
            }
            states = next;
        }

        var maxAny = 0.0;
        var belowTwo = 0.0;
        var atLeastTwo = 0.0;
        var belowThree = 0.0;
        var atLeastThree = 0.0;
        foreach (var state in states)
        {
            Decode(state.Key, out var products, out var scatters);
            var award = state.Value;
            for (var symbol = 0; symbol < _symbols.Length; symbol++)
                award += products[symbol] * _configuration.Paytable.Get(_symbols[symbol], 5);
            maxAny = Math.Max(maxAny, award);
            if (scatters < 2) belowTwo = Math.Max(belowTwo, award); else atLeastTwo = Math.Max(atLeastTwo, award);
            if (scatters < 3) belowThree = Math.Max(belowThree, award); else atLeastThree = Math.Max(atLeastThree, award);
        }

        return new ReelSetExtremes(set.Name, maxAny, belowTwo, atLeastTwo, belowThree, atLeastThree);
    }

    private Profile[] UniqueProfiles(ReelStrip reel)
    {
        var profiles = new Dictionary<int, Profile>();
        for (var stop = 0; stop < reel.Length; stop++)
        {
            var window = reel.WindowAt(stop);
            var matches = _symbols.Select(symbol => window.Count(item => item == symbol || item == SymbolId.Wild)).ToArray();
            var scatters = window.Count(item => item == SymbolId.Spark);
            var code = scatters;
            foreach (var count in matches) code = code * 4 + count;
            profiles.TryAdd(code, new Profile(matches, scatters));
        }
        return profiles.Values.ToArray();
    }

    private ulong Encode(IReadOnlyList<int> products, int scatters)
    {
        ulong value = (ulong)scatters;
        foreach (var product in products) value = value * ProductRadix + (uint)product;
        return value;
    }

    private void Decode(ulong value, out int[] products, out int scatters)
    {
        products = new int[_symbols.Length];
        for (var index = products.Length - 1; index >= 0; index--)
        {
            products[index] = (int)(value % ProductRadix);
            value /= ProductRadix;
        }
        scatters = (int)value;
    }

    private sealed record Profile(int[] Matches, int Scatters);
}
