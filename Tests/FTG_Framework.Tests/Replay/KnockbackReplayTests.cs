#nullable enable
using System;
using System.Text.Json;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public sealed class KnockbackReplayTests : IDisposable
{
    public KnockbackReplayTests() => EventBusTestHelper.Drain();
    public void Dispose() => EventBusTestHelper.Drain();

    [Fact]
    public void Event_RoundTripsAllAuthoritativeTrajectoryFields()
    {
        var expected = new KnockbackAppliedEvent(
            2, 4, -2, 1.5f, 0.3f, 42, 9, 7, 12, 13, true);
        string json = JsonSerializer.Serialize(expected);
        var actual = JsonSerializer.Deserialize<KnockbackAppliedEvent>(json);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LegacyFiveFieldPayload_RemainsReadableAndInert()
    {
        const string json =
            """{"PlayerId":2,"HorizontalForce":4,"VerticalForce":-2,"Gravity":1.5,"Friction":0.3}""";
        var actual = JsonSerializer.Deserialize<KnockbackAppliedEvent>(json);
        Assert.Equal(2, actual.PlayerId);
        Assert.Null(actual.WorldX);
        Assert.Null(actual.WorldY);
        Assert.False(actual.Completed);
        Assert.Equal(0, actual.GenerationId);
    }

    [Fact]
    public void ReplayPlayer_DispatchesRecordedAbsolutePosition()
    {
        var expected = new KnockbackAppliedEvent(
            2, 0, 0, 1.5f, 0.3f, 42, 9, 7, 12, 13, true);
        string payload = JsonSerializer.Serialize(expected);
        var player = new ReplayPlayer();
        player.Load(new ReplayFile(
            "2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, nameof(KnockbackAppliedEvent), payload)]));
        KnockbackAppliedEvent? observed = null;
        Action<KnockbackAppliedEvent> handler = e => observed = e;
        EventBus.Instance.Subscribe(handler);
        try
        {
            Assert.Equal(1, player.ProcessFrameReplay(EventBus.Instance, 0));
            Assert.Equal(expected, observed);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }
}
