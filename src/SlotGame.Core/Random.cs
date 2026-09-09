namespace SlotGame.Core;

public interface IRandomSource
{
    uint NextUInt32();
    int NextInt(int exclusiveMaximum);
}

/// <summary>Reproducible PCG-XSH-RR generator for simulation and replay. It is not a cryptographic RNG.</summary>
public sealed class Pcg32Random : IRandomSource
{
    private ulong _state;
    private readonly ulong _increment;

    public Pcg32Random(ulong seed, ulong sequence = 54)
    {
        _increment = (sequence << 1) | 1;
        _state = 0;
        NextUInt32();
        _state += seed;
        NextUInt32();
    }

    public uint NextUInt32()
    {
        var previous = _state;
        _state = unchecked(previous * 6364136223846793005UL + _increment);
        var xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
        var rotation = (int)(previous >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));

        var bound = (uint)exclusiveMaximum;
        var threshold = unchecked(0u - bound) % bound;
        while (true)
        {
            var value = NextUInt32();
            if (value >= threshold)
                return (int)(value % bound);
        }
    }
}

public sealed class SequenceRandom : IRandomSource
{
    private readonly Queue<uint> _values;
    public SequenceRandom(IEnumerable<uint> values) => _values = new Queue<uint>(values);
    public uint NextUInt32() => _values.Count > 0 ? _values.Dequeue() : 0;
    public int NextInt(int exclusiveMaximum) => (int)(NextUInt32() % (uint)exclusiveMaximum);
}
