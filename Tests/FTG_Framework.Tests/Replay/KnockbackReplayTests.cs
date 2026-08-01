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
            2, 4, -2, 1.5f, 0.3f, 42, 9, ulong.MaxValue, 12, 13, KnockbackPhase.Completed);
        string json = JsonSerializer.Serialize(expected);
        var actual = JsonSerializer.Deserialize<KnockbackAppliedEvent>(json);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LegacyKnockbackPayload_IsRejectedDuringLoad(int dataVersion)
    {
        const string json =
            """{"PlayerId":2,"HorizontalForce":4,"VerticalForce":-2,"Gravity":1.5,"Friction":0.3}""";
        var player = new ReplayPlayer();
        var error = Assert.Throws<InvalidOperationException>(() => player.Load(new ReplayFile(
            "2.3.0", dataVersion, 1,
            [new ReplayEntry(0, nameof(KnockbackAppliedEvent), json)])));
        Assert.Contains("[Replay]", error.Message);
        Assert.Contains("Phase", error.Message);
        Assert.Contains(dataVersion.ToString(), error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LegacyContainerWithoutKnockback_RemainsReadable(int dataVersion)
    {
        var player = new ReplayPlayer();
        player.Load(new ReplayFile(
            "2.3.0", dataVersion, 1,
            [new ReplayEntry(0, nameof(FrameAdvancedEvent), """{"FrameNumber":0}""")]));
        Assert.Single(player.GetEventsForFrame(0));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Phase\":0}")]
    [InlineData("{\"Phase\":4}")]
    [InlineData("{\"Phase\":\"Started\"}")]
    public void VersionThree_InvalidPhaseShape_IsRejectedDuringLoad(string payload)
    {
        var player = new ReplayPlayer();
        var error = Assert.Throws<InvalidOperationException>(() => player.Load(new ReplayFile(
            "2.3.0", 3, 1,
            [new ReplayEntry(0, nameof(KnockbackAppliedEvent), payload)])));
        Assert.Contains("[Replay]", error.Message);
        Assert.Contains("Phase", error.Message);
    }

    [Theory]
    [InlineData("{\"PlayerId\":2,\"GenerationId\":0,\"WorldX\":1,\"WorldY\":1,\"Phase\":1}")]
    [InlineData("{\"PlayerId\":3,\"GenerationId\":1,\"WorldX\":1,\"WorldY\":1,\"Phase\":1}")]
    [InlineData("{\"PlayerId\":2,\"GenerationId\":-1,\"WorldX\":1,\"WorldY\":1,\"Phase\":1}")]
    [InlineData("{\"PlayerId\":2,\"GenerationId\":1,\"WorldX\":\"NaN\",\"WorldY\":1,\"Phase\":1}")]
    public void VersionThree_InvalidRequiredFields_AreRejectedAtomicallyDuringLoad(string payload)
    {
        var player = new ReplayPlayer();
        Assert.Throws<InvalidOperationException>(() => player.Load(new ReplayFile(
            "2.3.0", 3, 1,
            [new ReplayEntry(0, nameof(KnockbackAppliedEvent), payload)])));
        Assert.Empty(player.GetEventsForFrame(0));
    }

    [Fact]
    public void VersionThree_GoldenJson_PreservesPascalCaseUnsignedGenerationAndNumericPhase()
    {
        var value = new KnockbackAppliedEvent(
            2, 4, -2, 1.5f, 0.3f, 42, 9, ulong.MaxValue, 12, 13, KnockbackPhase.Completed);
        Assert.Equal(
            """{"PlayerId":2,"HorizontalForce":4,"VerticalForce":-2,"Gravity":1.5,"Friction":0.3,"WorldX":42,"WorldY":9,"GenerationId":18446744073709551615,"ContactFrame":12,"FrameNumber":13,"Phase":3}""",
            JsonSerializer.Serialize(value));
    }

    [Fact]
    public void ReplayPlayer_DispatchesRecordedAbsolutePosition()
    {
        var expected = new KnockbackAppliedEvent(
            2, 0, 0, 1.5f, 0.3f, 42, 9, 7, 12, 13, KnockbackPhase.Completed);
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
