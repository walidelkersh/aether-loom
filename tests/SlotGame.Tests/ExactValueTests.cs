using SlotGame.Core;

namespace SlotGame.Tests;

/// <summary>
/// Pins the headline figures to values computed OUTSIDE this C# code base, by
/// scripts/independent_check.py (pure Python written from the game rules in docs/game-design.md, using
/// fractions.Fraction, sharing no code with SlotGame.Core). A wrong rule in the shared C# core would make the
/// engine, simulator and analyzers agree with each other and still fail here.
/// </summary>
public sealed class ExactValueTests
{
    private static readonly GameConfiguration Config = GameConfiguration.LoadDefault();
    private static readonly Lazy<ParStatistics> Par = new(() => new ParAnalyzer(Config).Analyze());

    // Independent reference values (scripts/independent_check.py, 15 significant digits).
    private const double ReferenceBaseRtp = 0.804316415315999;
    private const double ReferenceFeatureRtp = 0.161352733018644;
    private const double ReferenceTotalRtp = 0.965669148334642;
    private const double ReferenceFeatureFrequency = 0.004206244043356;

    [Fact]
    public void FloatingPointAnalyzerMatchesIndependentReference()
    {
        var report = new ExactAnalyzer(Config).Analyze();
        Assert.Equal(ReferenceBaseRtp, report.BaseRtp, 12);
        Assert.Equal(ReferenceFeatureRtp, report.FeatureRtp, 12);
        Assert.Equal(ReferenceTotalRtp, report.TotalRtp, 12);
        Assert.Equal(ReferenceFeatureFrequency, report.FeatureTriggerProbability, 12);
    }

    [Fact]
    public void WindowDistributionEvMatchesFullEnumeration()
    {
        var analyzer = new ExactAnalyzer(Config);
        var distributions = new ParAnalyzer(Config).Distributions;
        foreach (var set in Config.ReelSets.Values)
            Assert.Equal(analyzer.AnalyzeReelSet(set).ExpectedWaysAward, distributions[set.Name].ExpectedAward, tolerance: 1e-12);
    }

    [Fact]
    public void EnumeratedRtpAndHitRateMatchAnalyzer()
    {
        var floating = new ExactAnalyzer(Config).Analyze();
        Assert.Equal(floating.TotalRtp, Par.Value.Rtp, 13);
        Assert.Equal(floating.BaseHitProbability, Par.Value.BaseHitFrequency, 13);
    }

    [Fact]
    public void ExactVarianceAndCorrelationMatchIndependentReference()
    {
        var stats = Par.Value;
        Assert.Equal(14.912377, stats.Variance, 5);
        Assert.Equal(3.8616547, stats.StandardDeviation, 6);
        Assert.Equal(14.831976, stats.AsymptoticVariance, 5);
        Assert.Equal(1.644854 * stats.StandardDeviation, stats.VolatilityIndex90, 10);
        Assert.InRange(stats.CorrelationFactor, 0.99, 1.0); // persistent state slightly reduces long-run variance here
    }

    [Fact]
    public void StateValuesAndConditionalReturnsMatchIndependentReference()
    {
        var report = new ExactAnalyzer(Config).Analyze();
        Assert.Equal(0.912940, report.ConditionalStateRtp[0], 6);
        Assert.Equal(1.530903, report.ConditionalStateRtp[1], 6);
        Assert.Equal(3.720908, report.ConditionalStateRtp[2], 6);
        var stats = Par.Value;
        Assert.Equal(0, stats.States[0].RelativeValue, 12);
        Assert.Equal(1.1844, stats.States[1].RelativeValue, 4);
        Assert.Equal(2.7552, stats.States[2].RelativeValue, 4);
    }

    [Fact]
    public void ConstantBetGivesRtpAndSharedStateBetSwitchGivesExploitReturn()
    {
        var report = new ExactAnalyzer(Config).Analyze();
        Assert.Equal(report.TotalRtp, report.WagerWeightedReturn([1, 1, 1]), 14);
        Assert.Equal(1.522342, report.WagerWeightedReturn([0.2, 10, 10]), 6);
    }

    [Fact]
    public void WinDistributionIsAProbabilityDistributionAndMatchesHitRate()
    {
        var stats = Par.Value;
        Assert.Equal(1, stats.BaseWinDistribution.Sum(), 12);
        Assert.Equal(1 - stats.BaseHitFrequency, stats.BaseWinDistribution[0], 12);
    }

    [Fact]
    public void FeatureMomentsAgreeWithFirstMomentRecursion()
    {
        var report = new ExactAnalyzer(Config).Analyze();
        foreach (var feature in Par.Value.Features)
        {
            Assert.Equal(report.Features[(int)feature.StartingStage].ExpectedAward, feature.Mean, 10);
            Assert.True(feature.Variance > 0);
            Assert.InRange(feature.ZeroProbability, 0, 1e-3);
        }
    }

    [Fact]
    public void SessionsStartingInRestReturnLessThanLongRunRtp()
    {
        var stats = Par.Value;
        var first = stats.SessionRtpFromRest[0];
        var last = stats.SessionRtpFromRest[^1];
        Assert.True(first.ExpectedRtp < stats.Rtp);
        Assert.InRange(stats.Rtp - last.ExpectedRtp, 0, 2e-4);
    }

    [Fact]
    public void SharedStripsAreDetectedAndDocumented()
    {
        var group = Assert.Single(Config.SharedStripGroups());
        Assert.Equal(["BASE_OVERDRIVE", "BONUS_REST"], group.Order());
    }
}
