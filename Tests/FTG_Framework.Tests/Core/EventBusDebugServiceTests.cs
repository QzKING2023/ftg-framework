#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class EventBusDebugServiceTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private static HitConnectedEvent MakeHit(string moveId = "5A", int frame = 5)
        => new(AttackerId: 1, DefenderId: 2, MoveId: moveId, HitAdvantage: 3, Damage: 50);

    private static MoveBlockedEvent MakeBlock(string moveId = "5B", int frame = 6)
        => new(AttackerId: 1, DefenderId: 2, MoveId: moveId, BlockAdvantage: -2, Damage: 30);

    [Fact]
    public void Enable_SubscribesToEvents()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            EventBus.Instance.Publish(MakeHit());
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            var hitEntry = entries.ShouldContain(e => e.EventTypeName == nameof(HitConnectedEvent));
            Assert.True(hitEntry.TotalOccurrences > 0);
            Assert.True(hitEntry.LastFrameSeen >= 0);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void Disable_Unsubscribes_NoFurtherTracking()
    {
        var service = new EventBusDebugService();
        service.Enable();
        service.Disable();

        Assert.False(service.Enabled);

        EventBus.Instance.Publish(MakeHit());
        EventBus.Instance.ProcessFrame();

        var entries = service.GetEntries();
        Assert.Empty(entries);
    }

    [Fact]
    public void Enable_SetsEnabledFlag()
    {
        var service = new EventBusDebugService();
        Assert.False(service.Enabled);

        service.Enable();
        try
        {
            Assert.True(service.Enabled);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void MultipleEventTypes_TrackedIndependently()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            EventBus.Instance.Publish(MakeHit());
            EventBus.Instance.Publish(MakeBlock());
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            Assert.Contains(entries, e => e.EventTypeName == nameof(HitConnectedEvent));
            Assert.Contains(entries, e => e.EventTypeName == nameof(MoveBlockedEvent));
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void MultipleFirings_IncrementTotalOccurrences()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                EventBus.Instance.Publish(MakeHit(frame: 5 + i));
                EventBus.Instance.ProcessFrame();
            }

            var entries = service.GetEntries();
            var hitEntry = entries.ShouldContain(e => e.EventTypeName == nameof(HitConnectedEvent));
            Assert.Equal(3, hitEntry.TotalOccurrences);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void FrameAdvanced_RateLimited_PayloadUpdate()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            string firstSnapshot = "";
            for (int i = 0; i < 30; i++)
            {
                EventBus.Instance.ProcessFrame();
                var entries = service.GetEntries();
                var faEntry = entries.ShouldContain(e => e.EventTypeName == nameof(FrameAdvancedEvent));
                if (firstSnapshot.Length == 0 && faEntry.LastPayloadSnapshot.Length > 0)
                    firstSnapshot = faEntry.LastPayloadSnapshot;
            }

            var finalEntries = service.GetEntries();
            var finalFaEntry = finalEntries.ShouldContain(e => e.EventTypeName == nameof(FrameAdvancedEvent));
            Assert.Equal(30, finalFaEntry.TotalOccurrences);
            Assert.NotEmpty(firstSnapshot);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void GetEntries_ReturnsRecencyOrder()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            EventBus.Instance.Publish(MakeHit(frame: 10));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(MakeBlock(frame: 11));
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            Assert.True(entries[0].LastFrameSeen >= entries[^1].LastFrameSeen);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void Enable_Idempotent_NoDoubleSubscription()
    {
        var service = new EventBusDebugService();
        service.Enable();
        service.Enable();
        try
        {
            EventBus.Instance.Publish(MakeHit());
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            var hitEntry = entries.ShouldContain(e => e.EventTypeName == nameof(HitConnectedEvent));
            Assert.Equal(1, hitEntry.TotalOccurrences);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void Disable_Idempotent_NoThrow()
    {
        var service = new EventBusDebugService();
        service.Enable();
        service.Disable();
        service.Disable();
        Assert.False(service.Enabled);
    }

    [Fact]
    public void SubscriberCount_ReflectsActualSubscriptions()
    {
        var service = new EventBusDebugService();
        int beforeEnable = EventBus.Instance.GetSubscriberCount<HitConnectedEvent>();

        service.Enable();
        try
        {
            int afterEnable = EventBus.Instance.GetSubscriberCount<HitConnectedEvent>();
            Assert.True(afterEnable > beforeEnable);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void LastPayloadSnapshot_CapturesEventData()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            EventBus.Instance.Publish(MakeHit("5A"));
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            var hitEntry = entries.ShouldContain(e => e.EventTypeName == nameof(HitConnectedEvent));
            Assert.Contains("5A", hitEntry.LastPayloadSnapshot);
            Assert.Contains("AttackerId", hitEntry.LastPayloadSnapshot);
        }
        finally
        {
            service.Disable();
        }
    }

    [Fact]
    public void FrameRewoundEvent_CapturedWhenSubscribed()
    {
        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            EventBus.Instance.PublishImmediate(new FrameRewoundEvent(FrameNumber: 5));

            var entries = service.GetEntries();
            var rewindEntry = entries.ShouldContain(e => e.EventTypeName == nameof(FrameRewoundEvent));
            Assert.True(rewindEntry.TotalOccurrences > 0);
        }
        finally
        {
            service.Disable();
        }
    }
    public void Dispose() => _eventBusScope.Dispose();
}

internal static class TestExtensions
{
    public static T ShouldContain<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        foreach (var item in list)
            if (predicate(item)) return item;
        throw new InvalidOperationException("No matching element found in list.");
    }
}
