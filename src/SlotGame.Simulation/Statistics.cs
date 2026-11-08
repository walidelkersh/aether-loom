namespace SlotGame.Simulation;

public sealed class OnlineMoments
{
    public long Count { get; private set; }
    public double Mean { get; private set; }
    public double M2 { get; private set; }
    public double Maximum { get; private set; }

    public void Push(double value)
    {
        Count++;
        var delta = value - Mean;
        Mean += delta / Count;
        M2 += delta * (value - Mean);
        Maximum = Math.Max(Maximum, value);
    }

    public double PopulationVariance => Count == 0 ? 0 : M2 / Count;
    public double StandardDeviation => Math.Sqrt(PopulationVariance);
    public double SampleVariance => Count < 2 ? 0 : M2 / (Count - 1);

    public double MeanConfidenceHalfWidth95 => Count < 2 ? 0 : 1.959964 * Math.Sqrt(SampleVariance / Count);
}

public sealed record ChiSquareResult(double Statistic, int DegreesOfFreedom, double PValue, IReadOnlyList<int> PooledBuckets);

public static class Statistics
{
    public static double StudentT975(int df)
    {
        double[] table = [double.NaN, 12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228,
                          2.201, 2.179, 2.160, 2.145, 2.131, 2.120, 2.110, 2.101, 2.093, 2.086,
                          2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045, 2.042];
        if (df < 1) throw new ArgumentOutOfRangeException(nameof(df));
        if (df < table.Length) return table[df];
        const double z = 1.959964;
        return z + (z * z * z + z) / (4 * df) + (5 * Math.Pow(z, 5) + 16 * z * z * z + 3 * z) / (96.0 * df * df);
    }

    // Buckets expected below 5 are pooled into a neighbour.
    public static ChiSquareResult ChiSquare(IReadOnlyList<long> observed, IReadOnlyList<double> probabilities)
    {
        var total = observed.Sum();
        var pooledObserved = new List<double>();
        var pooledExpected = new List<double>();
        var pooledFrom = new List<int>();
        double o = 0, e = 0;
        var start = 0;
        for (var i = 0; i < observed.Count; i++)
        {
            o += observed[i];
            e += probabilities[i] * total;
            if (e >= 5 || i == observed.Count - 1)
            {
                pooledObserved.Add(o);
                pooledExpected.Add(e);
                pooledFrom.Add(start);
                o = e = 0;
                start = i + 1;
            }
        }
        if (pooledExpected.Count > 1 && pooledExpected[^1] < 5)
        {
            pooledObserved[^2] += pooledObserved[^1]; pooledExpected[^2] += pooledExpected[^1];
            pooledObserved.RemoveAt(pooledObserved.Count - 1); pooledExpected.RemoveAt(pooledExpected.Count - 1);
            pooledFrom.RemoveAt(pooledFrom.Count - 1);
        }
        var statistic = pooledObserved.Zip(pooledExpected).Sum(p => (p.First - p.Second) * (p.First - p.Second) / p.Second);
        var df = pooledExpected.Count - 1;
        return new ChiSquareResult(statistic, df, 1 - RegularizedGammaP(df / 2.0, statistic / 2.0), pooledFrom);
    }

    // Numerical Recipes series / continued fraction.
    public static double RegularizedGammaP(double a, double x)
    {
        if (x <= 0) return 0;
        var gln = LogGamma(a);
        if (x < a + 1)
        {
            double sum = 1 / a, term = sum, ap = a;
            for (var n = 0; n < 1000; n++)
            {
                ap++; term *= x / ap; sum += term;
                if (Math.Abs(term) < Math.Abs(sum) * 1e-15) break;
            }
            return sum * Math.Exp(-x + a * Math.Log(x) - gln);
        }
        double b = x + 1 - a, c = 1 / 1e-300, d = 1 / b, h = d;
        for (var i = 1; i < 1000; i++)
        {
            var an = -i * (i - a);
            b += 2;
            d = an * d + b; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = b + an / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d;
            var delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-15) break;
        }
        return 1 - Math.Exp(-x + a * Math.Log(x) - gln) * h;
    }

    private static double LogGamma(double x)
    {
        double[] g = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        var series = 1.000000000190015;
        foreach (var coefficient in g) series += coefficient / ++y;
        return -tmp + Math.Log(2.5066282746310005 * series / x);
    }
}
