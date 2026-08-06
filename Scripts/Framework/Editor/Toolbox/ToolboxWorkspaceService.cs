#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Editor;

/// <summary>
/// Pure-C# toolbox orchestration (S4.3-AC01/AC06/AC07/AC09/AC10/AC12/AC13).
/// Panel instances are registered through <see cref="ToolboxPanelHandle"/>s whose
/// callbacks are supplied by the thin Godot adapter; this service owns no Godot
/// types and is testable under dotnet test.
///
/// Lifecycle/Timing discipline (Epic 4 gate items 1-5):
///  - gate 1: the active lifecycle epoch is captured at first activation
///    (subscription time) and re-validated at every callback (S4.3-AC12);
///    a lifecycle event bumps the epoch at dispatch, so a genuine boundary
///    always arrives with an epoch different from the captured one — the
///    service acts, then re-captures. When the epoch is unchanged the callback
///    is rejected defensively (a stale-epoch callback cannot mutate state).
///  - gate 2: Close invalidates observers before panel instances are released
///    (unsubscribe first, then OnShutdown per panel).
///  - gate 3: Close is exactly-once; a duplicate close is rejected, not ignored.
///  - gate 4: marking the service closed is the first committed release;
///    everything after it is non-fallible by contract.
///  - gate 5: no persistence container is created here — layout is a versioned
///    UI preference handled by ToolboxLayoutService only.
/// </summary>
public sealed class ToolboxWorkspaceService : IDisposable
{
    private readonly object _sync = new();
    private readonly List<ToolboxPanelHandle> _panels = new();
    private readonly Dictionary<ToolboxPanelKind, ToolboxPanelHandle> _byKind = new();
    private ulong _capturedEpoch;
    private bool _lifecycleSubscribed;
    private bool _closed;

    /// <summary>Debug-trace hook (AC09: "debug traces mark the boundary").</summary>
    public event Action<string>? BoundaryTraced;

    public ulong CapturedEpoch => _capturedEpoch;
    public bool IsClosed => _closed;
    public int PanelCount { get { lock (_sync) return _panels.Count; } }
    public IReadOnlyList<ToolboxPanelKind> PanelKinds
    {
        get { lock (_sync) return _panels.Select(panel => panel.Kind).ToArray(); }
    }

    /// <summary>
    /// Registers a hosted panel and, on first activation, subscribes the
    /// workspace's lifecycle observers and captures the active epoch.
    /// </summary>
    public void Activate(ToolboxPanelHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        lock (_sync)
        {
            if (_closed)
                throw new InvalidOperationException("[Toolbox] Workspace is closed; activation is rejected.");
            if (_byKind.ContainsKey(handle.Kind))
                throw new InvalidOperationException($"[Toolbox] Panel kind {handle.Kind} is already activated.");
            _byKind.Add(handle.Kind, handle);
            _panels.Add(handle);
        }
        if (!_lifecycleSubscribed)
        {
            SubscribeLifecycle();
            _capturedEpoch = EventBus.Instance.LifecycleEpoch;
        }
    }

    /// <summary>
    /// Visibility semantics (S4.3-AC06): a hidden panel owns no active UI
    /// callbacks. Suspend/resume transition exactly once per state change.
    /// </summary>
    public void SetPanelVisible(ToolboxPanelKind kind, bool visible)
    {
        ToolboxPanelHandle handle = Find(kind);
        lock (_sync)
        {
            if (visible && handle.IsSuspended)
            {
                handle.IsSuspended = false;
                handle.OnResume?.Invoke();
            }
            else if (!visible && !handle.IsSuspended)
            {
                handle.IsSuspended = true;
                handle.OnSuspend?.Invoke();
            }
        }
    }

