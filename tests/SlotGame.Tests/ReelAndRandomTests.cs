using SlotGame.Core;

namespace SlotGame.Tests;

public sealed class ReelAndRandomTests
{
    [Fact]
    public void CircularStripIndexesBothDirections()
    {
        var reel = new ReelStrip([SymbolId.Crown, SymbolId.Moth, SymbolId.Spark]);
        Assert.Equal(SymbolId.Spark, reel[-1]);
        Assert.Equal(SymbolId.Crown, reel[3]);
        Assert.Equal([SymbolId.Spark, SymbolId.Crown, SymbolId.Moth], reel.WindowAt(2));
    }

    [Fact]
    public void SameSeedProducesSameCycle()
    {
        var config = GameConfiguration.LoadDefault();
        var first = new GameEngine(config, new Pcg32Random(123456)).Play(1, TensionState.Rest);
        var second = new GameEngine(config, new Pcg32Random(123456)).Play(1, TensionState.Rest);
        Assert.Equal(first.BaseSpin.Spin.Stops, second.BaseSpin.Spin.Stops);
        Assert.Equal(first.TotalAward, second.TotalAward);
        Assert.Equal(Flatten(first.BaseSpin.Spin.Grid), Flatten(second.BaseSpin.Spin.Grid));
    }

    [Fact]
    public void DifferentSeedsChangeStopSequence()
    {
        var config = GameConfiguration.LoadDefault();
        var first = new GameEngine(config, new Pcg32Random(1)).PlayBase(1, TensionState.Rest);
        var second = new GameEngine(config, new Pcg32Random(2)).PlayBase(1, TensionState.Rest);
        Assert.False(first.Spin.Stops.SequenceEqual(second.Spin.Stops));
    }

    [Fact]
    public void EveryConfiguredStripIsPhysicalAndSpecialsCannotStackInWindow()
    {
        var config = GameConfiguration.LoadDefault();
        foreach (var set in config.ReelSets.Values)
        {
            Assert.Equal(5, set.Reels.Count);
            Assert.All(set.Reels, reel => Assert.Equal(40, reel.Length));
            Assert.DoesNotContain(SymbolId.Wild, set.Reels[0].Stops);
            foreach (var reel in set.Reels)
            for (var stop = 0; stop < reel.Length; stop++)
            {
                Assert.InRange(reel.WindowAt(stop).Count(x => x == SymbolId.Spark), 0, 1);
                Assert.InRange(reel.WindowAt(stop).Count(x => x == SymbolId.Wild), 0, 1);
            }
        }
    }

    private static SymbolId[] Flatten(SymbolId[,] grid) => grid.Cast<SymbolId>().ToArray();
}
