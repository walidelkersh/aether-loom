using SlotGame.Core;
using SlotGame.Simulation;

namespace SlotGame.Tests;

public sealed class ExposureAndStateTests
{
    private readonly GameConfiguration _config = GameConfiguration.LoadDefault();

    [Fact]
    public void BaselineMaximumCycleIsUnchanged()
    {
        var extremes = new ExtremeAnalyzer(_config).Analyze();
        Assert.Equal(88.00, extremes.MaximumBaseAward, 9);
        Assert.Equal(11_227.60, extremes.MaximumGameCycleAward, 6);
        Assert.Equal(Enum.GetValues<TensionState>(), extremes.ReachableBaseStates);
    }

    [Fact]
    public void MaximumOnlyCombinesReachableEvents()
    {
        // BASE_REST loses its Sparks, so Rest is absorbing and the feature can't trigger. v1.0 reported ~11,227x here.
        var lowSpark = SensitivityRunner.WithBaseSparkDelta(_config, -1);
        var extremes = new ExtremeAnalyzer(lowSpark).Analyze();
        var rest = extremes.ReelSets["BASE_REST"];
        Assert.Null(rest.MaximumAtLeastThreeScatters);
        Assert.Equal([TensionState.Rest], extremes.ReachableBaseStates);
        Assert.Equal(rest.MaximumAnyAward, extremes.MaximumGameCycleAward, 9);
        Assert.Equal(49.60, extremes.MaximumGameCycleAward, 6);
    }

    [Fact]
    public void ExtremeAnalyzerMaximumMatchesFullEnumerationMaximum()
    {
        var extremes = new ExtremeAnalyzer(_config).Analyze();
        var distributions = new ParAnalyzer(_config).Distributions;
        foreach (var set in _config.ReelSets.Values)
            Assert.Equal(extremes.ReelSets[set.Name].MaximumAnyAward,
                (double)distributions[set.Name].MaximumAwardUnits / distributions[set.Name].Scale, 9);
    }

    [Fact]
    public void PerBetLedgerIsolatesStateBetweenBetLevels()
    {
        var ledger = new StateLedger(StatePolicy.PerBetLevel);
        ledger.Record(0.2m, TensionState.Overdrive);
        Assert.Equal(TensionState.Overdrive, ledger.StateFor(0.2m));
        Assert.Equal(TensionState.Rest, ledger.StateFor(10m));
        ledger.Clear();
        Assert.Equal(TensionState.Rest, ledger.StateFor(0.2m));
    }

    [Fact]
    public void SharedLedgerReproducesVersionOneBehaviour()
    {
        var ledger = new StateLedger(StatePolicy.SharedAcrossBets);
        ledger.Record(0.2m, TensionState.Taut);
        Assert.Equal(TensionState.Taut, ledger.StateFor(10m));
    }

    [Fact]
    [Trait("Category", "Statistical")]
    public void BetSwitchingIsProfitableOnlyWithSharedState()
    {
        var exact = new ExactAnalyzer(_config).Analyze().TotalRtp;
        var shared = BetSwitchRunner.Run(_config, StatePolicy.SharedAcrossBets, 400_000, 11);
        var perBet = BetSwitchRunner.Run(_config, StatePolicy.PerBetLevel, 400_000, 11);
        Assert.True(shared.Return > 1.25, $"shared-state exploit returned {shared.Return:P2}");
        Assert.InRange(perBet.Return, exact - 0.05, exact + 0.05);
    }
}
