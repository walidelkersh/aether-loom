namespace SlotGame.Core;

public sealed class ReelStrip
{
    private readonly SymbolId[] _stops;

    public ReelStrip(IEnumerable<SymbolId> stops)
    {
        _stops = stops.ToArray();
        if (_stops.Length < 3)
            throw new ArgumentException("A reel strip needs at least three stops.", nameof(stops));
    }

    public int Length => _stops.Length;
    public IReadOnlyList<SymbolId> Stops => _stops;
    public SymbolId this[int index] => _stops[Wrap(index)];
    public SymbolId[] WindowAt(int stop) => [this[stop], this[stop + 1], this[stop + 2]];

    private int Wrap(int index)
    {
        var wrapped = index % Length;
        return wrapped < 0 ? wrapped + Length : wrapped;
    }
}

public sealed class ReelSet
{
    public ReelSet(string name, IEnumerable<ReelStrip> reels)
    {
        Name = name;
        Reels = reels.ToArray();
        if (Reels.Count != 5)
            throw new ArgumentException("Aether Loom requires exactly five reels.", nameof(reels));
    }

    public string Name { get; }
    public IReadOnlyList<ReelStrip> Reels { get; }
}
