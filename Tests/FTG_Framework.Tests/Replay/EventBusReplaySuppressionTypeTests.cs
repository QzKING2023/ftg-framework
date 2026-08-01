#nullable enable
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Replay.Collision;

[Collection(EventBusTestCollection.Name)]
public sealed class EventBusReplaySuppressionTypeTests : System.IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    public void Dispose() => _eventBusScope.Dispose();

    [Fact]
    public void NameCollision_WithObserveOnlyEvent_IsNotSuppressed()
    {
        EventBus.Instance.BeginReplayAuthoritativeApply();
        try { EventBus.Instance.Publish(new StateChangedEvent(7)); }
        finally { EventBus.Instance.EndReplayAuthoritativeApply(); }
        SnapshotAtomicityDiagnostic diagnostic = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
        Assert.Single(diagnostic.Current);
        Assert.Contains(typeof(StateChangedEvent).FullName!, diagnostic.Current[0].PayloadType);
    }
}

public readonly record struct StateChangedEvent(int Value);
