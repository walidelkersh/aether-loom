namespace SlotGame.Core;

public sealed record ReelSetMath(
    string ReelSet,
    double ExpectedWaysAward,
    double BaseHitProbability,
    IReadOnlyDictionary<SymbolId, double> SymbolContributions,
    IReadOnlyList<double> ScatterProbabilities)
{
    public double FeatureTriggerProbability => ScatterProbabilities.Skip(3).Sum();
    public double EscalationProbability => ScatterProbabilities.Skip(2).Sum();
}

public sealed record FeatureMath(TensionState StartingStage, double ExpectedAward, double ExpectedSpins);

public sealed record TheoreticalMathReport(
    IReadOnlyDictionary<string, ReelSetMath> ReelSets,
    IReadOnlyList<double> StationaryStateProbabilities,
    double[,] BaseTransitionMatrix,
    IReadOnlyList<FeatureMath> Features,
    double BaseRtp,
    double FeatureRtp,
    double TotalRtp,
    double FeatureTriggerProbability,
    double BaseHitProbability);

public sealed class ExactAnalyzer
{
    private readonly GameConfiguration _configuration;

    public ExactAnalyzer(GameConfiguration configuration) => _configuration = configuration;

    public TheoreticalMathReport Analyze()
    {
        var reelMath = _configuration.ReelSets.Values.ToDictionary(set => set.Name, AnalyzeReelSet);
        var transition = BuildTransitionMatrix(reelMath);
        var stationary = StationaryDistribution(transition);
        var features = Enum.GetValues<TensionState>()
            .Select(stage => AnalyzeFeature(stage, reelMath)).ToArray();

        var baseRtp = 0.0;
        var featureRtp = 0.0;
        var triggerProbability = 0.0;
        var baseHitProbability = 0.0;
        foreach (var state in Enum.GetValues<TensionState>())
        {
            var stateWeight = stationary[(int)state];
            var math = reelMath[_configuration.BaseReelSet(state).Name];
            baseRtp += stateWeight * math.ExpectedWaysAward;
            baseHitProbability += stateWeight * math.BaseHitProbability;
            triggerProbability += stateWeight * math.FeatureTriggerProbability;
            featureRtp += stateWeight * math.FeatureTriggerProbability * features[(int)state].ExpectedAward;
        }

        return new TheoreticalMathReport(
            reelMath, stationary, transition, features, baseRtp, featureRtp,
            baseRtp + featureRtp, triggerProbability, baseHitProbability);
    }

    public ReelSetMath AnalyzeReelSet(ReelSet set)
    {
        var symbolContributions = _configuration.Paytable.PayingSymbols
            .ToDictionary(symbol => symbol, symbol => ExpectedSymbolAward(set, symbol));
        return new ReelSetMath(
            set.Name,
            symbolContributions.Values.Sum(),
            ExactBaseHitProbability(set),
            symbolContributions,
            ScatterDistribution(set));
    }

    private bool HasThreeReelWin(ReelSet set, int firstStop, int secondStop, int thirdStop)
    {
        var windows = new[]
        {
            set.Reels[0].WindowAt(firstStop),
            set.Reels[1].WindowAt(secondStop),
            set.Reels[2].WindowAt(thirdStop)
        };
        return _configuration.Paytable.PayingSymbols.Any(symbol =>
            windows.All(window => window.Any(item => item == symbol || item == SymbolId.Wild)));
    }

    private double ExactBaseHitProbability(ReelSet set)
    {
        long hits = 0;
        for (var first = 0; first < set.Reels[0].Length; first++)
        for (var second = 0; second < set.Reels[1].Length; second++)
        for (var third = 0; third < set.Reels[2].Length; third++)
            if (HasThreeReelWin(set, first, second, third)) hits++;
        return (double)hits / (set.Reels[0].Length * set.Reels[1].Length * set.Reels[2].Length);
    }

    private double ExpectedSymbolAward(ReelSet set, SymbolId symbol)
    {
        var distributions = set.Reels.Select(reel => CountDistribution(reel, symbol, includeWild: true)).ToArray();
        var expected = 0.0;
        var counts = new int[5];

        void Visit(int reel, double probability)
        {
            if (reel == 5)
            {
                var matched = 0;
                var ways = 1;
                for (var i = 0; i < 5 && counts[i] > 0; i++)
                {
                    matched++;
                    ways *= counts[i];
                }
                expected += probability * ways * _configuration.Paytable.Get(symbol, matched);
                return;
            }

            for (var count = 0; count <= 3; count++)
            {
                var p = distributions[reel][count];
                if (p == 0) continue;
                counts[reel] = count;
                Visit(reel + 1, probability * p);
            }
        }

        Visit(0, 1.0);
        return expected;
    }

