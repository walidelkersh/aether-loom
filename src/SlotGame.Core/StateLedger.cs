namespace SlotGame.Core;

public enum StatePolicy
{
    // v1.0 behaviour, kept to reproduce the bet-switching exploit in tests.
    SharedAcrossBets,

    PerBetLevel
}

// Cleared on cash-out so a saved Taut/Overdrive state is never left for the next player.
public sealed class StateLedger
{
    private readonly Dictionary<decimal, TensionState> _states = new();
    private TensionState _shared = TensionState.Rest;

    public StateLedger(StatePolicy policy = StatePolicy.PerBetLevel) => Policy = policy;

    public StatePolicy Policy { get; }

    public TensionState StateFor(decimal bet) => Policy == StatePolicy.SharedAcrossBets
        ? _shared
        : _states.GetValueOrDefault(bet, TensionState.Rest);

    public void Record(decimal bet, TensionState next)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        if (Policy == StatePolicy.SharedAcrossBets) _shared = next;
        else _states[bet] = next;
    }

    public GameCycleResult Play(GameEngine engine, decimal bet)
    {
        var cycle = engine.Play(bet, StateFor(bet));
        Record(bet, cycle.BaseSpin.StateAfter);
        return cycle;
    }

    public void Clear()
    {
        _states.Clear();
        _shared = TensionState.Rest;
    }
}
