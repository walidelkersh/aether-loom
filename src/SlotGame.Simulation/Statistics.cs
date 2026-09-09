namespace SlotGame.Simulation;

public sealed class OnlineMoments
{
    public long Count { get; private set; }
    public double Mean { get; private set; }
    public double M2 { get; private set; }
    public double Maximum { get; private set; }

    public void Push(double value)
    {
        Count++;
        var delta = value - Mean;
        Mean += delta / Count;
        M2 += delta * (value - Mean);
        Maximum = Math.Max(Maximum, value);
    }

    public double PopulationVariance => Count == 0 ? 0 : M2 / Count;
    public double StandardDeviation => Math.Sqrt(PopulationVariance);
    public double SampleVariance => Count < 2 ? 0 : M2 / (Count - 1);
    public double MeanConfidenceHalfWidth95 => Count < 2 ? 0 : 1.96 * Math.Sqrt(SampleVariance / Count);
}
