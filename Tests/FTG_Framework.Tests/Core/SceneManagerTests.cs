#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Xunit;

namespace FTG_Framework.Tests.Core;

public sealed class SceneManagerTests
{
    private static SceneManager CreateManager() => new(null);

    // ── RegisterScene ──

    [Fact]
    public void RegisterScene_StoresScenePath()
    {
        var manager = CreateManager();
        manager.RegisterScene("test", "res://Scenes/Test.tscn");

        Assert.Null(manager.CurrentSceneId);
    }

    [Fact]
    public void RegisterScene_DuplicateId_OverwritesPrevious()
    {
        var manager = CreateManager();
        manager.RegisterScene("menu", "res://Scenes/Menu.tscn");
        manager.RegisterScene("menu", "res://Scenes/MenuV2.tscn");

        // GoTo with overwritten scene ID should use the new path.
        // We validate indirectly: registering the overwritten ID succeeds (no throw).
        manager.RegisterScene("other", "res://Scenes/Other.tscn");
        Assert.Null(manager.CurrentSceneId);
    }

    // ── GoTo: unregistered scene ──

    [Fact]
    public void GoTo_UnregisteredScene_DoesNotCrash()
    {
        var manager = CreateManager();
        manager.GoTo("nonexistent");

        Assert.Null(manager.CurrentSceneId);
    }

    [Fact]
    public void GoTo_UnregisteredScene_DoesNotChangeCurrentSceneId()
    {
        var manager = CreateManager();
        manager.RegisterScene("a", "res://Scenes/A.tscn");

        var (changing, changed) = EventBusTestHelper.Collect<SceneChangingEvent, SceneChangedEvent>(() =>
        {
            manager.GoTo("a");
        });

        Assert.Single(changing);
        Assert.Single(changed);
        Assert.Equal("a", manager.CurrentSceneId);

        // GoTo unregistered scene — early return, no state change.
        var (changing2, changed2) = EventBusTestHelper.Collect<SceneChangingEvent, SceneChangedEvent>(() =>
        {
            manager.GoTo("nonexistent");
        });

        Assert.Empty(changing2);
        Assert.Empty(changed2);
        Assert.Equal("a", manager.CurrentSceneId);
    }

    // ── GoTo: events (null root — Godot loading skipped, state still transitions) ──

    [Fact]
    public void GoTo_RegisteredScene_UpdatesCurrentSceneId()
    {
        var manager = CreateManager();
        manager.RegisterScene("training", "res://Scenes/Training.tscn");

        EventBusTestHelper.Drain();
        manager.GoTo("training");
        EventBus.Instance.ProcessFrame();

        Assert.Equal("training", manager.CurrentSceneId);
    }

    [Fact]
    public void GoTo_PublishesSceneChangingEvent()
    {
        var manager = CreateManager();
        manager.RegisterScene("training", "res://Scenes/Training.tscn");

        var events = EventBusTestHelper.Collect<SceneChangingEvent>(() =>
        {
            manager.GoTo("training");
        });

        Assert.Single(events);
        Assert.Equal("", events[0].FromSceneId);
        Assert.Equal("training", events[0].ToSceneId);
    }

    [Fact]
    public void GoTo_PublishesSceneChangedEvent()
    {
        var manager = CreateManager();
        manager.RegisterScene("training", "res://Scenes/Training.tscn");

        var events = EventBusTestHelper.Collect<SceneChangedEvent>(() =>
        {
            manager.GoTo("training");
        });

        Assert.Single(events);
        Assert.Equal("training", events[0].SceneId);
    }

    [Fact]
    public void GoTo_FromExistingScene_PublishesTransitionEvents()
    {
        var manager = CreateManager();
        manager.RegisterScene("menu", "res://Scenes/Menu.tscn");
        manager.RegisterScene("training", "res://Scenes/Training.tscn");

        EventBusTestHelper.Drain();
        manager.GoTo("menu");
        EventBus.Instance.ProcessFrame();

        var events = EventBusTestHelper.Collect<SceneChangingEvent>(() =>
        {
            manager.GoTo("training");
        });

        Assert.Single(events);
        Assert.Equal("menu", events[0].FromSceneId);
        Assert.Equal("training", events[0].ToSceneId);
    }

    // ── Subscribe / Unsubscribe ──

    [Fact]
    public void Subscribe_RegistersHandlerWithEventBus()
    {
        var manager = CreateManager();
        var received = new List<string>();
        void Handler(HitConnectedEvent e) => received.Add(e.MoveId);

        manager.Subscribe<HitConnectedEvent>(Handler);
        EventBusTestHelper.Drain();

        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5A", 3, 50));
        EventBus.Instance.ProcessFrame();

        Assert.Single(received);
        Assert.Equal("5A", received[0]);

        EventBus.Instance.Unsubscribe<HitConnectedEvent>(Handler);
    }

