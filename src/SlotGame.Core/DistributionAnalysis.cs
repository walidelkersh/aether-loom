namespace SlotGame.Core;

// Every 40^5 stop combination of one reel set. Awards are integer hundredths of a bet summed in Int128, so the
// moments are exact. Split by Spark count (0/1/2/3+) because that count drives transitions, triggers and escalation.
public sealed class ReelSetDistribution
{
    public const int Classes = 4;
    public static readonly double[] BucketEdges = [0, 0.000001, 1, 2, 5, 10, 20, 50, 100];
    public static readonly string[] BucketLabels = ["0", "(0, 1)", "[1, 2)", "[2, 5)", "[5, 10)", "[10, 20)", "[20, 50)", "[50, 100)", "100+"];

    public required string ReelSet { get; init; }
    public required long Scale { get; init; }
    public required long Combinations { get; init; }
    public required long[] ClassCount { get; init; }
    public required long[] ClassZeroCount { get; init; }
    public required Int128[] ClassAwardSum { get; init; }
    public required Int128[] ClassAwardSquareSum { get; init; }
    public required long[] Buckets { get; init; }
    public required long MaximumAwardUnits { get; init; }

    public double ClassProbability(int c) => (double)ClassCount[c] / Combinations;
    public double ClassZeroProbability(int c) => (double)ClassZeroCount[c] / Combinations;
// E[award * 1{class = c}], in bets
    public double ClassAwardMoment(int c) => (double)ClassAwardSum[c] / Scale / Combinations;
    public double ExpectedAward => Enumerable.Range(0, Classes).Sum(ClassAwardMoment);
    public double ExpectedAwardSquared =>
        (double)Enumerable.Range(0, Classes).Aggregate(Int128.Zero, (sum, c) => sum + ClassAwardSquareSum[c]) / Scale / Scale / Combinations;
    public double HitProbability => 1 - (double)ClassZeroCount.Sum() / Combinations;

    public static ReelSetDistribution Enumerate(ReelSet set, Paytable paytable)
    {
        var symbols = paytable.PayingSymbols.ToArray();
        var n = symbols.Length;
        var scale = paytable.IntegerScale;
        var pay = new long[n, 6];
        for (var s = 0; s < n; s++)
        for (var length = 3; length <= 5; length++)
            pay[s, length] = (long)(paytable.GetExact(symbols[s], length) * scale);

        // Collapse each reel's 40 stops to distinct (matches per symbol, sparks) profiles with multiplicities.
        var profiles = set.Reels.Select(reel =>
        {
            var map = new Dictionary<string, (int[] Matches, int Sparks, long Weight)>();
            for (var stop = 0; stop < reel.Length; stop++)
            {
                var window = reel.WindowAt(stop);
                var matches = symbols.Select(sym => window.Count(x => x == sym || x == SymbolId.Wild)).ToArray();
                var sparks = window.Count(x => x == SymbolId.Spark);
                var key = string.Join(",", matches) + ":" + sparks;
                map[key] = map.TryGetValue(key, out var existing) ? existing with { Weight = existing.Weight + 1 } : (matches, sparks, 1);
            }
            return map.Values.ToArray();
        }).ToArray();

        var classCount = new long[Classes];
        var classZero = new long[Classes];
        var classSum = new Int128[Classes];
        var classSquare = new Int128[Classes];
        var buckets = new long[BucketEdges.Length];
        long maximum = 0;
        var bucketUnits = BucketEdges.Select(edge => edge == 0 ? 0 : edge < 1e-3 ? 1 : (long)Math.Round(edge * scale)).ToArray();

        var prod1 = new long[n];
        var prod2 = new long[n];
        var prod3 = new long[n];
        foreach (var r0 in profiles[0])
        foreach (var r1 in profiles[1])
        {
            for (var s = 0; s < n; s++) prod1[s] = r0.Matches[s] * r1.Matches[s];
            var w01 = r0.Weight * r1.Weight;
            foreach (var r2 in profiles[2])
            {
                for (var s = 0; s < n; s++) prod2[s] = prod1[s] * r2.Matches[s];
                var w012 = w01 * r2.Weight;
                foreach (var r3 in profiles[3])
                {
                    long partial3 = 0;
                    for (var s = 0; s < n; s++)
                    {
                        if (prod2[s] != 0 && r3.Matches[s] == 0) partial3 += prod2[s] * pay[s, 3];
                        prod3[s] = prod2[s] * r3.Matches[s];
                    }
                    var w0123 = w012 * r3.Weight;
                    var sparks0123 = r0.Sparks + r1.Sparks + r2.Sparks + r3.Sparks;
                    foreach (var r4 in profiles[4])
                    {
                        var award = partial3;
                        for (var s = 0; s < n; s++)
                        {
                            var p = prod3[s];
                            if (p == 0) continue;
                            var m = r4.Matches[s];
                            award += m == 0 ? p * pay[s, 4] : p * m * pay[s, 5];
                        }
                        var weight = w0123 * r4.Weight;
                        var c = Math.Min(3, sparks0123 + r4.Sparks);
                        classCount[c] += weight;
                        if (award == 0) classZero[c] += weight;
                        classSum[c] += (Int128)award * weight;
                        classSquare[c] += (Int128)award * award * weight;
                        if (award > maximum) maximum = award;
                        var bucket = 0;
                        for (var b = bucketUnits.Length - 1; b > 0; b--)
                            if (award >= bucketUnits[b]) { bucket = b; break; }
                        buckets[bucket] += weight;
                    }
                }
            }
        }

        return new ReelSetDistribution
        {
            ReelSet = set.Name,
            Scale = scale,
            Combinations = set.Reels.Aggregate(1L, (total, reel) => total * reel.Length),
            ClassCount = classCount,
            ClassZeroCount = classZero,
            ClassAwardSum = classSum,
            ClassAwardSquareSum = classSquare,
            Buckets = buckets,
            MaximumAwardUnits = maximum
        };
    }
}

