#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Combo;

internal sealed class ComboExecutor : IComboExecutor, IModule
{
    private readonly IDataStore _dataStore;
    private readonly IFrameDataEngine _frameDataEngine;
    private readonly ChainValidator _chainValidator;
    private readonly Dictionary<string, GatlingTable> _windowSnapshots = new();
    private readonly Dictionary<int, List<ActiveWindow>> _activeWindows = new();
    private bool _initialized;

    private sealed class ActiveWindow
    {
        public string MoveId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int EndFrame { get; set; }
        public bool Consumed { get; set; }
    }

    public ComboExecutor(IDataStore dataStore, IFrameDataEngine frameDataEngine)
    {
        ArgumentNullException.ThrowIfNull(frameDataEngine);
        _dataStore = dataStore;
        _frameDataEngine = frameDataEngine;
        _chainValidator = new ChainValidator(dataStore);
    }

    public void Initialize(IDataStore dataStore)
    {
        if (_initialized) return;
        _initialized = true;
        EventBus.Instance.Subscribe<CancelWindowEnteredEvent>(OnCancelWindowEntered);
        EventBus.Instance.Subscribe<CancelWindowExitedEvent>(OnCancelWindowExited);
        EventBus.Instance.Subscribe<ComboEndedEvent>(OnComboEnded);
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        FrameworkLog.Info?.Invoke("[Combo] ComboExecutor initialized.");
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<CancelWindowEnteredEvent>(OnCancelWindowEntered);
        EventBus.Instance.Unsubscribe<CancelWindowExitedEvent>(OnCancelWindowExited);
        EventBus.Instance.Unsubscribe<ComboEndedEvent>(OnComboEnded);
        EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        _activeWindows.Clear();
        _windowSnapshots.Clear();
        _chainValidator.Clear();
        _initialized = false;
    }

    private void OnMoveFrameChanged(MoveFrameChangedEvent evt)
    {
        _chainValidator.AddToChainIfEmpty(evt.PlayerId, evt.MoveId);
    }

    private void OnComboEnded(ComboEndedEvent evt)
    {
        _chainValidator.ResetForPlayer(evt.PlayerId);
    }

    private void OnCancelWindowEntered(CancelWindowEnteredEvent evt)
    {
        if (!_activeWindows.TryGetValue(evt.PlayerId, out var windows))
        {
            windows = new List<ActiveWindow>();
            _activeWindows[evt.PlayerId] = windows;
        }

        windows.Add(new ActiveWindow
        {
            MoveId = evt.MoveId,
            Category = evt.Category,
            EndFrame = evt.EndFrame,
            Consumed = false
        });

        var characterId = GetCharacterId(evt.PlayerId);
        if (!_windowSnapshots.ContainsKey(characterId))
            CaptureTableForWindow(characterId);
    }

    private void OnCancelWindowExited(CancelWindowExitedEvent evt)
    {
        if (!_activeWindows.TryGetValue(evt.PlayerId, out var windows))
            return;

        int removed = windows.RemoveAll(w => w.MoveId == evt.MoveId && w.Category == evt.Category && !w.Consumed);
        if (removed > 0)
            FrameworkLog.Info?.Invoke($"[Combo] Cancel window expired unconsumed — P{evt.PlayerId} {evt.MoveId}/{evt.Category}");

        if (windows.Count == 0)
        {
            _activeWindows.Remove(evt.PlayerId);
            var characterId = GetCharacterId(evt.PlayerId);
            ReleaseTableForWindow(characterId);
        }
    }

    public bool TryCancel(int playerId, string candidateMoveId)
    {
        if (candidateMoveId is null)
            return false;

        if (playerId < 1 || playerId > 2)
            return false;

        if (!_activeWindows.TryGetValue(playerId, out var windows) || windows.Count == 0)
            return false;

        var characterId = GetCharacterId(playerId);

        for (int i = windows.Count - 1; i >= 0; i--)
        {
            var window = windows[i];
            if (window.Consumed)
                continue;

            // TryCancel runs before FrameDataEngine.Update dispatches the exit
            // event — without this guard the window stays usable one frame past EndFrame.
            if (_frameDataEngine.GetCurrentFrame(playerId) > window.EndFrame)
                continue;

            if (CanCancel(characterId, window.MoveId, candidateMoveId, window.Category))
            {
                window.Consumed = true;
                windows.RemoveAt(i);

                if (windows.Count == 0)
                {
                    _activeWindows.Remove(playerId);
                    ReleaseTableForWindow(characterId);
                }

                if (!_chainValidator.IsUnique(playerId, candidateMoveId))
                    return false;

                _chainValidator.AddToChain(playerId, candidateMoveId);
                EventBus.Instance.Publish(new MoveCanceledEvent(
                    playerId, window.MoveId, candidateMoveId, window.Category));
                return true;
            }
        }

        return false;
    }

    public bool CanCancel(string characterId, string fromMoveId, string toMoveId, string cancelCategory)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        ArgumentNullException.ThrowIfNull(fromMoveId);
        ArgumentNullException.ThrowIfNull(toMoveId);
        ArgumentNullException.ThrowIfNull(cancelCategory);

        GatlingTable? table;
        if (_windowSnapshots.TryGetValue(characterId, out var snapshot))
            table = snapshot;
        else
            table = _dataStore.GetGatlingTable(characterId);

        if (table is null || table.Entries.Count == 0)
            return false;

        foreach (var entry in table.Entries)
        {
            if (entry.SourceMove == fromMoveId && entry.CancelCategory == cancelCategory)
                return entry.TargetMoves.Contains(toMoveId);
        }

        return false;
    }

    public void CaptureTableForWindow(string characterId)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        var table = _dataStore.GetGatlingTable(characterId);
        if (table is not null)
            _windowSnapshots[characterId] = table;
    }

    public void ReleaseTableForWindow(string characterId)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        _windowSnapshots.Remove(characterId);
    }

    private static string GetCharacterId(int playerId) => playerId switch
    {
        1 => "ryu",
        2 => "ken",
        _ => "unknown"
    };
}
