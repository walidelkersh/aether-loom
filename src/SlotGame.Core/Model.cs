namespace SlotGame.Core;

public enum SymbolId { Crown, Moth, Shuttle, Dye, Linen, Knot, Wild, Spark }
public enum TensionState { Rest = 0, Taut = 1, Overdrive = 2 }

public sealed record SymbolDefinition(SymbolId Id, string DisplayName, bool IsWild = false, bool IsScatter = false);

public sealed record SymbolWin(SymbolId Symbol, int ReelsMatched, int Ways, double Multiplier)
{
    public double Award(double bet, double stageMultiplier = 1.0) => Multiplier * Ways * bet * stageMultiplier;
}

public sealed record SpinResult(
    string ReelSet,
    int[] Stops,
    SymbolId[,] Grid,
    IReadOnlyList<SymbolWin> Wins,
    int ScatterCount,
    double Bet,
    double StageMultiplier)
{
    public double Award => Wins.Sum(win => win.Award(Bet, StageMultiplier));
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
    public double Award => Spins.Sum(spin => spin.Spin.Award);
    public int SpinsPlayed => Spins.Count;
}

public sealed record GameCycleResult(BaseSpinResult BaseSpin, BonusResult? Bonus)
{
    public double BaseAward => BaseSpin.Spin.Award;
    public double FeatureAward => Bonus?.Award ?? 0;
    public double TotalAward => BaseAward + FeatureAward;
}
