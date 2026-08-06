#nullable enable
using System;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-AC02: read-only selection propagation. The coordination service holds no
/// EventBus or Data references; every propagation path asserts zero Data writes
/// and zero EventBus publishes/injections.
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class ToolboxSelectionTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private readonly EventBus _bus = EventBus.Instance;

    public void Dispose() => _eventBusScope.Dispose();

    [Theory]
    [InlineData(ToolboxSelectionIdentityKind.Move, ToolboxPanelKind.RuntimeTuning, true)]
    [InlineData(ToolboxSelectionIdentityKind.Move, ToolboxPanelKind.MoveAuthoring, false)]
    [InlineData(ToolboxSelectionIdentityKind.Move, ToolboxPanelKind.EventBusDebug, false)]
    [InlineData(ToolboxSelectionIdentityKind.KnockbackProfile, ToolboxPanelKind.RuntimeTuning, false)]
    [InlineData(ToolboxSelectionIdentityKind.PhysicsResponseProfile, ToolboxPanelKind.RuntimeTuning, false)]
    public void ApplicabilityMatrix_ResolvesPerIdentityAndConsumer(
        ToolboxSelectionIdentityKind identity, ToolboxPanelKind consumer, bool expected)
    {
        var selection = new ToolboxSelection(
            ToolboxPanelKind.MoveAuthoring, identity, identity == ToolboxSelectionIdentityKind.Move ? "5A" : "light");

        Assert.Equal(expected, selection.IsApplicableTo(consumer));
    }

    [Fact]
    public void PropagateSelection_DeliversToApplicableSubscribersOnly()
    {
        var service = new ToolboxSelectionService();
        int authoringCalls = 0;
        int tuningCalls = 0;
        service.Subscribe(ToolboxPanelKind.MoveAuthoring, _ => authoringCalls++);
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, _ => tuningCalls++);

        bool moveDelivered = service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A"));
        bool profileDelivered = service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.KnockbackProfile, "light"));

        Assert.True(moveDelivered);
        Assert.False(profileDelivered);
        Assert.Equal(0, authoringCalls); // authoring rejects any selection (read-only contract)
        Assert.Equal(1, tuningCalls);
    }

    [Fact]
    public void PropagateSelection_IdenticalRepeatIsDeduplicated()
    {
        var service = new ToolboxSelectionService();
        int calls = 0;
        int changedEvents = 0;
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, _ => calls++);
        service.SelectionChanged += _ => changedEvents++;
        var selection = new ToolboxSelection(
            ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A");

        Assert.True(service.PropagateSelection(selection));
        Assert.False(service.PropagateSelection(selection));

        Assert.Equal(1, calls);
        Assert.Equal(1, changedEvents);
    }

    [Fact]
    public void PropagateSelection_UpdatesCurrentSelectionAndFiresChange()
    {
        var service = new ToolboxSelectionService();
        ToolboxSelection? observed = null;
        service.SelectionChanged += selection => observed = selection;
        var selection = new ToolboxSelection(
            ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A");

        service.PropagateSelection(selection);

        Assert.Equal(selection, service.CurrentSelection);
        Assert.Equal(selection, observed);
    }

    [Fact]
    public void Unsubscribe_RemovesOnlyTheMatchingSubscriber()
    {
        var service = new ToolboxSelectionService();
        int calls = 0;
        void Handler(ToolboxSelection _) => calls++;
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, Handler);
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, _ => calls++);
        service.Unsubscribe(ToolboxPanelKind.RuntimeTuning, Handler);

        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Propagation_ZeroEventBusPublishOrInjection()
    {
        var service = new ToolboxSelectionService();
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, _ => { });
        int before = _bus.GetSubscriberCount<MatchInitializedEvent>() +
                     _bus.GetSubscriberCount<InputReceivedEvent>() +
                     _bus.GetSubscriberCount<FrameAdvancedEvent>();

        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A"));
        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.KnockbackProfile, "light"));

        // No subscriber registrations, no queued envelopes, no pending reloads:
        // the residual-state scope in Dispose verifies the bus is untouched.
        Assert.Equal(before, _bus.GetSubscriberCount<MatchInitializedEvent>() +
                              _bus.GetSubscriberCount<InputReceivedEvent>() +
                              _bus.GetSubscriberCount<FrameAdvancedEvent>());
    }

    [Fact]
    public void Propagation_ZeroDataMutation()
    {
        var service = new ToolboxSelectionService();
        service.Subscribe(ToolboxPanelKind.RuntimeTuning, _ => { });
        var store = new DataStore(
            [MakeMove("5A"), MakeMove("5B")],
            knockbackProfiles: [MakeKnockbackProfile("light")],
            physicsResponseProfiles: [MakeResponseProfile("default")]);
        var movesBefore = SnapshotMoves(store);
        var knockbackBefore = SnapshotKnockback(store);
        var responseBefore = SnapshotResponse(store);

        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, "5A"));
        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.KnockbackProfile, "light"));
        service.PropagateSelection(
            new ToolboxSelection(ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.PhysicsResponseProfile, "default"));

        Assert.Equal(movesBefore, SnapshotMoves(store));
        Assert.Equal(knockbackBefore, SnapshotKnockback(store));
        Assert.Equal(responseBefore, SnapshotResponse(store));
    }

    private static object[] SnapshotMoves(DataStore store) =>
        store.GetAllMoves().Select(m => new object[]
        {
            m.MoveId, m.Startup, m.Active, m.Recovery, m.Damage, m.ChainRepeatable
        }).ToArray();

    private static object[] SnapshotKnockback(DataStore store) =>
        store.GetAllKnockbackProfiles().Select(p => new object[]
        {
            p.ProfileId, p.Horizontal, p.Vertical, p.Gravity, p.Friction
        }).ToArray();

    private static object[] SnapshotResponse(DataStore store) =>
        store.GetAllPhysicsResponseProfiles().Select(p => new object[]
        {
            p.ProfileId, p.KnockbackMultiplier, p.GravityScale, p.Friction, p.AirFriction
        }).ToArray();

    private static MoveDefinition MakeMove(string moveId) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
        Damage = 10
    };

    private static KnockbackProfile MakeKnockbackProfile(string profileId) => new()
    {
        ProfileId = profileId,
        Horizontal = 8.0f,
        Vertical = 3.0f,
        Gravity = 1.5f,
        Friction = 0.3f
    };

    private static PhysicsResponseProfile MakeResponseProfile(string profileId) => new()
    {
        ProfileId = profileId,
        KnockbackMultiplier = 1.0f,
        GravityScale = 1.0f,
        Friction = 0.5f,
        AirFriction = 0.2f
    };
}