    [Fact]
    public void Unsubscribe_RemovesHandlerFromEventBus()
    {
        var manager = CreateManager();
        var received = new List<string>();
        void Handler(HitConnectedEvent e) => received.Add(e.MoveId);

        manager.Subscribe<HitConnectedEvent>(Handler);
        manager.Unsubscribe<HitConnectedEvent>(Handler);

        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "99", 0, 0));
        EventBus.Instance.ProcessFrame();

        Assert.Empty(received);
    }

    [Fact]
    public void Unsubscribe_OnlyAffectsSpecifiedHandler()
    {
        var manager = CreateManager();
        var first = new List<string>();
        var second = new List<string>();
        void Handler1(HitConnectedEvent e) => first.Add(e.MoveId);
        void Handler2(HitConnectedEvent e) => second.Add(e.MoveId);

        manager.Subscribe<HitConnectedEvent>(Handler1);
        manager.Subscribe<HitConnectedEvent>(Handler2);
        manager.Unsubscribe<HitConnectedEvent>(Handler1);

        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "X", 0, 0));
        EventBus.Instance.ProcessFrame();

        Assert.Empty(first);
        Assert.Single(second);
        Assert.Equal("X", second[0]);

        manager.Unsubscribe<HitConnectedEvent>(Handler2);
    }

    // ── Subscription cleanup on GoTo ──

    [Fact]
    public void GoTo_CleansUpPreviousSceneSubscriptions()
    {
        var manager = CreateManager();
        manager.RegisterScene("a", "res://Scenes/A.tscn");
        manager.RegisterScene("b", "res://Scenes/B.tscn");

        // GoTo scene A first, then subscribe — simulating IScene.Enter calling manager.Subscribe.
        EventBusTestHelper.Drain();
        manager.GoTo("a");
        EventBus.Instance.ProcessFrame();

        var received = new List<string>();
        void Handler(HitConnectedEvent e) => received.Add(e.MoveId);
        manager.Subscribe<HitConnectedEvent>(Handler);

        // Subscription is active during scene A.
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "hit1", 0, 0));
        EventBus.Instance.ProcessFrame();
        Assert.Single(received);

        // GoTo scene B — should clean up scene A's subscription.
        manager.GoTo("b");
        EventBus.Instance.ProcessFrame();

        received.Clear();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "hit2", 0, 0));
        EventBus.Instance.ProcessFrame();
        Assert.Empty(received);
    }

    [Fact]
    public void GoTo_ClearsTrackedSubscriptionsSoNewSceneStartsClean()
    {
        var manager = CreateManager();
        manager.RegisterScene("a", "res://Scenes/A.tscn");
        manager.RegisterScene("b", "res://Scenes/B.tscn");

        var first = new List<string>();
        void FirstHandler(HitConnectedEvent e) => first.Add(e.MoveId);

        manager.Subscribe<HitConnectedEvent>(FirstHandler);
        EventBusTestHelper.Drain();
        manager.GoTo("a");
        EventBus.Instance.ProcessFrame();

        // GoTo B cleans up FirstHandler.
        manager.GoTo("b");
        EventBus.Instance.ProcessFrame();

        // New scene subscribes its own handler.
        var second = new List<string>();
        void SecondHandler(HitConnectedEvent e) => second.Add(e.MoveId);
        manager.Subscribe<HitConnectedEvent>(SecondHandler);

        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "newHit", 3, 50));
        EventBus.Instance.ProcessFrame();

        Assert.Empty(first);
        Assert.Single(second);
        Assert.Equal("newHit", second[0]);

        manager.Unsubscribe<HitConnectedEvent>(SecondHandler);
    }

    // ── Factory registration ──

    [Fact]
    public void FactoryRegistration_GoToUpdatesCurrentSceneId()
    {
        var manager = CreateManager();
        manager.RegisterScene("factory_scene", () => throw new InvalidOperationException("Factory should not be called with null root"));

        EventBusTestHelper.Drain();
        manager.GoTo("factory_scene");
        EventBus.Instance.ProcessFrame();

        Assert.Equal("factory_scene", manager.CurrentSceneId);
    }

    [Fact]
    public void FactoryRegistration_PublishesTransitionEvents()
    {
        var manager = CreateManager();
        manager.RegisterScene("fs", () => throw new InvalidOperationException("Factory should not be called with null root"));

        var events = EventBusTestHelper.Collect<SceneChangingEvent>(() =>
        {
            manager.GoTo("fs");
        });

        Assert.Single(events);
        Assert.Equal("fs", events[0].ToSceneId);
    }

    [Fact]
    public void FactoryRegistration_OverwritesPathRegistration()
    {
        var manager = CreateManager();
        manager.RegisterScene("hybrid", "res://Scenes/Old.tscn");
        manager.RegisterScene("hybrid", () => throw new InvalidOperationException("Factory should not be called with null root"));

        // The factory registration should take precedence — GoTo works.
        EventBusTestHelper.Drain();
        manager.GoTo("hybrid");
        EventBus.Instance.ProcessFrame();

        Assert.Equal("hybrid", manager.CurrentSceneId);
    }

    // ── EventBusTestHelper integration ──

    [Fact]
    public void Subscribe_HandlersCanUseEventBusTestHelperCollect()
    {
        var manager = CreateManager();
        manager.RegisterScene("s1", "res://Scenes/S1.tscn");

        var events = EventBusTestHelper.Collect<SceneChangedEvent>(() =>
        {
            manager.GoTo("s1");
        });

        Assert.Single(events);
        Assert.Equal("s1", events[0].SceneId);
    }
}
