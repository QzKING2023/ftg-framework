#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class ReplayIntegrationTests : IDisposable
{
    public void Dispose()
    {
        EventBus.Instance.Recorder = null;
        EventBusTestHelper.Drain();
    }

    private static (StubDataStore data, ComboStateTracker tracker, FrameDataEngine engine, ComboExecutor executor)
        CreateModules()
    {
        var data = new StubDataStore();
        data.SetMove(new FTG_Framework.Data.MoveDefinition
        {
            MoveId = "5LP", Startup = 3, Active = 2, Recovery = 5,
            Damage = 30, HitAdvantage = 3, BlockAdvantage = -2
        });

        var engine = new FrameDataEngine(data);
        engine.Initialize(data);

        var executor = new ComboExecutor(data, engine);
        executor.Initialize(data);

        var tracker = new ComboStateTracker(data);
        tracker.Initialize(data);

        return (data, tracker, engine, executor);
    }

    [Fact]
    public void Record_ThenReplay_3RunsIdentical()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        EventBus.Instance.Recorder = recorder;

        // Simulate 10 frames of gameplay
        for (int frame = 0; frame < 10; frame++)
        {
            if (frame == 1)
                EventBus.Instance.Publish(new InputReceivedEvent(1, frame, 0, 3));
            if (frame == 2)
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30));
            EventBus.Instance.ProcessFrame();
        }

        var replayFile = recorder.Save();
        EventBus.Instance.Recorder = null;
        EventBusTestHelper.Drain();

        Assert.True(replayFile.EventCount > 0);

        // Replay 3 times into fresh ComboStateTracker instances
        var results = new List<(int hitCount, string? moveId, bool active)>();

        for (int run = 0; run < 3; run++)
        {
            var (data, tracker, _, _) = CreateModules();

            var player = new ReplayPlayer();
            player.Load(replayFile);
            EventBus.Instance.SuppressFrameAdvanced = true;

            for (int frame = 0; frame < replayFile.FrameCount; frame++)
            {
                player.ProcessFrameReplay(EventBus.Instance, frame);
                EventBus.Instance.ProcessFrame();
            }

            EventBus.Instance.SuppressFrameAdvanced = false;

            results.Add((tracker.GetHitCount(1), tracker.GetCurrentMoveId(1), tracker.IsActive(1)));

            tracker.Shutdown();
            EventBusTestHelper.Drain();
        }

        var first = results[0];
        for (int i = 1; i < results.Count; i++)
        {
            Assert.Equal(first.hitCount, results[i].hitCount);
            Assert.Equal(first.moveId, results[i].moveId);
            Assert.Equal(first.active, results[i].active);
        }
    }

    [Fact]
    public void ProcessFrame_NullRecorder_WorksNormally()
    {
        EventBus.Instance.Recorder = null;

        // Normal frame processing should work without recorder
        EventBus.Instance.Publish(new InputReceivedEvent(1, 0, 0, 3));
        EventBus.Instance.ProcessFrame();
        EventBus.Instance.ProcessFrame();
    }

    [Fact]
    public void PublishImmediate_WithRecorder_EventsCaptured()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        EventBus.Instance.Recorder = recorder;

        // Simulate rewind during recording
        EventBus.Instance.PublishImmediate(new FrameRewoundEvent(3));
        EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "5LP", 3, 30));

        EventBus.Instance.Recorder = null;
        var file = recorder.Save();

        Assert.True(file.EventCount >= 2);
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void ReplayLifecycle_EventsFireInOrder()
    {
        var events = new List<string>();

        void OnStart(ReplayStartedEvent e) => events.Add("start");
        void OnEnd(ReplayEndedEvent e) => events.Add("end");
        void OnPause(ReplayPausedEvent e) => events.Add($"pause@{e.PausedAtFrame}");

        EventBus.Instance.Subscribe<ReplayStartedEvent>(OnStart);
        EventBus.Instance.Subscribe<ReplayEndedEvent>(OnEnd);
        EventBus.Instance.Subscribe<ReplayPausedEvent>(OnPause);

        try
        {
            // Create a minimal replay file
            var entries = new List<ReplayEntry>
            {
                new(0, nameof(FrameAdvancedEvent), """{"FrameNumber":0}"""),
            };
            var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 0, entries);

            var orchestrator = new ReplayOrchestrator();
            var player = new ReplayPlayer();
            player.Load(file);

            // Manually trigger the lifecycle (same sequence as LoadAndStartReplay + ProcessReplayFrame + Stop)
            EventBus.Instance.Publish(new ReplayStartedEvent(file.FrameCount, file.DataVersion));
            EventBus.Instance.ProcessFrame();

            EventBus.Instance.Publish(new ReplayPausedEvent(0));
            EventBus.Instance.ProcessFrame();

            EventBus.Instance.Publish(new ReplayEndedEvent(1));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(3, events.Count);
            Assert.Equal("start", events[0]);
            Assert.Equal("pause@0", events[1]);
            Assert.Equal("end", events[2]);
        }
        finally
        {
            EventBus.Instance.Unsubscribe<ReplayStartedEvent>(OnStart);
            EventBus.Instance.Unsubscribe<ReplayEndedEvent>(OnEnd);
            EventBus.Instance.Unsubscribe<ReplayPausedEvent>(OnPause);
            EventBusTestHelper.Drain();
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesAllFields()
    {
        var recorder = new ReplayRecorder { IsRecording = true };

        for (int frame = 0; frame < 5; frame++)
        {
            EventBus.Instance.Recorder = recorder;
            EventBus.Instance.Publish(new InputReceivedEvent(1, frame, 0, 3));
            EventBus.Instance.ProcessFrame();
        }

        var original = recorder.Save();
        EventBus.Instance.Recorder = null;
        EventBusTestHelper.Drain();

        // Serialize and deserialize
        var options = new System.Text.Json.JsonSerializerOptions
        {
            TypeInfoResolver = ReplayEventJsonContext.Default,
            PropertyNameCaseInsensitive = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(original, options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ReplayFile>(json, options);
        Assert.NotNull(restored);

        Assert.Equal(original.FrameworkVersion, restored.FrameworkVersion);
        Assert.Equal(original.DataVersion, restored.DataVersion);
        Assert.Equal(original.FrameCount, restored.FrameCount);
        Assert.Equal(original.EventCount, restored.EventCount);
    }

    [Fact]
    public void FrameDataEngine_TryGetSnapshot_AfterUpdate()
    {
        var stubData = new StubDataStore();
        stubData.SetMove(new FTG_Framework.Data.MoveDefinition
        {
            MoveId = "5LP", Startup = 3, Active = 2, Recovery = 5,
            Damage = 30, HitAdvantage = 3, BlockAdvantage = -2
        });

        var engine = new FrameDataEngine(stubData);
        engine.Initialize(stubData);

        engine.StartMove(1, "5LP");
        engine.Update();

        int currentFrame = EventBus.Instance.CurrentFrame;
        var snapshot = engine.TryGetSnapshot(currentFrame);
        Assert.NotNull(snapshot);
        Assert.Equal("5LP", snapshot!.Value.P1MoveId);

        engine.Shutdown();
    }
}
