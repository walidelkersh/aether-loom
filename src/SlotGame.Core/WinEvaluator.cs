namespace SlotGame.Core;

public sealed class WaysWinEvaluator
{
    private readonly Paytable _paytable;
    public WaysWinEvaluator(Paytable paytable) => _paytable = paytable;

    public IReadOnlyList<SymbolWin> Evaluate(SymbolId[,] grid)
    {
        if (grid.GetLength(0) != 3 || grid.GetLength(1) != 5)
            throw new ArgumentException("Ways evaluation requires a 3x5 grid.", nameof(grid));

        var wins = new List<SymbolWin>();
        foreach (var symbol in _paytable.PayingSymbols)
        {
            var matchedReels = 0;
            var ways = 1;
            for (var reel = 0; reel < 5; reel++)
            {
                var matches = 0;
                for (var row = 0; row < 3; row++)
                    if (grid[row, reel] == symbol || grid[row, reel] == SymbolId.Wild) matches++;

                if (matches == 0) break;
                matchedReels++;
                ways *= matches;
            }

            var multiplier = _paytable.Get(symbol, matchedReels);
            if (multiplier > 0)
                wins.Add(new SymbolWin(symbol, matchedReels, ways, multiplier));
        }
        return wins;
    }
}
