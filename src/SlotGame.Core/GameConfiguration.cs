using System.Text.Json;

namespace SlotGame.Core;

public sealed class Paytable
{
    private readonly IReadOnlyDictionary<SymbolId, IReadOnlyDictionary<int, double>> _pays;
    public Paytable(IReadOnlyDictionary<SymbolId, IReadOnlyDictionary<int, double>> pays) => _pays = pays;
    public IReadOnlyCollection<SymbolId> PayingSymbols => _pays.Keys.ToArray();
    public double Get(SymbolId symbol, int reels) =>
        _pays.TryGetValue(symbol, out var counts) && counts.TryGetValue(reels, out var pay) ? pay : 0;
    public IReadOnlyDictionary<int, double> For(SymbolId symbol) => _pays[symbol];
}

public sealed class GameConfiguration
{
    private static readonly IReadOnlyDictionary<string, SymbolId> Names = new Dictionary<string, SymbolId>(StringComparer.OrdinalIgnoreCase)
    {
        ["CROWN"] = SymbolId.Crown, ["MOTH"] = SymbolId.Moth, ["SHUTTLE"] = SymbolId.Shuttle,
        ["DYE"] = SymbolId.Dye, ["LINEN"] = SymbolId.Linen, ["KNOT"] = SymbolId.Knot,
        ["WILD"] = SymbolId.Wild, ["SPARK"] = SymbolId.Spark
    };

    private GameConfiguration() { }

    public required string Version { get; init; }
    public required string GameName { get; init; }
    public required int Rows { get; init; }
    public required int ReelCount { get; init; }
    public required int InitialFreeSpins { get; init; }
    public required int MaximumFeatureSpins { get; init; }
    public required IReadOnlyList<double> StageMultipliers { get; init; }
    public required IReadOnlyList<double> Bets { get; init; }
    public required Paytable Paytable { get; init; }
    public required IReadOnlyDictionary<string, ReelSet> ReelSets { get; init; }

    public ReelSet BaseReelSet(TensionState state) => ReelSets[$"BASE_{StateName(state)}"];
    public ReelSet BonusReelSet(TensionState state) => ReelSets[$"BONUS_{StateName(state)}"];
    public double StageMultiplier(TensionState state) => StageMultipliers[(int)state];

    public GameConfiguration With(
        IReadOnlyList<double>? stageMultipliers = null,
        int? initialFreeSpins = null,
        int? maximumFeatureSpins = null,
        IReadOnlyDictionary<string, ReelSet>? reelSets = null)
    {
        var copy = new GameConfiguration
        {
            Version = Version,
            GameName = GameName,
            Rows = Rows,
            ReelCount = ReelCount,
            InitialFreeSpins = initialFreeSpins ?? InitialFreeSpins,
            MaximumFeatureSpins = maximumFeatureSpins ?? MaximumFeatureSpins,
            StageMultipliers = stageMultipliers ?? StageMultipliers,
            Bets = Bets,
            Paytable = Paytable,
            ReelSets = reelSets ?? ReelSets
        };
        copy.Validate();
        return copy;
    }

    public static GameConfiguration LoadDefault()
    {
        var assembly = typeof(GameConfiguration).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("game-config.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded game configuration is missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var codeMap = root.GetProperty("symbolCodes").EnumerateObject()
            .ToDictionary(item => item.Name[0], item => ParseSymbol(item.Value.GetString()!));

        var pays = new Dictionary<SymbolId, IReadOnlyDictionary<int, double>>();
        foreach (var symbol in root.GetProperty("paytable").EnumerateObject())
        {
            pays[ParseSymbol(symbol.Name)] = symbol.Value.EnumerateObject()
                .ToDictionary(item => int.Parse(item.Name), item => item.Value.GetDouble());
        }

        var sets = new Dictionary<string, ReelSet>(StringComparer.Ordinal);
        foreach (var set in root.GetProperty("reelSets").EnumerateObject())
        {
            var reels = set.Value.EnumerateArray()
                .Select(strip => new ReelStrip(strip.GetString()!.Select(code => codeMap[code])))
                .ToArray();
            sets[set.Name] = new ReelSet(set.Name, reels);
        }

        var configuration = new GameConfiguration
        {
            Version = root.GetProperty("version").GetString()!,
            GameName = root.GetProperty("gameName").GetString()!,
            Rows = root.GetProperty("rows").GetInt32(),
            ReelCount = root.GetProperty("reels").GetInt32(),
            InitialFreeSpins = root.GetProperty("initialFreeSpins").GetInt32(),
            MaximumFeatureSpins = root.GetProperty("maximumFeatureSpins").GetInt32(),
            StageMultipliers = root.GetProperty("stageMultipliers").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
            Bets = root.GetProperty("bets").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
            Paytable = new Paytable(pays),
            ReelSets = sets
        };
        configuration.Validate();
        return configuration;
    }

    public void Validate()
    {
        if (Rows != 3 || ReelCount != 5 || StageMultipliers.Count != 3)
            throw new InvalidOperationException("Aether Loom configuration must be 5x3 with three stages.");

        string[] expectedNames = ["BASE_REST", "BASE_TAUT", "BASE_OVERDRIVE", "BONUS_REST", "BONUS_TAUT", "BONUS_OVERDRIVE"];
        foreach (var name in expectedNames)
        {
            if (!ReelSets.TryGetValue(name, out var set))
                throw new InvalidOperationException($"Required reel set {name} is missing.");
            if (set.Reels.Any(reel => reel.Length != 40))
                throw new InvalidOperationException($"Every reel in {name} must contain 40 stops.");
            if (set.Reels[0].Stops.Contains(SymbolId.Wild))
                throw new InvalidOperationException($"Reel 1 in {name} cannot contain Wild.");

            foreach (var reel in set.Reels)
            {
                ValidateSpacing(reel, SymbolId.Spark, name);
                ValidateSpacing(reel, SymbolId.Wild, name);
            }
        }

        if (Paytable.PayingSymbols.Any(symbol => symbol is SymbolId.Wild or SymbolId.Spark))
            throw new InvalidOperationException("Wild and Spark cannot have direct pays.");
    }

    private static void ValidateSpacing(ReelStrip reel, SymbolId symbol, string setName)
    {
        var positions = reel.Stops.Select((item, index) => (item, index))
            .Where(x => x.item == symbol).Select(x => x.index).ToArray();
        for (var i = 0; i < positions.Length; i++)
        for (var j = 0; j < i; j++)
        {
            var distance = Math.Abs(positions[i] - positions[j]);
            distance = Math.Min(distance, reel.Length - distance);
            if (distance < 3)
                throw new InvalidOperationException($"{symbol} stops in {setName} must be separated by three circular positions.");
        }
    }

    private static SymbolId ParseSymbol(string name) => Names.TryGetValue(name, out var symbol)
        ? symbol
        : throw new InvalidOperationException($"Unknown symbol name: {name}");

    private static string StateName(TensionState state) => state switch
    {
        TensionState.Rest => "REST",
        TensionState.Taut => "TAUT",
        TensionState.Overdrive => "OVERDRIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
