using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SlotGame.Core;
using SlotGame.Simulation;

var options = Arguments.Parse(args);
var configuration = GameConfiguration.LoadDefault();
var theory = new ExactAnalyzer(configuration).Analyze();
var json = new JsonSerializerOptions
{
    WriteIndented = true,
    IncludeFields = true,
    Converters = { new JsonStringEnumConverter() },
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
};

void Save(object payload)
{
    if (options.Output is null) return;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
    File.WriteAllText(options.Output, JsonSerializer.Serialize(payload, json));
    Console.WriteLine($"Report written to {options.Output}");
}

switch (options.Mode)
{
    case Mode.Extremes:
    {
        var extremes = new ExtremeAnalyzer(configuration).Analyze();
        Console.WriteLine($"Reachable base states:     {string.Join(", ", extremes.ReachableBaseStates)}");
        Console.WriteLine($"Exact maximum base award:  {extremes.MaximumBaseAward:N2}x bet");
        Console.WriteLine($"Exact maximum game cycle:  {extremes.MaximumGameCycleAward:N2}x bet");
        foreach (var feature in extremes.MaximumFeatureAwards)
            Console.WriteLine($"Maximum feature from {feature.Key,-9}: {(feature.Value is null ? "unreachable" : $"{feature.Value:N2}x bet")}");
        Save(extremes);
        return;
    }

    case Mode.Sensitivity:
    {
        var rows = SensitivityRunner.Run(configuration, options.Spins, options.Seed);
        Console.WriteLine("Parameter,Level,Exact RTP,Feature frequency,Hit frequency,Std dev,Exact maximum,Maximum observed");
        foreach (var row in rows)
            Console.WriteLine($"{row.Parameter},{row.Level},{row.ExactRtp:P4},{row.ExactFeatureFrequency:P4},{row.SimulatedHitFrequency:P4},{row.SimulatedStandardDeviation:F4},{row.ExactMaximumGameCycleAward:F2},{row.MaximumObservedWin:F2}");
        Save(rows);
        return;
    }

    case Mode.Par:
    {
        var timer = Stopwatch.StartNew();
        Console.WriteLine("Enumerating 40^5 stop combinations for each distinct reel set...");
        var par = new ParAnalyzer(configuration);
        var stats = par.Analyze();
        var extremes = new ExtremeAnalyzer(configuration).Analyze();
        Console.WriteLine($"Done in {timer.Elapsed.TotalSeconds:N1} s.");
        Console.WriteLine($"Exact RTP (analyzer):     {theory.TotalRtp:F15}");
        Console.WriteLine($"Exact RTP (enumeration):  {stats.Rtp:F15}");
        Console.WriteLine($"Variance / SD:            {stats.Variance:F6} / {stats.StandardDeviation:F6}x bet");
        Console.WriteLine($"Volatility index (90%):   {stats.VolatilityIndex90:F4}");
        Console.WriteLine($"Asymptotic variance:      {stats.AsymptoticVariance:F6} ({stats.CorrelationFactor - 1:+0.000%;-0.000%} vs i.i.d.)");
        Console.WriteLine($"Hit frequency:            {stats.HitFrequency:P6} (base {stats.BaseHitFrequency:P6})");
        foreach (var state in stats.States)
            Console.WriteLine($"  {state.State,-9} RTP {state.ExpectedReturn:P4}  value vs Rest {state.RelativeValue:+0.0000;-0.0000} bets");
        var maxBet = (double)configuration.Bets.Max();
        var minBet = (double)configuration.Bets.Min();
        var exploit = theory.WagerWeightedReturn([minBet, maxBet, maxBet]);
        Console.WriteLine($"Bet-switch return (shared state, {minBet:0.00} Rest / {maxBet:0.00} otherwise): {exploit:P4}");

        Save(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            configuration.Version,
            configuration.GameName,
            sharedStripGroups = configuration.SharedStripGroups(),
            exact = new
            {
                theory.BaseRtp, theory.FeatureRtp, theory.TotalRtp, theory.FeatureTriggerProbability, theory.BaseHitProbability,
                stationaryStateProbabilities = theory.StationaryStateProbabilities,
                conditionalStateRtp = theory.ConditionalStateRtp,
                transitionMatrix = ToJagged(theory.BaseTransitionMatrix),
                reelSets = theory.ReelSets,
                features = theory.Features
            },
            statistics = new
            {
                stats.Rtp, stats.Variance, stats.StandardDeviation, stats.VolatilityIndex90, stats.VolatilityIndex95,
                stats.AsymptoticVariance, stats.CorrelationFactor, stats.HitFrequency, stats.BaseHitFrequency,
                states = stats.States, features = stats.Features,
                baseWinDistribution = ReelSetDistribution.BucketLabels.Zip(stats.BaseWinDistribution, (label, p) => new { label, probability = p }),
                confidenceBands = stats.ConfidenceBands,
                sessionRtpFromRest = stats.SessionRtpFromRest.Select(x => new { spins = x.Spins, expectedRtp = x.ExpectedRtp })
            },
            reelSetDistributions = par.Distributions.Values.DistinctBy(d => d.ReelSet).Select(d => new
            {
                d.ReelSet,
                combinations = d.Combinations,
                expectedAward = d.ExpectedAward,
                expectedAwardSquared = d.ExpectedAwardSquared,
                hitProbability = d.HitProbability,
                maximumAward = (double)d.MaximumAwardUnits / d.Scale,
                sparkClassProbabilities = Enumerable.Range(0, 4).Select(d.ClassProbability).ToArray(),
                winBuckets = d.Buckets
            }),
            advantagePlay = new
            {
                minBet, maxBet,
                sharedStateBetSwitchReturn = exploit,
                maxBetOnlyInOverdrive = theory.WagerWeightedReturn([minBet, minBet, maxBet])
            },
            extremes = new
            {
                extremes.ReachableBaseStates, extremes.MaximumBaseAward, extremes.MaximumGameCycleAward,
                extremes.MaximumFeatureAwards
            }
        });
        return;
    }

    case Mode.BetSwitch:
    {
        var reports = new[] { StatePolicy.SharedAcrossBets, StatePolicy.PerBetLevel }
            .Select(policy => BetSwitchRunner.Run(configuration, policy, options.Spins, options.Seed)).ToArray();
        foreach (var report in reports)
            Console.WriteLine($"{report.Policy,-18} return per credit wagered: {report.Return:P4} over {report.PaidSpins:N0} spins");
        Save(new
        {
            theoreticalSharedStateReturn = theory.WagerWeightedReturn([(double)configuration.Bets.Min(), (double)configuration.Bets.Max(), (double)configuration.Bets.Max()]),
            constantWagerRtp = theory.TotalRtp,
            simulations = reports
        });
        return;
    }
}