    /// <summary>
    /// Editor play-session start (AC09). Traces the boundary; the framework's
    /// own MatchInitializedEvent covers session start from inside the game, so
    /// no revalidation is required from the editor side.
    /// </summary>
    public void OnPlaySessionStarted(string? detail = null)
    {
        if (_closed) return;
        BoundaryTraced?.Invoke($"[Toolbox] play session started{(detail is null ? "" : $" ({detail})")}");
    }

    /// <summary>
    /// Editor play-session end (AC09). The game process is gone and no EventBus
    /// lifecycle event is published editor-side, so this is the boundary where
    /// panels must revalidate against the active committed version.
    /// </summary>
    public void OnPlaySessionEnded(string? detail = null)
    {
        if (_closed) return;
        TraceAndRevalidate($"play session ended{(detail is null ? "" : $" ({detail})")}");
    }

    /// <summary>
    /// Exactly-once close (S4.3-AC07/AC13). Invalidate-before-release:
    /// workspace observers are unsubscribed before panel instances are
    /// released, exactly once. A second close is rejected, not ignored. No
    /// fallible operation occurs after the first committed release.
    /// </summary>
    public void Close()
    {
        bool wasClosed;
        lock (_sync)
        {
            if (_closed)
                throw new InvalidOperationException(
                    "[Toolbox] Close was already performed; duplicate close is rejected.");
            wasClosed = _closed = true; // first committed release — everything after is non-fallible
        }

        if (_lifecycleSubscribed)
            UnsubscribeLifecycle();

        foreach (ToolboxPanelHandle handle in _panels.ToArray())
            handle.OnShutdown?.Invoke();

        lock (_sync)
        {
            _panels.Clear();
            _byKind.Clear();
        }
    }

    public void Dispose() => Close();

    private ToolboxPanelHandle Find(ToolboxPanelKind kind)
    {
        lock (_sync)
        {
            if (_closed)
                throw new InvalidOperationException("[Toolbox] Workspace is closed.");
            return _byKind.TryGetValue(kind, out ToolboxPanelHandle? handle)
                ? handle
                : throw new InvalidOperationException($"[Toolbox] Panel kind {kind} is not activated.");
        }
    }

    private void SubscribeLifecycle()
    {
        EventBus.Instance.Subscribe<MatchInitializedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Subscribe<ReplayStartedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Subscribe<ReplayEndedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Subscribe<StateRestoredEvent>(OnStateRestored);
        _lifecycleSubscribed = true;
    }

    private void UnsubscribeLifecycle()
    {
        EventBus.Instance.Unsubscribe<MatchInitializedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Unsubscribe<ReplayStartedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Unsubscribe<ReplayEndedEvent>(OnLifecycleBoundary);
        EventBus.Instance.Unsubscribe<StateRestoredEvent>(OnStateRestored);
        _lifecycleSubscribed = false;
    }

    // Story 4.2 lesson: a lifecycle event bumps the epoch at dispatch, so the
    // boundary event itself invalidates the captured epoch. Revalidate before
    // acting, then re-capture the current epoch.
    private void OnLifecycleBoundary<T>(T _) where T : struct
    {
        if (_closed) return;
        ulong current = EventBus.Instance.LifecycleEpoch;
        if (current == _capturedEpoch)
            return; // not a genuine new-epoch boundary — reject defensively
        TraceAndRevalidate($"lifecycle boundary (epoch {_capturedEpoch} → {current})");
        _capturedEpoch = current;
    }

    // StateRestoredEvent is not a lifecycle type (it does not bump the epoch),
    // but it is still a revalidation boundary for runtime panels (AC09).
    private void OnStateRestored(StateRestoredEvent _)
    {
        if (_closed) return;
        TraceAndRevalidate("state-restore boundary");
    }

    private void TraceAndRevalidate(string boundary)
    {
        BoundaryTraced?.Invoke($"[Toolbox] {boundary}");
        ToolboxPanelHandle[] snapshot;
        lock (_sync) snapshot = _panels.ToArray();
        foreach (ToolboxPanelHandle handle in snapshot)
            handle.OnRevalidate?.Invoke();
    }
}
