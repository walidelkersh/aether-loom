using SlotGame.Core;
using SlotGame.Simulation;

namespace SlotGame.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public void ChiSquarePValueMatchesTables()
    {
        // 3.841 on 1 df and 11.070 on 5 df are the 5% critical values.
        Assert.Equal(0.05, 1 - Statistics.RegularizedGammaP(0.5, 3.841459 / 2), 5);
        Assert.Equal(0.05, 1 - Statistics.RegularizedGammaP(2.5, 11.0705 / 2), 4);
    }

    [Fact]
    public void ChiSquarePoolsSparseBuckets()
    {
        var result = Statistics.ChiSquare([500, 300, 199, 1], [0.5, 0.3, 0.1999, 0.0001]);
        Assert.Equal(2, result.DegreesOfFreedom); // last bucket (expected 0.1) pooled into its neighbour
        Assert.InRange(result.PValue, 0.9, 1.0);
    }

    [Fact]
    public void StudentCriticalValuesConvergeToNormal()
    {
        Assert.Equal(2.262, Statistics.StudentT975(9), 3);
        Assert.InRange(Statistics.StudentT975(99), 1.98, 1.99);
        Assert.InRange(Statistics.StudentT975(100_000), 1.9599, 1.9601);
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void SimulatedWinDistributionPassesChiSquareAgainstExactEnumeration()
    {
        var config = GameConfiguration.LoadDefault();
        var exact = new ParAnalyzer(config).Analyze();
        var simulation = new SimulationRunner(config).Run(300_000, 424242);
        var chi = Statistics.ChiSquare(simulation.BaseWinBuckets, exact.BaseWinDistribution);
        Assert.True(chi.PValue > 0.001, $"chi-square {chi.Statistic:F2} on {chi.DegreesOfFreedom} df, p = {chi.PValue:G3}");
        Assert.InRange(simulation.HitFrequency, exact.HitFrequency - 0.005, exact.HitFrequency + 0.005);
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void SimulatedRtpIsWithinFourCorrelationCorrectedStandardErrors()
    {
        var config = GameConfiguration.LoadDefault();
        var exact = new ParAnalyzer(config).Analyze();
        const long spins = 300_000;
        var simulation = new SimulationRunner(config).Run(spins, 7);
        var z = (simulation.ObservedRtp - exact.Rtp) / Math.Sqrt(exact.AsymptoticVariance / spins);
        Assert.InRange(z, -4, 4);
        Assert.True(simulation.RtpConfidenceHalfWidth95BatchMeans > 0);
    }
}
