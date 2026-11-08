using SlotGame.Core;

namespace SlotGame.Simulation;

public sealed record SimulationReport(
    long PaidSpins, ulong Seed, double TotalWager, double TotalPayout,
    double ObservedRtp, double BaseRtp, double FeatureRtp,
    double HitFrequency, double BaseHitFrequency,
    long FeatureTriggers, double FeatureFrequency,
    long FreeSpinsPlayed, double FreeSpinFrequency, double FreeSpinsPerTrigger, double AverageWin,
    double Variance, double StandardDeviation, double VolatilityIndex90,
    double MaximumObservedWin,
    double RtpConfidenceHalfWidth95Iid,
    double RtpConfidenceHalfWidth95BatchMeans,
    int Batches,
    IReadOnlyList<long> StateVisits, IReadOnlyList<long> FeatureStarts,
    IReadOnlyList<long> BaseWinBuckets);

public sealed class SimulationRunner
{
    public const int DefaultBatches = 100;
    private readonly GameConfiguration _configuration;
    public SimulationRunner(GameConfiguration configuration) => _configuration = configuration;

    /// <summary>
    /// Seeded Monte Carlo of the production state loop at a constant 1.00 bet, starting in Rest.
    /// Two confidence intervals are reported: the textbook i.i.d. interval, and a batch-means interval
    /// (spins split into contiguous batches far longer than the chain's memory) that stays valid when
    /// consecutive paid spins are correlated through the persistent state.
    /// </summary>
    public SimulationReport Run(long paidSpins, ulong seed, Action<long>? progress = null, int batches = DefaultBatches)
    {
        if (paidSpins <= 0) throw new ArgumentOutOfRangeException(nameof(paidSpins));
        batches = (int)Math.Clamp(batches, 2, Math.Max(2, paidSpins / 10));
        const decimal bet = 1m;
        var engine = new GameEngine(_configuration, new Pcg32Random(seed));
        var state = TensionState.Rest;
        var moments = new OnlineMoments();
        var batchMeans = new OnlineMoments();
        var batchSize = paidSpins / batches;
        var batchTotal = 0.0;
        var batchCount = 0L;
        decimal totalPayout = 0, basePayout = 0, featurePayout = 0, winningPayout = 0;
        long hitCount = 0, baseHitCount = 0, featureTriggers = 0, freeSpins = 0;
        var stateVisits = new long[3];
        var featureStarts = new long[3];
        var buckets = new long[ReelSetDistribution.BucketEdges.Length];
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
            var x = (double)(award / bet);
            moments.Push(x);
            buckets[Bucket((double)(cycle.BaseAward / bet))]++;
            if (cycle.BaseAward > 0) baseHitCount++;
            if (award > 0) { hitCount++; winningPayout += award; }

            if (cycle.Bonus is not null)
            {
                featureTriggers++;
                featureStarts[(int)cycle.Bonus.StartingStage]++;
                freeSpins += cycle.Bonus.SpinsPlayed;
            }

            batchTotal += x;
            if (++batchCount == batchSize && batchMeans.Count < batches)
            {
                batchMeans.Push(batchTotal / batchSize);
                batchTotal = 0;
                batchCount = 0;
            }
            if (progress is not null && (index + 1) % reportEvery == 0) progress(index + 1);
        }

        var wager = (double)(paidSpins * bet);
        var payout = (double)totalPayout;
        var batchHalfWidth = Statistics.StudentT975(batches - 1) * Math.Sqrt(batchMeans.SampleVariance / batchMeans.Count);
        return new SimulationReport(
            paidSpins, seed, wager, payout, payout / wager,
            (double)basePayout / wager, (double)featurePayout / wager,
            (double)hitCount / paidSpins, (double)baseHitCount / paidSpins,
            featureTriggers, (double)featureTriggers / paidSpins,
            freeSpins, (double)freeSpins / paidSpins,
            featureTriggers == 0 ? 0 : (double)freeSpins / featureTriggers,
            hitCount == 0 ? 0 : (double)winningPayout / hitCount,
            moments.PopulationVariance, moments.StandardDeviation, 1.644854 * moments.StandardDeviation,
            moments.Maximum, moments.MeanConfidenceHalfWidth95, batchHalfWidth, (int)batchMeans.Count,
            stateVisits, featureStarts, buckets);
    }

    public static int Bucket(double awardInBets)
    {
        var edges = ReelSetDistribution.BucketEdges;
        for (var b = edges.Length - 1; b > 0; b--)
            if (awardInBets >= edges[b] - 1e-12) return b;
        return 0;
    }
}

public sealed record BetSwitchReport(StatePolicy Policy, long PaidSpins, double Wagered, double Paid, double Return);

public static class BetSwitchRunner
{
    /// <summary>
    /// Adversarial player: minimum bet while the visible state for the maximum bet is Rest, maximum bet otherwise.
    /// Under <see cref="StatePolicy.SharedAcrossBets"/> tension built at 0.20 is spent at 10.00 (the v1.0 exploit).
    /// Under <see cref="StatePolicy.PerBetLevel"/> the maximum-bet state can only advance through maximum-bet spins.
    /// </summary>
    public static BetSwitchReport Run(GameConfiguration configuration, StatePolicy policy, long spins, ulong seed)
    {
        var engine = new GameEngine(configuration, new Pcg32Random(seed));
        var ledger = new StateLedger(policy);
        var low = configuration.Bets.Min();
        var high = configuration.Bets.Max();
        decimal wagered = 0, paid = 0;
        for (var i = 0L; i < spins; i++)
        {
            var bet = ledger.StateFor(high) == TensionState.Rest ? low : high;
            var cycle = ledger.Play(engine, bet);
            wagered += bet;
            paid += cycle.TotalAward;
        }
        return new BetSwitchReport(policy, spins, (double)wagered, (double)paid, (double)(paid / wagered));
    }
}
