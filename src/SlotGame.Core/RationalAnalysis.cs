namespace SlotGame.Core;

public sealed record RationalMathReport(
    Fraction BaseRtp,
    Fraction FeatureRtp,
    Fraction TotalRtp,
    Fraction FeatureTriggerProbability,
    IReadOnlyList<Fraction> StationaryStateProbabilities,
    IReadOnlyList<Fraction> FeatureExpectedAwards);

/// <summary>
/// Second, independent derivation of the RTP in exact rational arithmetic (no floating point anywhere).
/// It deliberately uses a different method from <see cref="ExactAnalyzer"/>:
/// <list type="bullet">
/// <item>Ways EV by the closed form E = p3 E[C1]E[C2]E[C3]P(C4=0) + p4 E[C1..C4]P(C5=0) + p5 E[C1..C5],
/// valid because reels stop independently and a zero count zeroes the product.</item>
/// <item>Stationary distribution by exact Gaussian elimination over the rationals.</item>
/// <item>Feature EV by backward induction over (stage, spins remaining, spins played).</item>
/// </list>
/// Agreement with the floating-point analyzer to ~1e-15 is a regression test on both.
/// </summary>
public sealed class RationalAnalyzer
{
    private readonly GameConfiguration _configuration;

    public RationalAnalyzer(GameConfiguration configuration) => _configuration = configuration;

    public RationalMathReport Analyze()
    {
        var waysEv = new Dictionary<string, Fraction>();
        var scatter = new Dictionary<string, Fraction[]>();
        foreach (var set in _configuration.ReelSets.Values)
        {
            waysEv[set.Name] = WaysExpectedValue(set);
            scatter[set.Name] = ScatterDistribution(set);
        }

        var transition = new Fraction[3, 3];
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) transition[i, j] = Fraction.Zero;
        foreach (var state in Enum.GetValues<TensionState>())
        {
            var distribution = scatter[_configuration.BaseReelSet(state).Name];
            for (var count = 0; count < distribution.Length; count++)
            {
                var next = GameEngine.NextBaseState(state, count);
                transition[(int)state, (int)next] += distribution[count];
            }
        }

        var stationary = Stationary(transition);
        var features = Enum.GetValues<TensionState>().Select(stage => FeatureValue(stage, waysEv, scatter)).ToArray();

        Fraction baseRtp = 0, featureRtp = 0, trigger = 0;
        foreach (var state in Enum.GetValues<TensionState>())
        {
            var name = _configuration.BaseReelSet(state).Name;
            var p = scatter[name].Skip(3).Aggregate(Fraction.Zero, (a, b) => a + b);
            baseRtp += stationary[(int)state] * waysEv[name];
            trigger += stationary[(int)state] * p;
            featureRtp += stationary[(int)state] * p * features[(int)state];
        }
        return new RationalMathReport(baseRtp, featureRtp, baseRtp + featureRtp, trigger, stationary, features);
    }

    public Fraction WaysExpectedValue(ReelSet set)
    {
        Fraction total = 0;
        foreach (var symbol in _configuration.Paytable.PayingSymbols)
        {
            var mean = new Fraction[5];
            var zero = new Fraction[5];
            for (var reel = 0; reel < 5; reel++)
            {
                var strip = set.Reels[reel];
                long countSum = 0, zeroStops = 0;
                for (var stop = 0; stop < strip.Length; stop++)
                {
                    var c = strip.WindowAt(stop).Count(x => x == symbol || x == SymbolId.Wild);
                    countSum += c;
                    if (c == 0) zeroStops++;
                }
                mean[reel] = new Fraction(countSum, strip.Length);
                zero[reel] = new Fraction(zeroStops, strip.Length);
            }

            Fraction product = mean[0] * mean[1];
            for (var length = 3; length <= 5; length++)
            {
                product *= mean[length - 1];
                var pay = Fraction.FromDecimal(_configuration.Paytable.GetExact(symbol, length));
                var stops = length < 5 ? zero[length] : Fraction.One;
                total += pay * product * stops;
            }
        }
        return total;
    }

    private static Fraction[] ScatterDistribution(ReelSet set)
    {
        var total = new Fraction[] { 1 };
        foreach (var reel in set.Reels)
        {
            var perReel = new Fraction[4];
            for (var i = 0; i < 4; i++) perReel[i] = Fraction.Zero;
            for (var stop = 0; stop < reel.Length; stop++)
                perReel[reel.WindowAt(stop).Count(x => x == SymbolId.Spark)] += new Fraction(1, reel.Length);

            var next = new Fraction[total.Length + 3];
            for (var i = 0; i < next.Length; i++) next[i] = Fraction.Zero;
            for (var current = 0; current < total.Length; current++)
            for (var count = 0; count < 4; count++)
                next[current + count] += total[current] * perReel[count];
            total = next;
        }
        return total;
    }

    private static Fraction[] Stationary(Fraction[,] p)
    {
        // Rows 0..1: balance equations (P^T - I) pi = 0. Row 2: sum(pi) = 1.
        var a = new Fraction[3, 4];
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 3; column++)
                a[row, column] = p[column, row] - (row == column ? Fraction.One : Fraction.Zero);
            a[row, 3] = Fraction.Zero;
        }
        for (var column = 0; column < 3; column++) a[2, column] = Fraction.One;
        a[2, 3] = Fraction.One;

        for (var pivot = 0; pivot < 3; pivot++)
        {
            var swap = Enumerable.Range(pivot, 3 - pivot).First(r => !a[r, pivot].IsZero);
            for (var c = 0; c < 4; c++) (a[pivot, c], a[swap, c]) = (a[swap, c], a[pivot, c]);
            for (var row = 0; row < 3; row++)
            {
                if (row == pivot || a[row, pivot].IsZero) continue;
                var factor = a[row, pivot] / a[pivot, pivot];
                for (var c = pivot; c < 4; c++) a[row, c] -= factor * a[pivot, c];
            }
        }
        return Enumerable.Range(0, 3).Select(i => a[i, 3] / a[i, i]).ToArray();
    }

    private Fraction FeatureValue(TensionState start, IReadOnlyDictionary<string, Fraction> waysEv, IReadOnlyDictionary<string, Fraction[]> scatter)
    {
        var memo = new Dictionary<(TensionState, int, int), Fraction>();
        Fraction Value(TensionState stage, int remaining, int played)
        {
            if (remaining == 0 || played >= _configuration.MaximumFeatureSpins) return Fraction.Zero;
            if (memo.TryGetValue((stage, remaining, played), out var cached)) return cached;
            var name = _configuration.BonusReelSet(stage).Name;
            var award = waysEv[name] * Fraction.FromDecimal(_configuration.StageMultiplierExact(stage));
            var escalate = played + 1 < _configuration.MaximumFeatureSpins
                ? scatter[name].Skip(2).Aggregate(Fraction.Zero, (x, y) => x + y)
                : Fraction.Zero;
            var next = (TensionState)Math.Min(2, (int)stage + 1);
            var value = award + escalate * Value(next, remaining, played + 1)
                + (Fraction.One - escalate) * Value(stage, remaining - 1, played + 1);
            memo[(stage, remaining, played)] = value;
            return value;
        }
        return Value(start, _configuration.InitialFreeSpins, 0);
    }
}