public sealed record FeatureMoments(TensionState StartingStage, double Mean, double SecondMoment, double ZeroProbability)
{
    public double Variance => SecondMoment - Mean * Mean;
}

public sealed record StateCycleStatistics(
    TensionState State,
    double ExpectedReturn,
    double SecondMoment,
    double HitProbability,
    double BaseHitProbability,
    double FeatureProbability,
    double RelativeValue);

public sealed record ConfidenceBand(long Spins, double LowerRtp90, double UpperRtp90, double LowerRtp95, double UpperRtp95);

public sealed record ParStatistics(
    double Rtp,
    double Variance,
    double StandardDeviation,
    double VolatilityIndex90,
    double VolatilityIndex95,
    double AsymptoticVariance,
    double HitFrequency,
    double BaseHitFrequency,
    IReadOnlyList<double> StationaryStateProbabilities,
    IReadOnlyList<StateCycleStatistics> States,
    IReadOnlyList<FeatureMoments> Features,
    IReadOnlyList<double> BaseWinDistribution,
    IReadOnlyList<ConfidenceBand> ConfidenceBands,
    IReadOnlyList<(long Spins, double ExpectedRtp)> SessionRtpFromRest)
{
    public double CorrelationFactor => AsymptoticVariance / Variance;
}

// Feature second moments condition on the Spark class: the spin that extends the feature is correlated with its
// own award. sigma^2_inf = Var(X) + 2 sum_k Cov(X_0, X_k) comes from the fundamental matrix.
public sealed class ParAnalyzer
{
    private readonly GameConfiguration _configuration;
    private readonly Dictionary<string, ReelSetDistribution> _distributions;

    public ParAnalyzer(GameConfiguration configuration, IReadOnlyDictionary<string, ReelSetDistribution>? distributions = null)
    {
        _configuration = configuration;
        _distributions = distributions?.ToDictionary(x => x.Key, x => x.Value) ?? EnumerateAll(configuration);
    }

    public IReadOnlyDictionary<string, ReelSetDistribution> Distributions => _distributions;

    public static Dictionary<string, ReelSetDistribution> EnumerateAll(GameConfiguration configuration)
    {
        var groups = configuration.GroupByStrips().ToArray();
        var computed = new ReelSetDistribution[groups.Length];
        Parallel.For(0, groups.Length, i => computed[i] = ReelSetDistribution.Enumerate(groups[i].First(), configuration.Paytable));
        var result = new Dictionary<string, ReelSetDistribution>(StringComparer.Ordinal);
        for (var i = 0; i < groups.Length; i++)
            foreach (var set in groups[i]) result[set.Name] = computed[i];
        return result;
    }