    private static double[] CountDistribution(ReelStrip reel, SymbolId symbol, bool includeWild)
    {
        var distribution = new double[4];
        for (var stop = 0; stop < reel.Length; stop++)
        {
            var count = reel.WindowAt(stop).Count(item => item == symbol || includeWild && item == SymbolId.Wild);
            distribution[count] += 1.0 / reel.Length;
        }
        return distribution;
    }

    private static double[] ScatterDistribution(ReelSet set)
    {
        var total = new double[16];
        total[0] = 1;
        var maximum = 0;
        foreach (var reel in set.Reels)
        {
            var perReel = CountDistribution(reel, SymbolId.Spark, includeWild: false);
            var next = new double[16];
            for (var current = 0; current <= maximum; current++)
            for (var count = 0; count <= 3; count++)
                next[current + count] += total[current] * perReel[count];
            total = next;
            maximum += 3;
        }
        return total;
    }

    private double[,] BuildTransitionMatrix(IReadOnlyDictionary<string, ReelSetMath> reelMath)
    {
        var matrix = new double[3, 3];
        foreach (var state in Enum.GetValues<TensionState>())
        {
            var scatter = reelMath[_configuration.BaseReelSet(state).Name].ScatterProbabilities;
            for (var count = 0; count < scatter.Count; count++)
            {
                var next = GameEngine.NextBaseState(state, count);
                matrix[(int)state, (int)next] += scatter[count];
            }
        }
        return matrix;
    }

    private static double[] StationaryDistribution(double[,] matrix)
    {
        var current = new[] { 1.0, 0.0, 0.0 };
        for (var iteration = 0; iteration < 100_000; iteration++)
        {
            var next = new double[3];
            for (var from = 0; from < 3; from++)
            for (var to = 0; to < 3; to++)
                next[to] += current[from] * matrix[from, to];

            var total = next.Sum();
            for (var state = 0; state < next.Length; state++) next[state] /= total;
            if (next.Zip(current).Max(pair => Math.Abs(pair.First - pair.Second)) < 1e-13)
                return next;
            current = next;
        }
        throw new InvalidOperationException("Base-state stationary distribution did not converge.");
    }

    private FeatureMath AnalyzeFeature(TensionState startingStage, IReadOnlyDictionary<string, ReelSetMath> reelMath)
    {
        var awardMemo = new Dictionary<(TensionState Stage, int Remaining, int Played), double>();
        var spinMemo = new Dictionary<(TensionState Stage, int Remaining, int Played), double>();

        double ExpectedAward(TensionState stage, int remaining, int played)
        {
            if (remaining == 0 || played >= _configuration.MaximumFeatureSpins) return 0;
            var key = (stage, remaining, played);
            if (awardMemo.TryGetValue(key, out var cached)) return cached;

            var math = reelMath[_configuration.BonusReelSet(stage).Name];
            var currentAward = math.ExpectedWaysAward * _configuration.StageMultiplier(stage);
            var canAward = played + 1 < _configuration.MaximumFeatureSpins;
            var p = canAward ? math.EscalationProbability : 0;
            var nextStage = (TensionState)Math.Min((int)TensionState.Overdrive, (int)stage + 1);
            var value = currentAward
                + p * ExpectedAward(nextStage, remaining, played + 1)
                + (1 - p) * ExpectedAward(stage, remaining - 1, played + 1);
            awardMemo[key] = value;
            return value;
        }

        double ExpectedSpins(TensionState stage, int remaining, int played)
        {
            if (remaining == 0 || played >= _configuration.MaximumFeatureSpins) return 0;
            var key = (stage, remaining, played);
            if (spinMemo.TryGetValue(key, out var cached)) return cached;

            var math = reelMath[_configuration.BonusReelSet(stage).Name];
            var canAward = played + 1 < _configuration.MaximumFeatureSpins;
            var p = canAward ? math.EscalationProbability : 0;
            var nextStage = (TensionState)Math.Min((int)TensionState.Overdrive, (int)stage + 1);
            var value = 1
                + p * ExpectedSpins(nextStage, remaining, played + 1)
                + (1 - p) * ExpectedSpins(stage, remaining - 1, played + 1);
            spinMemo[key] = value;
            return value;
        }

        return new FeatureMath(
            startingStage,
            ExpectedAward(startingStage, _configuration.InitialFreeSpins, 0),
            ExpectedSpins(startingStage, _configuration.InitialFreeSpins, 0));
    }
}
