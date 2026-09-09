using SlotGame.Core;

namespace SlotGame.Simulation;

public sealed record SimulationReport(
    long PaidSpins, ulong Seed, double TotalWager, double TotalPayout,
    double ObservedRtp, double BaseRtp, double FeatureRtp,
    double HitFrequency, double BaseHitFrequency,
    long FeatureTriggers, double FeatureFrequency,
    long FreeSpinsPlayed, double FreeSpinFrequency, double FreeSpinsPerTrigger, double AverageWin,
    double Variance, double StandardDeviation, double VolatilityIndex,
    double MaximumObservedWin, double RtpConfidenceHalfWidth95,
    IReadOnlyList<long> StateVisits, IReadOnlyList<long> FeatureStarts);

public sealed class SimulationRunner
{
    private readonly GameConfiguration _configuration;
    public SimulationRunner(GameConfiguration configuration) => _configuration = configuration;

    public SimulationReport Run(long paidSpins, ulong seed, Action<long>? progress = null)
    {
        if (paidSpins <= 0) throw new ArgumentOutOfRangeException(nameof(paidSpins));
        const double bet = 1.0;
        var engine = new GameEngine(_configuration, new Pcg32Random(seed));
        var state = TensionState.Rest;
        var moments = new OnlineMoments();
        var totalPayout = 0.0;
        var basePayout = 0.0;
        var featurePayout = 0.0;
        var hitCount = 0L;
        var baseHitCount = 0L;
        var featureTriggers = 0L;
        var freeSpins = 0L;
        var winningPayout = 0.0;
        var stateVisits = new long[3];
        var featureStarts = new long[3];
        var reportEvery = Math.Max(1, paidSpins / 100);

        for (var index = 0L; index < paidSpins; index++)
        {
            stateVisits[(int)state]++;
            var cycle = engine.Play(bet, state);
            state = cycle.BaseSpin.StateAfter;
            var award = cycle.TotalAward;
            basePayout += cycle.BaseAward;
            featurePayout += cycle.FeatureAward;
            totalPayout += award;
            moments.Push(award / bet);
            if (cycle.BaseAward > 0) baseHitCount++;
            if (award > 0) { hitCount++; winningPayout += award; }

            if (cycle.Bonus is not null)
            {
                featureTriggers++;
                featureStarts[(int)cycle.Bonus.StartingStage]++;
                freeSpins += cycle.Bonus.SpinsPlayed;
            }
            if (progress is not null && (index + 1) % reportEvery == 0) progress(index + 1);
        }

        var wager = paidSpins * bet;
        return new SimulationReport(
            paidSpins, seed, wager, totalPayout, totalPayout / wager,
            basePayout / wager, featurePayout / wager,
            (double)hitCount / paidSpins, (double)baseHitCount / paidSpins,
            featureTriggers, (double)featureTriggers / paidSpins,
            freeSpins, (double)freeSpins / paidSpins,
            featureTriggers == 0 ? 0 : (double)freeSpins / featureTriggers,
            hitCount == 0 ? 0 : winningPayout / hitCount,
            moments.PopulationVariance, moments.StandardDeviation, moments.StandardDeviation,
            moments.Maximum, moments.MeanConfidenceHalfWidth95,
            stateVisits, featureStarts);
    }
}
