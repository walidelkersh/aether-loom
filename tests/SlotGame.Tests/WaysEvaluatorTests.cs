using SlotGame.Core;

namespace SlotGame.Tests;

public sealed class WaysEvaluatorTests
{
    private readonly GameConfiguration _config = GameConfiguration.LoadDefault();

    [Fact]
    public void CountsWaysAsProductAcrossConsecutiveReels()
    {
        var grid = Fill(SymbolId.Spark);
        grid[0, 0] = SymbolId.Crown;
        grid[1, 0] = SymbolId.Crown;
        grid[0, 1] = SymbolId.Crown;
        grid[0, 2] = SymbolId.Crown;
        grid[1, 2] = SymbolId.Crown;
        grid[2, 2] = SymbolId.Crown;

        var win = Assert.Single(new WaysWinEvaluator(_config.Paytable).Evaluate(grid));
        Assert.Equal(SymbolId.Crown, win.Symbol);
        Assert.Equal(3, win.ReelsMatched);
        Assert.Equal(6, win.Ways);
        Assert.Equal(2.4, win.Award(1), 10);
    }

    [Fact]
    public void WildSubstitutesAndScatterDoesNot()
    {
        var grid = Fill(SymbolId.Spark);
        grid[0, 0] = SymbolId.Moth;
        grid[0, 1] = SymbolId.Wild;
        grid[1, 1] = SymbolId.Moth;
        grid[0, 2] = SymbolId.Moth;

        var win = Assert.Single(new WaysWinEvaluator(_config.Paytable).Evaluate(grid));
        Assert.Equal(SymbolId.Moth, win.Symbol);
        Assert.Equal(2, win.Ways);
        Assert.Equal(0.6, win.Award(1), 10);
    }

    [Fact]
    public void OnlyLongestMatchForEachSymbolPays()
    {
        var grid = Fill(SymbolId.Spark);
        for (var reel = 0; reel < 5; reel++) grid[0, reel] = SymbolId.Crown;
        var win = Assert.Single(new WaysWinEvaluator(_config.Paytable).Evaluate(grid));
        Assert.Equal(5, win.ReelsMatched);
        Assert.Equal(4, win.Multiplier);
    }

    [Fact]
    public void InvalidGridIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WaysWinEvaluator(_config.Paytable).Evaluate(new SymbolId[3, 4]));
    }

    private static SymbolId[,] Fill(SymbolId symbol)
    {
        var grid = new SymbolId[3, 5];
        for (var row = 0; row < 3; row++)
        for (var reel = 0; reel < 5; reel++) grid[row, reel] = symbol;
        return grid;
    }
}
