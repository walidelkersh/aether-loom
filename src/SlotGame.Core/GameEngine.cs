namespace SlotGame.Core;

public sealed class GameEngine
{
    private readonly GameConfiguration _configuration;
    private readonly WaysWinEvaluator _evaluator;
    private readonly IRandomSource _random;

    public GameEngine(GameConfiguration configuration, IRandomSource random)
    {
        _configuration = configuration;
        _configuration.Validate();
        _evaluator = new WaysWinEvaluator(configuration.Paytable);
        _random = random;
    }

    public GameCycleResult Play(double bet, TensionState state)
    {
        ValidateBet(bet);
        var baseSpin = PlayBase(bet, state);
        var bonus = baseSpin.FeatureTriggered ? PlayBonus(bet, state) : null;
        return new GameCycleResult(baseSpin, bonus);
    }

    public BaseSpinResult PlayBase(double bet, TensionState state)
    {
        ValidateBet(bet);
        var spin = Spin(_configuration.BaseReelSet(state), bet, 1.0);
        var triggered = spin.ScatterCount >= 3;
        return new BaseSpinResult(spin, state, NextBaseState(state, spin.ScatterCount), triggered);
    }

    public BonusResult PlayBonus(double bet, TensionState startingStage)
    {
        ValidateBet(bet);
        var stage = startingStage;
        var remaining = _configuration.InitialFreeSpins;
        var played = 0;
        var spins = new List<BonusSpinResult>(_configuration.MaximumFeatureSpins);

        while (remaining > 0 && played < _configuration.MaximumFeatureSpins)
        {
            remaining--;
            played++;
            var stageBefore = stage;
            var spin = Spin(_configuration.BonusReelSet(stage), bet, _configuration.StageMultiplier(stage));
            var awarded = spin.ScatterCount >= 2 && played < _configuration.MaximumFeatureSpins;
            if (awarded)
            {
                remaining++;
                stage = (TensionState)Math.Min((int)TensionState.Overdrive, (int)stage + 1);
            }

            spins.Add(new BonusSpinResult(spin, stageBefore, stage, remaining, awarded));
        }
        return new BonusResult(startingStage, spins);
    }

    public static TensionState NextBaseState(TensionState state, int scatterCount)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (scatterCount is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(scatterCount));
        if (scatterCount >= 3 || state == TensionState.Overdrive)
            return TensionState.Rest;

        return scatterCount switch
        {
            0 => (TensionState)Math.Max((int)TensionState.Rest, (int)state - 1),
            1 => state,
            2 => (TensionState)Math.Min((int)TensionState.Overdrive, (int)state + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(scatterCount))
        };
    }

    private SpinResult Spin(ReelSet set, double bet, double multiplier)
    {
        var stops = new int[5];
        var grid = new SymbolId[3, 5];
        for (var reel = 0; reel < 5; reel++)
        {
            stops[reel] = _random.NextInt(set.Reels[reel].Length);
            for (var row = 0; row < 3; row++)
                grid[row, reel] = set.Reels[reel][stops[reel] + row];
        }

        var scatterCount = 0;
        foreach (var symbol in grid)
            if (symbol == SymbolId.Spark) scatterCount++;

        return new SpinResult(set.Name, stops, grid, _evaluator.Evaluate(grid), scatterCount, bet, multiplier);
    }

    private void ValidateBet(double bet)
    {
        if (bet <= 0 || !_configuration.Bets.Contains(bet))
            throw new ArgumentOutOfRangeException(nameof(bet), $"Bet must be one of: {string.Join(", ", _configuration.Bets)}");
    }
}
