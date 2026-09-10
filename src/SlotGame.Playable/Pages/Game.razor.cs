using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SlotGame.Core;

namespace SlotGame.Playable.Pages;

public partial class Game : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private readonly GameConfiguration _configuration = GameConfiguration.LoadDefault();
    private readonly TheoreticalMathReport _theory;
    private readonly GameEngine _engine;
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _effects;
    private DotNetObjectReference<Game>? _reference;
    private bool _disposed;
    private bool _reducedMotion;
    private bool _sound;
    private bool _quick;
    private bool _spinning;
    private bool _rolling;
    private int _settled = 5;
    private bool _isBonus;
    private bool _showInspector;
    private bool _showPays;
    private bool _preview;
    private bool _previewComplete;
    private int _freeRemaining;
    private int _freePlayed;
    private SymbolId[,] _grid;
    private SpinResult? _currentSpin;
    private BaseSpinResult? _currentBase;
    private TensionState _state;
    private TensionState _displayStage;
    private double _balance = 1000;
    private double _bet = 1;
    private double _lastAward;
    private double _featureAward;
    private long _sessionSpins;
    private double _sessionWager;
    private double _sessionPayout;
    private readonly Queue<RoundReceipt> _history = new();
    private string _message = "Two Sparks tighten the loom. Three start the feature.";
    private string? _featureBanner;

    public Game()
    {
        _theory = new ExactAnalyzer(_configuration).Analyze();
        _engine = new GameEngine(_configuration, new Pcg32Random((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        // A non-paying display assembled from actual strip windows; no RNG consumed.
        var set = _configuration.BaseReelSet(TensionState.Rest);
        _grid = new SymbolId[3, 5];
        for (var reel = 0; reel < 5; reel++)
        for (var row = 0; row < 3; row++) _grid[row, reel] = set.Reels[reel][reel * 7 + row];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _effects = await JS.InvokeAsync<IJSObjectReference>("import", "./js/cabinet.js");
        _reference = DotNetObjectReference.Create(this);
        _reducedMotion = await _effects.InvokeAsync<bool>("connect", _reference);
    }

    [JSInvokable]
    public Task KeyboardSpin() => InvokeAsync(Spin);

    [JSInvokable]
    public Task CloseRules() => InvokeAsync(async () =>
    {
        _showPays = false;
        if (_effects is not null) await _effects.InvokeVoidAsync("closeDialog");
        StateHasChanged();
    });

    private async Task OpenRules()
    {
        _showPays = true;
        StateHasChanged();
        if (_effects is not null) await _effects.InvokeVoidAsync("openDialog");
    }

    private async Task ToggleSound()
    {
        _sound = !_sound;
        if (_effects is not null) await _effects.InvokeVoidAsync("sound", _sound);
    }

    private Task Pause(int milliseconds) => Task.Delay(
        _reducedMotion ? 30 : _quick ? Math.Max(45, milliseconds / 3) : milliseconds, _lifetime.Token);

    private async Task Cue(string kind)
    {
        if (_effects is not null)
        {
            // Audio is optional feedback; device failures must never interrupt settlement.
            try { await _effects.InvokeVoidAsync("cue", kind); }
            catch (JSException) { _sound = false; }
        }
    }

    // Busy spans the complete paid cycle, including all bonus presentation pauses.
    // Animation state is separate: a stopped reel never unlocks another paid spin.
    private async Task Spin()
    {
        if (_disposed || _spinning || _showPays || _balance < _bet) return;
        _spinning = true;
        _preview = _previewComplete = false;
        _isBonus = false;
        _featureBanner = null;
        _lastAward = _featureAward = 0;
        var bet = _bet;
        var before = _state;
        var cycle = _engine.Play(bet, before);
        _balance -= bet;
        _sessionWager += bet;
        _sessionSpins++;
        try
        {
            _displayStage = before;
            _message = "The shuttle is turning…";
            await Reveal(cycle.BaseSpin.Spin);
            _currentBase = cycle.BaseSpin;
            _lastAward = cycle.BaseAward;
            _message = cycle.BaseSpin.FeatureTriggered
                ? $"{cycle.BaseSpin.Spin.ScatterCount} Sparks · Weave Ascending"
                : DescribeTransition(cycle.BaseSpin);
            if (cycle.BaseAward > 0) await Cue("win");
            _state = cycle.BaseSpin.StateAfter;
            if (cycle.Bonus is not null) await PresentBonus(cycle.Bonus);
            else
            {
                _displayStage = _state;
                if (_state > before) await Cue("rise");
            }
            _lastAward = cycle.TotalAward;
            _balance += cycle.TotalAward;
            _sessionPayout += cycle.TotalAward;
            _history.Enqueue(new RoundReceipt(_sessionSpins, bet, cycle.BaseAward, cycle.FeatureAward, before, _state));
            while (_history.Count > 5) _history.Dequeue();
        }
        catch (OperationCanceledException) when (_disposed) { }
        finally
        {
            _rolling = _spinning = false;
            if (!_disposed) StateHasChanged();
        }
    }

    private async Task Reveal(SpinResult spin)
    {
        _rolling = true;
        _settled = 0;
        _currentSpin = null; // Only resolved outcomes enter Analysis.
        StateHasChanged();
        await Cue("spin");
        await Pause(420);
        for (var reel = 0; reel < 5; reel++)
        {
            for (var row = 0; row < 3; row++) _grid[row, reel] = spin.Grid[row, reel];
            _settled = reel + 1;
            await Cue("stop");
            StateHasChanged();
            if (reel < 4) await Pause(105);
        }
        _currentSpin = spin;
        _rolling = false;
        StateHasChanged();
    }

    private async Task PresentBonus(BonusResult bonus)
    {
        _isBonus = true;
        _freePlayed = 0;
        _freeRemaining = _configuration.InitialFreeSpins;
        _displayStage = bonus.StartingStage;
        _featureBanner = $"Weave Ascending · {bonus.StartingStage} entry";
        await Cue("feature");
        StateHasChanged();
        await Pause(1200);
        foreach (var result in bonus.Spins)
        {
            _freePlayed++;
            _freeRemaining--;
            _displayStage = result.StageBefore;
            // Show only the awarded queue, never the precomputed final duration.
            _featureBanner = $"Free spin {_freePlayed} · {_freeRemaining} queued · {result.Spin.StageMultiplier:0.##}×";
            await Reveal(result.Spin);
            _featureAward += result.Spin.Award;
            _lastAward += result.Spin.Award;
            _freeRemaining = Math.Min(result.SpinsRemainingAfter, _configuration.MaximumFeatureSpins - _freePlayed);
            _displayStage = result.StageAfter;
            _message = result.ExtraSpinAwarded
                ? result.StageBefore == TensionState.Overdrive
                    ? "Overdrive holds · +1 free spin"
                    : $"{result.StageBefore} → {result.StageAfter} · +1 free spin"
                : result.Spin.IsHit ? $"{result.Spin.Wins.Sum(win => win.Ways)} ways at {result.Spin.StageMultiplier:0.##}×" : "No win on this free spin";
            if (result.ExtraSpinAwarded) await Cue("rise");
            else if (result.Spin.IsHit) await Cue("win");
            StateHasChanged();
            await Pause(result.ExtraSpinAwarded ? 1000 : 600);
        }
        _featureBanner = $"{(_preview ? "Preview" : "Feature")} complete · {_featureAward:0.00} credits · {_freePlayed} spins";
        _message = _preview ? "Preview finished. Your balance and paid game state were preserved." : "The loom returns to Rest.";
        _isBonus = false;
        _displayStage = _state;
        await Cue("complete");
    }

    private async Task PreviewFeature()
    {
        if (_disposed || _spinning || _showPays) return;
        _spinning = true;
        _preview = true;
        _previewComplete = false;
        _lastAward = _featureAward = 0;
        _currentBase = null;
        // Separate RNG, fixed replay seed, no wager, no state or session accounting.
        var demonstration = new GameEngine(_configuration, new Pcg32Random(42));
        try
        {
            await PresentBonus(demonstration.PlayBonus(_bet, TensionState.Rest));
            _previewComplete = true;
        }
        catch (OperationCanceledException) when (_disposed) { }
        finally
        {
            _rolling = _spinning = false;
            if (!_disposed) StateHasChanged();
        }
    }

    private void IncreaseBet()
    {
        if (_spinning) return;
        _bet = _configuration.Bets[Math.Min(_configuration.Bets.Count - 1, _configuration.Bets.ToList().IndexOf(_bet) + 1)];
    }
    private void DecreaseBet()
    {
        if (_spinning) return;
        _bet = _configuration.Bets[Math.Max(0, _configuration.Bets.ToList().IndexOf(_bet) - 1)];
    }
    private void ResetCredits()
    {
        if (_spinning) return;
        _balance = 1000;
        _sessionSpins = 0;
        _sessionWager = _sessionPayout = 0;
        _history.Clear();
        _lastAward = 0;
        _message = "Fictional credits reset. A new session begins.";
    }

    private bool IsWinning(int row, int reel) => !_rolling && _currentSpin?.Wins.Any(win =>
        reel < win.ReelsMatched && (_grid[row, reel] == win.Symbol || _grid[row, reel] == SymbolId.Wild)) == true;
    private string CurrentReelSet => _currentSpin?.ReelSet ?? (_rolling ? "Resolving…" : _configuration.BaseReelSet(_state).Name);
    private string StopIndices => _currentSpin is null ? "—" : string.Join(" · ", _currentSpin.Stops.Select(stop => stop.ToString("00")));
    private double NextEntryProbability => _theory.ReelSets[_configuration.BaseReelSet(_state).Name].FeatureTriggerProbability;
    private static string DescribeTransition(BaseSpinResult spin) => spin.StateBefore == spin.StateAfter
        ? spin.Spin.IsHit ? $"{spin.Spin.Wins.Sum(win => win.Ways)} winning ways · {spin.StateAfter}" : $"No win · {spin.StateAfter}"
        : $"{spin.StateBefore} → {spin.StateAfter} · next paid spin";
    private static string ArtClass(SymbolId symbol) => symbol.ToString().ToLowerInvariant();
    private static string SymbolName(SymbolId symbol) => symbol.ToString().ToUpperInvariant();
    private sealed record RoundReceipt(long Number, double Bet, double Base, double Feature, TensionState Before, TensionState After);

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _lifetime.CancelAsync();
        if (_effects is not null)
        {
            try { await _effects.InvokeVoidAsync("disconnect"); await _effects.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
        _reference?.Dispose();
        _lifetime.Dispose();
    }
}