// Default: exact summary + seeded Monte Carlo with exact-vs-simulated distribution test.
Console.WriteLine($"{configuration.GameName} math model v{configuration.Version}");
Console.WriteLine($"Exact base RTP:       {theory.BaseRtp:P6}");
Console.WriteLine($"Exact feature RTP:    {theory.FeatureRtp:P6}");
Console.WriteLine($"Exact total RTP:      {theory.TotalRtp:P6}");
Console.WriteLine($"Exact feature rate:   {theory.FeatureTriggerProbability:P6} (1 in {1 / theory.FeatureTriggerProbability:N2})");
Console.WriteLine($"Exact base hit rate:  {theory.BaseHitProbability:P6}");
Console.WriteLine($"Stationary states:    {string.Join(" / ", theory.StationaryStateProbabilities.Select(x => x.ToString("P4")))}");
if (options.Mode == Mode.TheoryOnly) return;

Console.WriteLine($"Running {options.Spins:N0} paid spins with seed {options.Seed}...");
var simulation = new SimulationRunner(configuration).Run(options.Spins, options.Seed);
var exactStats = new ParAnalyzer(configuration).Analyze();
var chi = Statistics.ChiSquare(simulation.BaseWinBuckets, exactStats.BaseWinDistribution);
var exactHalfWidth = 1.959964 * Math.Sqrt(exactStats.AsymptoticVariance / options.Spins);
Console.WriteLine($"Observed total RTP:   {simulation.ObservedRtp:P6}");
Console.WriteLine($"  95% CI (i.i.d.):        ± {simulation.RtpConfidenceHalfWidth95Iid:P4}");
Console.WriteLine($"  95% CI (batch means):   ± {simulation.RtpConfidenceHalfWidth95BatchMeans:P4}");
Console.WriteLine($"  95% CI (exact sigma_inf): ± {exactHalfWidth:P4}; z = {(simulation.ObservedRtp - theory.TotalRtp) / (exactHalfWidth / 1.959964):+0.00;-0.00}");
Console.WriteLine($"Hit frequency:        {simulation.HitFrequency:P6} (exact {exactStats.HitFrequency:P6})");
Console.WriteLine($"Std dev:              {simulation.StandardDeviation:N6}x bet (exact {exactStats.StandardDeviation:N6})");
Console.WriteLine($"Feature frequency:    {simulation.FeatureFrequency:P6} (1 in {(simulation.FeatureFrequency == 0 ? 0 : 1 / simulation.FeatureFrequency):N2})");
Console.WriteLine($"Base win distribution chi-square: {chi.Statistic:F2} on {chi.DegreesOfFreedom} df, p = {chi.PValue:F4}");
Console.WriteLine($"Maximum observed:     {simulation.MaximumObservedWin:N2}x bet");