    public ParStatistics Analyze()
    {
        var states = Enum.GetValues<TensionState>();
        var features = states.Select(FeatureMomentsFrom).ToArray();

        var transition = new double[3, 3];
        var joint = new double[3, 3]; // E[X 1{next = s'} | s]
        var mean = new double[3];
        var second = new double[3];
        var hit = new double[3];
        var baseHit = new double[3];
        var trigger = new double[3];
        foreach (var state in states)
        {
            var d = _distributions[_configuration.BaseReelSet(state).Name];
            var f = features[(int)state];
            var s = (int)state;
            for (var c = 0; c < ReelSetDistribution.Classes; c++)
            {
                var next = (int)GameEngine.NextBaseState(state, c);
                var p = d.ClassProbability(c);
                var ex = d.ClassAwardMoment(c) + (c == 3 ? p * f.Mean : 0);
                transition[s, next] += p;
                joint[s, next] += ex;
                mean[s] += ex;
            }
            var p3 = d.ClassProbability(3);
            var b3 = d.ClassAwardMoment(3);
            second[s] = d.ExpectedAwardSquared + 2 * b3 * f.Mean + p3 * f.SecondMoment;
            hit[s] = 1 - (d.ClassZeroProbability(0) + d.ClassZeroProbability(1) + d.ClassZeroProbability(2)
                          + d.ClassZeroProbability(3) * f.ZeroProbability);
            baseHit[s] = d.HitProbability;
            trigger[s] = p3;
        }

        var pi = MarkovMath.StationaryDistribution(transition);
        var mu = Enumerable.Range(0, 3).Sum(s => pi[s] * mean[s]);
        var variance = Enumerable.Range(0, 3).Sum(s => pi[s] * second[s]) - mu * mu;
        var centered = mean.Select(m => m - mu).ToArray();
        var fundamental = MarkovMath.FundamentalMatrix(transition, pi);
        var value = MarkovMath.Multiply(fundamental, centered);

        var autocovarianceSum = 0.0;
        for (var s = 0; s < 3; s++)
        for (var next = 0; next < 3; next++)
            autocovarianceSum += pi[s] * (joint[s, next] - mu * transition[s, next]) * value[next];
        var asymptotic = variance + 2 * autocovarianceSum;

        var sd = Math.Sqrt(variance);
        var sdInf = Math.Sqrt(asymptotic);
        var bands = new long[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 }
            .Select(n => new ConfidenceBand(n,
                mu - 1.644854 * sdInf / Math.Sqrt(n), mu + 1.644854 * sdInf / Math.Sqrt(n),
                mu - 1.959964 * sdInf / Math.Sqrt(n), mu + 1.959964 * sdInf / Math.Sqrt(n)))
            .ToArray();

        var buckets = new double[ReelSetDistribution.BucketEdges.Length];
        foreach (var state in states)
        {
            var d = _distributions[_configuration.BaseReelSet(state).Name];
            for (var b = 0; b < buckets.Length; b++) buckets[b] += pi[(int)state] * d.Buckets[b] / d.Combinations;
        }

        var session = new List<(long, double)>();
        var distribution = new double[] { 1, 0, 0 };
        double cumulative = 0;
        var checkpoints = new HashSet<long> { 10, 100, 1_000, 10_000 };
        for (long t = 1; t <= 10_000; t++)
        {
            cumulative += Enumerable.Range(0, 3).Sum(s => distribution[s] * mean[s]);
            var next = new double[3];
            for (var from = 0; from < 3; from++)
            for (var to = 0; to < 3; to++) next[to] += distribution[from] * transition[from, to];
            distribution = next;
            if (checkpoints.Contains(t)) session.Add((t, cumulative / t));
        }

        var stateStats = states.Select(state => new StateCycleStatistics(
            state, mean[(int)state], second[(int)state], hit[(int)state], baseHit[(int)state], trigger[(int)state],
            value[(int)state] - value[0])).ToArray();

        return new ParStatistics(
            mu, variance, sd, 1.644854 * sd, 1.959964 * sd, asymptotic,
            Enumerable.Range(0, 3).Sum(s => pi[s] * hit[s]),
            Enumerable.Range(0, 3).Sum(s => pi[s] * baseHit[s]),
            pi, stateStats, features, buckets, bands, session);
    }

    private FeatureMoments FeatureMomentsFrom(TensionState start)
    {
        var memo = new Dictionary<(TensionState, int, int), (double M1, double M2, double Z)>();
        (double M1, double M2, double Z) Visit(TensionState stage, int remaining, int played)
        {
            if (remaining == 0 || played >= _configuration.MaximumFeatureSpins) return (0, 0, 1);
            if (memo.TryGetValue((stage, remaining, played), out var cached)) return cached;
            var d = _distributions[_configuration.BonusReelSet(stage).Name];
            var k = _configuration.StageMultiplier(stage);
            // Escalation (2+ Sparks) is possible except on the capped final spin.
            var canEscalate = played + 1 < _configuration.MaximumFeatureSpins;
            double pE = 0, xE = 0, zE = 0;
            if (canEscalate)
            {
                pE = d.ClassProbability(2) + d.ClassProbability(3);
                xE = k * (d.ClassAwardMoment(2) + d.ClassAwardMoment(3));
                zE = d.ClassZeroProbability(2) + d.ClassZeroProbability(3);
            }
            var xAll = k * d.ExpectedAward;
            var x2 = k * k * d.ExpectedAwardSquared;
            var zAll = 1 - d.HitProbability;
            var pN = 1 - pE;
            var xN = xAll - xE;
            var zN = zAll - zE;

            var nextStage = (TensionState)Math.Min(2, (int)stage + 1);
            var e = canEscalate ? Visit(nextStage, remaining, played + 1) : (0, 0, 1);
            var n = Visit(stage, remaining - 1, played + 1);
            var m1 = xAll + pE * e.M1 + pN * n.M1;
            var m2 = x2 + 2 * (xE * e.M1 + xN * n.M1) + pE * e.M2 + pN * n.M2;
            var z = zE * e.Z + zN * n.Z;
            return memo[(stage, remaining, played)] = (m1, m2, z);
        }

        var result = Visit(start, _configuration.InitialFreeSpins, 0);
        return new FeatureMoments(start, result.M1, result.M2, result.Z);
    }
}
