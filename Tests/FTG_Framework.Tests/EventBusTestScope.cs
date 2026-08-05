#nullable enable
using System;
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

[Flags]
internal enum EventBusResidualState
{
    None = 0,
    Subscribers = 1 << 0,
    Queues = 1 << 1,
    PendingReloads = 1 << 2,
    Dispatch = 1 << 3,
    Flags = 1 << 4,
    Recorder = 1 << 5,
    All = Subscribers | Queues | PendingReloads | Dispatch | Flags | Recorder
}

internal sealed class EventBusTestScope : IDisposable
{
    private bool _disposed;
    private readonly EventBusResidualState _allowedResidualState;

    public EventBusTestScope(EventBusResidualState allowedResidualState = EventBusResidualState.None)
    {
        _allowedResidualState = allowedResidualState;
        Baseline = EventBus.Instance.BeginTestScope();
    }

    public EventBusTestDiagnostic Baseline { get; }
    public EventBusTestDiagnostic? Residual { get; private set; }

    public void Dispose()
    {
        if (_disposed)
            return;

        Residual = EventBus.Instance.EndTestScope();
        _disposed = true;
        EventBusResidualState actual = Classify(Residual);
        EventBusResidualState unexpected = actual & ~_allowedResidualState;
        if (unexpected != EventBusResidualState.None)
            throw new InvalidOperationException($"[EventBus] Test leaked unexpected singleton state ({unexpected}); allowed={_allowedResidualState}; diagnostic={Residual}");
    }

    private static EventBusResidualState Classify(EventBusTestDiagnostic diagnostic)
    {
        EventBusResidualState state = EventBusResidualState.None;
        if (diagnostic.SubscriberCount != 0) state |= EventBusResidualState.Subscribers;
        if (diagnostic.CurrentQueueCount != 0 || diagnostic.NextQueueCount != 0) state |= EventBusResidualState.Queues;
        if (diagnostic.PendingReloadCount != 0) state |= EventBusResidualState.PendingReloads;
        if (diagnostic.IsDispatching || diagnostic.DispatchEpoch is not null) state |= EventBusResidualState.Dispatch;
        if (diagnostic.Paused || diagnostic.StepRequested || diagnostic.SuppressFrameAdvanced
            || diagnostic.ReplayApplyActive) state |= EventBusResidualState.Flags;
        if (diagnostic.HasRecorder) state |= EventBusResidualState.Recorder;
        return state;
    }
}