Save(new
{
    generatedAtUtc = DateTimeOffset.UtcNow,
    configuration.Version,
    configuration.GameName,
    exact = new
    {
        theory.BaseRtp, theory.FeatureRtp, theory.TotalRtp, theory.FeatureTriggerProbability, theory.BaseHitProbability,
        stationaryStateProbabilities = theory.StationaryStateProbabilities,
        conditionalStateRtp = theory.ConditionalStateRtp,
        transitionMatrix = ToJagged(theory.BaseTransitionMatrix),
        reelSets = theory.ReelSets,
        features = theory.Features,
        exactStats.Variance, exactStats.StandardDeviation, exactStats.AsymptoticVariance, exactStats.HitFrequency,
        baseWinDistribution = exactStats.BaseWinDistribution,
        rtpHalfWidth95FromAsymptoticVariance = exactHalfWidth
    },
    simulation,
    distributionTest = chi
});

static double[][] ToJagged(double[,] matrix) => Enumerable.Range(0, matrix.GetLength(0))
    .Select(row => Enumerable.Range(0, matrix.GetLength(1)).Select(column => matrix[row, column]).ToArray()).ToArray();

internal enum Mode { Simulate, TheoryOnly, Sensitivity, Extremes, Par, BetSwitch }

internal sealed record Arguments(Mode Mode, long Spins, ulong Seed, string? Output)
{
    public static Arguments Parse(string[] args)
    {
        var mode = Mode.Simulate;
        long spins = 1_000_000;
        ulong seed = 20260908;
        string? output = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--spins": spins = long.Parse(args[++i]); break;
                case "--seed": seed = ulong.Parse(args[++i]); break;
                case "--theory-only": mode = Mode.TheoryOnly; break;
                case "--sensitivity": mode = Mode.Sensitivity; break;
                case "--extremes": mode = Mode.Extremes; break;
                case "--par": mode = Mode.Par; break;
                case "--bet-switch": mode = Mode.BetSwitch; break;
                case "--output": output = args[++i]; break;
                case "--help":
                    Console.WriteLine("""
                        Usage: dotnet run --project src/SlotGame.Simulation -c Release -- [mode] [--spins N] [--seed N] [--output PATH]
                          (default)          exact summary + seeded Monte Carlo, three CIs, chi-square distribution test
                          --theory-only      exact summary only
                          --par              exact PAR statistics: full 40^5 enumeration, variance, VI, sigma_inf
                          --extremes         reachable maximum-exposure analysis
                          --sensitivity      one-at-a-time parameter sweep
                          --bet-switch       adversarial bet-switching player under shared vs per-bet state
                        """);
                    Environment.Exit(0);
                    break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        return new Arguments(mode, spins, seed, output);
    }
}
