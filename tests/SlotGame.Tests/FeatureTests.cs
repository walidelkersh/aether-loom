using SlotGame.Core;

namespace SlotGame.Tests;

public sealed class FeatureTests
{
    private readonly GameConfiguration _config = GameConfiguration.LoadDefault();

    [Theory]
    [InlineData(TensionState.Rest, 2, TensionState.Taut)]
    [InlineData(TensionState.Taut, 2, TensionState.Overdrive)]
    [InlineData(TensionState.Taut, 0, TensionState.Rest)]
    [InlineData(TensionState.Taut, 1, TensionState.Taut)]
    [InlineData(TensionState.Overdrive, 0, TensionState.Rest)]
    [InlineData(TensionState.Overdrive, 2, TensionState.Rest)]
    [InlineData(TensionState.Taut, 3, TensionState.Rest)]
    public void BaseStateTransitionsFollowTensionRules(TensionState before, int scatters, TensionState after)
    {
        Assert.Equal(after, GameEngine.NextBaseState(before, scatters));
    }

    [Fact]
    public void ImpossibleStateAndScatterCountsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameEngine.NextBaseState((TensionState)99, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameEngine.NextBaseState(TensionState.Rest, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameEngine.NextBaseState(TensionState.Rest, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(_config, new Pcg32Random(1)).Play(0.3, TensionState.Rest));
    }

    [Fact]
    public void ThreeSparksTriggerFeatureAndResetBaseState()
    {
        var set = _config.BaseReelSet(TensionState.Rest);
        var stops = StopsWithScatterCount(set, 3);
        var result = new GameEngine(_config, new SequenceRandom(stops)).PlayBase(1, TensionState.Rest);
        Assert.True(result.FeatureTriggered);
        Assert.Equal(3, result.Spin.ScatterCount);
        Assert.Equal(TensionState.Rest, result.StateAfter);
    }

    [Fact]
    public void BonusInheritsStageAndEscalatesOnTwoSparks()
    {
        var set = _config.BonusReelSet(TensionState.Rest);
        var stops = StopsWithScatterCount(set, 2);
        var result = new GameEngine(_config, new SequenceRandom(stops)).PlayBonus(1, TensionState.Rest);
        var first = result.Spins[0];
        Assert.Equal(TensionState.Rest, result.StartingStage);
        Assert.Equal(TensionState.Rest, first.StageBefore);
        Assert.Equal(TensionState.Taut, first.StageAfter);
        Assert.True(first.ExtraSpinAwarded);
    }

    [Fact]
    public void BonusAlwaysTerminatesAtConfiguredCap()
    {
        var set = _config.BonusReelSet(TensionState.Overdrive);
        var forcingStops = StopsWithScatterCount(set, 2);
        var sequence = Enumerable.Range(0, _config.MaximumFeatureSpins).SelectMany(_ => forcingStops).Select(x => (uint)x);
        var result = new GameEngine(_config, new SequenceRandom(sequence)).PlayBonus(1, TensionState.Overdrive);
        Assert.Equal(_config.MaximumFeatureSpins, result.SpinsPlayed);
        Assert.False(result.Spins[^1].ExtraSpinAwarded);
        Assert.All(result.Spins, spin => Assert.True(spin.Spin.Award >= 0));
    }

    [Fact]
    public void BetScalesPayoutWithoutChangingOutcome()
    {
        uint[] stops = [1, 2, 3, 4, 5];
        var one = new GameEngine(_config, new SequenceRandom(stops)).PlayBase(1, TensionState.Rest);
        var five = new GameEngine(_config, new SequenceRandom(stops)).PlayBase(5, TensionState.Rest);
        Assert.Equal(one.Spin.Stops, five.Spin.Stops);
        Assert.Equal(one.Spin.Award * 5, five.Spin.Award, 10);
    }

    private static uint[] StopsWithScatterCount(ReelSet set, int target)
    {
        var values = new uint[5];
        for (var reel = 0; reel < 5; reel++)
        {
            var wantsScatter = reel < target;
            values[reel] = (uint)Enumerable.Range(0, set.Reels[reel].Length).First(stop =>
                set.Reels[reel].WindowAt(stop).Contains(SymbolId.Spark) == wantsScatter);
        }
        return values;
    }
}
