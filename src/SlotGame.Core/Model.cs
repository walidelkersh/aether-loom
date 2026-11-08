namespace SlotGame.Core;

public enum SymbolId { Crown, Moth, Shuttle, Dye, Linen, Knot, Wild, Spark }
public enum TensionState { Rest = 0, Taut = 1, Overdrive = 2 }

/// <summary>One symbol's ways win. <see cref="Multiplier"/> is the paytable value per way, in units of total bet.</summary>
public sealed record SymbolWin(SymbolId Symbol, int ReelsMatched, int Ways, decimal Multiplier)
{
    /// <summary>Credits paid. Decimal arithmetic: 0.07 x 3 ways x 0.20 bet is exactly 0.042, never 0.04200000000000001.</summary>
    public decimal Award(decimal bet, decimal stageMultiplier = 1m) => Multiplier * Ways * bet * stageMultiplier;
}

public sealed record SpinResult(
    string ReelSet,
    int[] Stops,
    SymbolId[,] Grid,
    IReadOnlyList<SymbolWin> Wins,
    int ScatterCount,
    decimal Bet,
    decimal StageMultiplier)
{
    public decimal Award => Wins.Sum(win => win.Award(Bet, StageMultiplier));
    public bool IsHit => Award > 0;
}

public sealed record BaseSpinResult(SpinResult Spin, TensionState StateBefore, TensionState StateAfter, bool FeatureTriggered);

public sealed record BonusSpinResult(
    SpinResult Spin,
    TensionState StageBefore,
    TensionState StageAfter,
    int SpinsRemainingAfter,
    bool ExtraSpinAwarded);

public sealed record BonusResult(TensionState StartingStage, IReadOnlyList<BonusSpinResult> Spins)
{
    public decimal Award => Spins.Sum(spin => spin.Spin.Award);
    public int SpinsPlayed => Spins.Count;
}

public sealed record GameCycleResult(BaseSpinResult BaseSpin, BonusResult? Bonus)
{
    public decimal BaseAward => BaseSpin.Spin.Award;
    public decimal FeatureAward => Bonus?.Award ?? 0m;
    public decimal TotalAward => BaseAward + FeatureAward;
}
