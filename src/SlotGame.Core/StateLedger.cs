namespace SlotGame.Core;

/// <summary>How persistent tension state is scoped when the player can change bet.</summary>
public enum StatePolicy
{
    /// <summary>
    /// Version 1.0 behaviour: one state shared by every bet level. Exploitable: a player can build tension at the
    /// minimum bet and spend Taut/Overdrive at the maximum bet (152.23% wager-weighted return, see docs/advantage-play.md).
    /// Kept only so the exploit can be reproduced and tested.
    /// </summary>
    SharedAcrossBets,

    /// <summary>
    /// Industry-standard fix for persistent-state games: each bet level owns an independent state. Tension earned at
    /// 0.20 can only be spent at 0.20, so the return of every bet level's chain is the constant-wager RTP.
    /// </summary>
    PerBetLevel
}

/// <summary>
/// Session-scoped store of persistent tension state. Every session starts in Rest and the ledger is cleared on
/// cash-out, so a saved Taut/Overdrive state can never be left behind for the next player ("machine hunting").
/// </summary>
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

    /// <summary>Plays one paid cycle at <paramref name="bet"/> using, and then updating, that bet level's state.</summary>
    public GameCycleResult Play(GameEngine engine, decimal bet)
    {
        var cycle = engine.Play(bet, StateFor(bet));
        Record(bet, cycle.BaseSpin.StateAfter);
        return cycle;
    }

    /// <summary>Cash-out / session end. Persistent state never survives the session that earned it.</summary>
    public void Clear()
    {
        _states.Clear();
        _shared = TensionState.Rest;
    }
}
