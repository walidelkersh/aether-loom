using SlotGame.Core;

namespace SlotGame.Tests;

/// <summary>Expected values below are worked by hand from the paytable, not produced by the code under test.</summary>
public sealed class HandCalculatedTests
{
    private readonly GameConfiguration _config = GameConfiguration.LoadDefault();

    [Fact]
    public void EightWayCrownExampleFromTheRulesPays3Point20()
    {
        // Two Crown-or-Wild on each of reels 1-3, none on reel 4: 2 x 2 x 2 = 8 ways x 0.40 = 3.20 x bet.
        var grid = Fill(SymbolId.Spark);
        grid[0, 0] = SymbolId.Crown; grid[1, 0] = SymbolId.Crown;
        grid[0, 1] = SymbolId.Crown; grid[1, 1] = SymbolId.Wild;
        grid[0, 2] = SymbolId.Wild;  grid[2, 2] = SymbolId.Crown;
        var win = Assert.Single(new WaysWinEvaluator(_config.Paytable).Evaluate(grid));
        Assert.Equal(8, win.Ways);
        Assert.Equal(3.20m, win.Award(1m));
    }

    [Fact]
    public void FourReelExtensionUsesFourReelPay()
    {
        // Same 2 x 1 x 3 = 6 ways, plus one Crown on reel 4: 6 ways x 1.20 = 7.20 (only the longest match pays).
        var grid = Fill(SymbolId.Spark);
        grid[0, 0] = SymbolId.Crown; grid[1, 0] = SymbolId.Crown;
        grid[0, 1] = SymbolId.Crown;
        grid[0, 2] = SymbolId.Crown; grid[1, 2] = SymbolId.Crown; grid[2, 2] = SymbolId.Crown;
        grid[1, 3] = SymbolId.Crown;
        var win = Assert.Single(new WaysWinEvaluator(_config.Paytable).Evaluate(grid));
        Assert.Equal(4, win.ReelsMatched);
        Assert.Equal(7.20m, win.Award(1m));
    }

    [Fact]
    public void DifferentSymbolsAddAndWildCountsForEach()
    {
        // Reel 2 Wild is shared: Moth 1x1x1 = 1 way x 0.30, Knot 1x1x1 = 1 way x 0.07. Total 0.37 x bet.
        var grid = Fill(SymbolId.Spark);
        grid[0, 0] = SymbolId.Moth; grid[1, 0] = SymbolId.Knot;
        grid[0, 1] = SymbolId.Wild;
        grid[0, 2] = SymbolId.Moth; grid[1, 2] = SymbolId.Knot;
        var wins = new WaysWinEvaluator(_config.Paytable).Evaluate(grid);
        Assert.Equal(2, wins.Count);
        Assert.Equal(0.37m, wins.Sum(w => w.Award(1m)));
    }

    [Fact]
    public void SettlementIsExactInDecimal()
    {
        // 0.07 x 3 ways x 0.20 bet x 1.75 stage multiplier = 0.0735 exactly. In binary double this is 0.07350000000000001.
        var win = new SymbolWin(SymbolId.Knot, 3, 3, 0.07m);
        Assert.Equal(0.0735m, win.Award(0.20m, 1.75m));
    }

    [Fact]
    public void TwoStateStationaryDistributionMatchesBalanceEquation()
    {
        // A->B 10%, B->A 20%: 0.1 pi_A = 0.2 pi_B, so pi = (2/3, 1/3).
        var pi = MarkovMath.StationaryDistribution(new double[,] { { 0.9, 0.1 }, { 0.2, 0.8 } });
        Assert.Equal(2.0 / 3, pi[0], 14);
        Assert.Equal(1.0 / 3, pi[1], 14);
    }

    [Fact]
    public void OverdriveRowAlwaysVentsToRest()
    {
        var matrix = new ExactAnalyzer(_config).Analyze().BaseTransitionMatrix;
        Assert.Equal(1.0, matrix[2, 0], 12);
        Assert.Equal(0.0, matrix[0, 2], 15); // Rest can never jump straight to Overdrive.
    }

    private static SymbolId[,] Fill(SymbolId symbol)
    {
        var grid = new SymbolId[3, 5];
        for (var row = 0; row < 3; row++)
        for (var reel = 0; reel < 5; reel++) grid[row, reel] = symbol;
        return grid;
    }
}
