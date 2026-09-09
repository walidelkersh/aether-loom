using SlotGame.Core;

namespace SlotGame.Tests;

public sealed class MathematicalTests
{
    private readonly GameConfiguration _config = GameConfiguration.LoadDefault();

    [Fact]
    public void ProbabilityDistributionsAndTransitionRowsSumToOne()
    {
        var report = new ExactAnalyzer(_config).Analyze();
        foreach (var set in report.ReelSets.Values)
        {
            Assert.InRange(set.ScatterProbabilities.Sum(), 1 - 1e-12, 1 + 1e-12);
            Assert.All(set.ScatterProbabilities, p => Assert.InRange(p, 0, 1));
            Assert.True(set.ExpectedWaysAward >= 0);
        }

        for (var row = 0; row < 3; row++)
        {
            var sum = Enumerable.Range(0, 3).Sum(column => report.BaseTransitionMatrix[row, column]);
            Assert.InRange(sum, 1 - 1e-12, 1 + 1e-12);
        }
        Assert.InRange(report.StationaryStateProbabilities.Sum(), 1 - 1e-12, 1 + 1e-12);
    }

    [Fact]
    public void RtpComponentsReconcileExactly()
    {
        var report = new ExactAnalyzer(_config).Analyze();
        Assert.Equal(report.BaseRtp + report.FeatureRtp, report.TotalRtp, 12);
        Assert.InRange(report.TotalRtp, 0.94, 0.97);
        Assert.True(report.FeatureRtp > 0);
    }

    [Fact]
    public void FeatureStageRaisesExpectedAwardAndVolatilityControl()
    {
        var report = new ExactAnalyzer(_config).Analyze();
        var stageAwards = Enum.GetValues<TensionState>()
            .Select(stage => report.ReelSets[_config.BonusReelSet(stage).Name].ExpectedWaysAward * _config.StageMultiplier(stage))
            .ToArray();
        Assert.True(stageAwards[1] > stageAwards[0]);
        Assert.True(stageAwards[2] > stageAwards[1]);
    }

    [Fact]
    public void FeatureDurationIsFiniteAndBounded()
    {
        var report = new ExactAnalyzer(_config).Analyze();
        Assert.All(report.Features, feature => Assert.InRange(feature.ExpectedSpins, _config.InitialFreeSpins, _config.MaximumFeatureSpins));
    }
}
