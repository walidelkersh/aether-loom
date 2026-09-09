using System.Text.Json;
using System.Text.Json.Serialization;
using SlotGame.Core;
using SlotGame.Simulation;

var options = Arguments.Parse(args);
var configuration = GameConfiguration.LoadDefault();
var theory = new ExactAnalyzer(configuration).Analyze();

if (options.Fixtures)
{
    var fixtures = from seed in new ulong[] { 1, 42, 20260908 }
                   from state in Enum.GetValues<TensionState>()
                   let spin = new GameEngine(configuration, new Pcg32Random(seed)).PlayBase(1, state)
                   select new
                   {
                       seed,
                       state = state.ToString(),
                       spin.Spin.ReelSet,
                       spin.Spin.Stops,
                       grid = Enumerable.Range(0, 3).Select(row => Enumerable.Range(0, 5).Select(reel => spin.Spin.Grid[row, reel].ToString()).ToArray()).ToArray(),
                       spin.Spin.ScatterCount,
                       award = spin.Spin.Award,
                       stateAfter = spin.StateAfter.ToString(),
                       spin.FeatureTriggered
                   };
    var fixturePath = options.Output ?? "reports/deterministic-fixtures.json";
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fixturePath))!);
    File.WriteAllText(fixturePath, JsonSerializer.Serialize(fixtures, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Deterministic fixtures written to {fixturePath}");
    return;
}

if (options.Extremes)
{
    var extremes = new ExtremeAnalyzer(configuration).Analyze();
    Console.WriteLine($"Exact maximum base award:  {extremes.MaximumBaseAward:N2}x bet");
    Console.WriteLine($"Exact maximum game cycle:  {extremes.MaximumGameCycleAward:N2}x bet");
    foreach (var feature in extremes.MaximumFeatureAwards)
        Console.WriteLine($"Maximum feature from {feature.Key,-9}: {feature.Value:N2}x bet");
    if (options.Output is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
        File.WriteAllText(options.Output, JsonSerializer.Serialize(extremes, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));
    }
    return;
}

if (options.Sensitivity)
{
    var rows = SensitivityRunner.Run(configuration, options.Spins, options.Seed);
    Console.WriteLine("Parameter,Level,Exact RTP,Feature frequency,Hit frequency,Std dev,Exact maximum,Maximum observed");
    foreach (var row in rows)
        Console.WriteLine($"{row.Parameter},{row.Level},{row.ExactRtp:P4},{row.ExactFeatureFrequency:P4},{row.SimulatedHitFrequency:P4},{row.SimulatedStandardDeviation:F4},{row.ExactMaximumGameCycleAward:F2},{row.MaximumObservedWin:F2}");
    if (options.Output is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
        File.WriteAllText(options.Output, JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));
        Console.WriteLine($"Sensitivity report written to {options.Output}");
    }
    return;
}

Console.WriteLine($"{configuration.GameName} math model v{configuration.Version}");
Console.WriteLine($"Exact base RTP:       {theory.BaseRtp:P6}");
Console.WriteLine($"Exact feature RTP:    {theory.FeatureRtp:P6}");
Console.WriteLine($"Exact total RTP:      {theory.TotalRtp:P6}");
Console.WriteLine($"Exact feature rate:   {theory.FeatureTriggerProbability:P6} (1 in {1 / theory.FeatureTriggerProbability:N2})");
Console.WriteLine($"Exact base hit rate:  {theory.BaseHitProbability:P6}");
Console.WriteLine($"Stationary states:    {string.Join(" / ", theory.StationaryStateProbabilities.Select(x => x.ToString("P4")))}");

SimulationReport? simulation = null;
if (!options.TheoryOnly)
{
    Console.WriteLine($"Running {options.Spins:N0} paid spins with seed {options.Seed}...");
    simulation = new SimulationRunner(configuration).Run(options.Spins, options.Seed);
    Console.WriteLine($"Observed total RTP:   {simulation.ObservedRtp:P6} ± {simulation.RtpConfidenceHalfWidth95:P6} (95% CI)");
    Console.WriteLine($"Observed base RTP:    {simulation.BaseRtp:P6}");
    Console.WriteLine($"Observed feature RTP: {simulation.FeatureRtp:P6}");
    Console.WriteLine($"Hit frequency:        {simulation.HitFrequency:P6}");
    Console.WriteLine($"Feature frequency:    {simulation.FeatureFrequency:P6} (1 in {(simulation.FeatureFrequency == 0 ? 0 : 1 / simulation.FeatureFrequency):N2})");
    Console.WriteLine($"Free spins / paid:    {simulation.FreeSpinFrequency:P6}");
    Console.WriteLine($"Std dev / volatility: {simulation.StandardDeviation:N6}x bet");
    Console.WriteLine($"Maximum observed:     {simulation.MaximumObservedWin:N2}x bet");
}

if (options.Output is not null)
{
    var transition = Enumerable.Range(0, 3)
        .Select(row => Enumerable.Range(0, 3).Select(column => theory.BaseTransitionMatrix[row, column]).ToArray()).ToArray();
    var payload = new
    {
        generatedAtUtc = DateTimeOffset.UtcNow,
        configuration.Version,
        configuration.GameName,
        exact = new
        {
            theory.BaseRtp, theory.FeatureRtp, theory.TotalRtp, theory.FeatureTriggerProbability, theory.BaseHitProbability,
            stationaryStateProbabilities = theory.StationaryStateProbabilities,
            transitionMatrix = transition,
            reelSets = theory.ReelSets,
            features = theory.Features
        },
        simulation
    };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
    File.WriteAllText(options.Output, JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    }));
    Console.WriteLine($"Report written to {options.Output}");
}

internal sealed record Arguments(long Spins, ulong Seed, bool TheoryOnly, bool Sensitivity, bool Extremes, bool Fixtures, string? Output)
{
    public static Arguments Parse(string[] args)
    {
        long spins = 1_000_000;
        ulong seed = 20260908;
        var theoryOnly = false;
        var sensitivity = false;
        var extremes = false;
        var fixtures = false;
        string? output = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--spins": spins = long.Parse(args[++i]); break;
                case "--seed": seed = ulong.Parse(args[++i]); break;
                case "--theory-only": theoryOnly = true; break;
                case "--sensitivity": sensitivity = true; break;
                case "--extremes": extremes = true; break;
                case "--fixtures": fixtures = true; break;
                case "--output": output = args[++i]; break;
                case "--help":
                    Console.WriteLine("Usage: dotnet run --project src/SlotGame.Simulation -- [--spins N] [--seed N] [--theory-only] [--sensitivity] [--extremes] [--fixtures] [--output PATH]");
                    Environment.Exit(0);
                    break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        return new Arguments(spins, seed, theoryOnly, sensitivity, extremes, fixtures, output);
    }
}
