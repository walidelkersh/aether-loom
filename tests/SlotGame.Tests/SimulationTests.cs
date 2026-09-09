using SlotGame.Core;
using SlotGame.Simulation;

namespace SlotGame.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void WelfordMomentsMatchKnownPopulation()
    {
        var moments = new OnlineMoments();
        foreach (var value in new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }) moments.Push(value);
        Assert.Equal(5, moments.Mean, 12);
        Assert.Equal(4, moments.PopulationVariance, 12);
        Assert.Equal(2, moments.StandardDeviation, 12);
        Assert.Equal(9, moments.Maximum, 12);
    }

    [Fact]
    public void SeededSimulationReproducesAndReconcilesComponents()
    {
        var config = GameConfiguration.LoadDefault();
        var runner = new SimulationRunner(config);
        var first = runner.Run(10_000, 99);
        var second = runner.Run(10_000, 99);
        Assert.Equal(first.ObservedRtp, second.ObservedRtp);
        Assert.Equal(first.TotalPayout, second.TotalPayout);
        Assert.Equal(first.FeatureTriggers, second.FeatureTriggers);
        Assert.Equal(first.StateVisits, second.StateVisits);
        Assert.Equal(first.FeatureStarts, second.FeatureStarts);
        Assert.Equal(first.BaseRtp + first.FeatureRtp, first.ObservedRtp, 10);
        Assert.Equal(first.TotalPayout, first.BaseRtp * first.TotalWager + first.FeatureRtp * first.TotalWager, 8);
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void MonteCarloTracksExactRtpAndFeatureFrequency()
    {
        var config = GameConfiguration.LoadDefault();
        var exact = new ExactAnalyzer(config).Analyze();
        var observed = new SimulationRunner(config).Run(250_000, 20260908);
        Assert.InRange(Math.Abs(observed.ObservedRtp - exact.TotalRtp), 0, 0.04);
        Assert.InRange(Math.Abs(observed.FeatureFrequency - exact.FeatureTriggerProbability), 0, 0.0005);
        Assert.InRange(exact.TotalRtp, observed.ObservedRtp - 3 * observed.RtpConfidenceHalfWidth95, observed.ObservedRtp + 3 * observed.RtpConfidenceHalfWidth95);
    }
}
