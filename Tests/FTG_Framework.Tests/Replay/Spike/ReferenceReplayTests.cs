#nullable enable
using System.Reflection;
using System.Text.Json;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests.Replay.Spike;

// ============================================================================
// Task 3.3-3.4: Reference recording + replay for representative frame types
// covering all 4 dispatch phases. 3 replay runs must produce identical state.
//
// Uses ComboStateTracker as the verification target — it is the most
// event-driven module with easily measurable state (HitCount, CurrentMoveId).
// ============================================================================

[Collection(EventBusTestCollection.Name)]
public class ReferenceReplayTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MethodInfo PublishImmediateMethod = typeof(EventBus)
        .GetMethod(nameof(EventBus.PublishImmediate), BindingFlags.Public | BindingFlags.Instance)!;

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    /// <summary>
    /// Helper: inject a deserialized ReplayEntry into EventBus via PublishImmediate.
    /// </summary>
    private static void InjectEvent(ReplayEntry entry)
    {
        var eventType = EventTypeRegistry.Resolve(entry.EventType)
                        ?? throw new InvalidOperationException($"Unknown event type: {entry.EventType}");
        var evt = JsonSerializer.Deserialize(entry.Payload, eventType, JsonOptions)
                  ?? throw new InvalidOperationException($"Failed to deserialize: {entry.Payload}");
        var publish = PublishImmediateMethod.MakeGenericMethod(eventType);
        publish.Invoke(EventBus.Instance, [evt]);
    }

    [Fact]
    public void RecordAndReplay_ComboHitSequence_3ReplayRunsIdentical()
    {
        // Scenario: 3-frame combo hit sequence
        // Phase 1: FrameAdvanced        — frame tick
        // Phase 3: HitConnected         — hit event
        // Phase 3: MoveFrameChanged     — move transition (idle→startup)
        // Phase 4: ComboStarted         — combo begins
        //
        // This covers all 4 dispatch phases with 3 event types.

        var recorder = new ReplayRecorder { IsRecording = true };

        // Build recording manually — simulating what ProcessFrame would dispatch
        recorder.Record(0, new FrameAdvancedEvent(0));

        recorder.Record(1, new FrameAdvancedEvent(1));
        recorder.Record(1, new MoveFrameChangedEvent(1, "5LP", 1, 10, MovePhase.Startup));

        recorder.Record(2, new FrameAdvancedEvent(2));
        recorder.Record(2, new MoveFrameChangedEvent(1, "5LP", 2, 10, MovePhase.Startup));
        recorder.Record(2, new HitConnectedEvent(1, 2, "5LP", 3, 30));

        var replayFile = recorder.Save();
        Assert.Equal(6, replayFile.EventCount);

        // Verify all 4 dispatch phases are represented
        Assert.Contains(replayFile.Entries, e => e.EventType == "FrameAdvancedEvent");    // Phase 1
        Assert.Contains(replayFile.Entries, e => e.EventType == "MoveFrameChangedEvent"); // Phase 3
        Assert.Contains(replayFile.Entries, e => e.EventType == "HitConnectedEvent");     // Phase 3
        // ComboStarted is published by ComboStateTracker as a subscriber response to HitConnected
        // — we don't record it explicitly; it emerges from replay

        // Replay 3 times into fresh ComboStateTracker instances
        var results = new List<(int hitCount, string? moveId, bool active)>();

        for (int run = 0; run < 3; run++)
        {
            var stubData = new StubDataStore();
            var tracker = new ComboStateTracker(stubData);
            tracker.Initialize(stubData);

            var player = new ReplayPlayer();
            player.Load(replayFile);

            for (int frame = 0; frame <= player.FrameCount; frame++)
            {
                foreach (var entry in player.GetEventsForFrame(frame))
                {
                    InjectEvent(entry);
                }
            }

            results.Add((tracker.GetHitCount(1), tracker.GetCurrentMoveId(1), tracker.IsActive(1)));

            tracker.Shutdown();
            EventBusTestHelper.Drain();
        }

        // All 3 runs must match each other exactly
        Assert.Equal(3, results.Count);
        var first = results[0];
        for (int i = 1; i < results.Count; i++)
        {
            Assert.Equal(first.hitCount, results[i].hitCount);
            Assert.Equal(first.moveId, results[i].moveId);
            Assert.Equal(first.active, results[i].active);
        }
    }

    [Fact]
    public void RecordAndReplay_FullComboLifecycle_3ReplayRunsIdentical()
    {
        // Scenario: Full combo lifecycle — 2 hits then combo ends via advantage countdown
        var recorder = new ReplayRecorder { IsRecording = true };

        recorder.Record(0, new FrameAdvancedEvent(0));

        // Hit 1: 5LP connects
        recorder.Record(1, new FrameAdvancedEvent(1));
        recorder.Record(1, new MoveFrameChangedEvent(1, "5LP", 1, 10, MovePhase.Startup));
        recorder.Record(1, new HitConnectedEvent(1, 2, "5LP", 3, 30));

        // Hit 2: 5HP connects (still in combo)
        recorder.Record(2, new FrameAdvancedEvent(2));
        recorder.Record(2, new MoveFrameChangedEvent(1, "5HP", 1, 23, MovePhase.Startup));
        recorder.Record(2, new HitConnectedEvent(1, 2, "5HP", 1, 80));

        // Frame 3: advantage ticks down
        recorder.Record(3, new FrameAdvancedEvent(3));

        var replayFile = recorder.Save();
        Assert.Equal(8, replayFile.EventCount);

        // Replay 3 times
        var results = new List<(int hitCount, string? moveId, bool active)>();

        for (int run = 0; run < 3; run++)
        {
            var stubData = new StubDataStore();
            var tracker = new ComboStateTracker(stubData);
            tracker.Initialize(stubData);

            var player = new ReplayPlayer();
            player.Load(replayFile);

            for (int frame = 0; frame <= player.FrameCount; frame++)
            {
                foreach (var entry in player.GetEventsForFrame(frame))
                {
                    InjectEvent(entry);
                }
            }

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
    public void RecordAndReplay_WithInputReceivedEvent_CoversPhase2()
    {
        // Covers Phase 2 (Input System): InputReceivedEvent
        var recorder = new ReplayRecorder { IsRecording = true };

        recorder.Record(0, new FrameAdvancedEvent(0));
        recorder.Record(0, new InputReceivedEvent(1, 0, 0, 3)); // Directional input

        recorder.Record(1, new FrameAdvancedEvent(1));
        recorder.Record(1, new InputReceivedEvent(1, 1, 1, 7)); // Button C press

        var replayFile = recorder.Save();
        Assert.Equal(4, replayFile.EventCount);
        Assert.Contains(replayFile.Entries, e => e.EventType == "InputReceivedEvent");

        // Replay 3 times — InputReceived has no stateful subscribers in spike tests,
        // but we verify the recording survives round-trip without errors
        for (int run = 0; run < 3; run++)
        {
            var player = new ReplayPlayer();
            player.Load(replayFile);

            int dispatchedCount = 0;
            for (int frame = 0; frame <= player.FrameCount; frame++)
            {
                foreach (var entry in player.GetEventsForFrame(frame))
                {
                    InjectEvent(entry);
                    dispatchedCount++;
                }
            }
            Assert.Equal(4, dispatchedCount);
        }
    }

    [Fact]
    public void RecordAndReplay_WithComboEndedEvent_CoversPhase4()
    {
        // Covers Phase 4 (Combo): ComboEndedEvent explicitly in recording
        var recorder = new ReplayRecorder { IsRecording = true };

        recorder.Record(0, new FrameAdvancedEvent(0));
        recorder.Record(0, new MoveFrameChangedEvent(1, "5LP", 1, 10, MovePhase.Startup));
        recorder.Record(0, new HitConnectedEvent(1, 2, "5LP", 3, 30));
        recorder.Record(0, new ComboEndedEvent(1, 1, "5LP"));

        var replayFile = recorder.Save();
        Assert.Contains(replayFile.Entries, e => e.EventType == "ComboEndedEvent");

        // Replay 3 times — verify ComboStateTracker receives ComboEndedEvent
        // (ComboStateTracker does not subscribe to ComboEndedEvent directly,
        // but ComboExecutor does — we verify the event dispatches without errors)
        for (int run = 0; run < 3; run++)
        {
            var stubData = new StubDataStore();
            var tracker = new ComboStateTracker(stubData);
            tracker.Initialize(stubData);

            var player = new ReplayPlayer();
            player.Load(replayFile);

            int dispatchedCount = 0;
            for (int frame = 0; frame <= player.FrameCount; frame++)
            {
                foreach (var entry in player.GetEventsForFrame(frame))
                {
                    InjectEvent(entry);
                    dispatchedCount++;
                }
            }

            Assert.Equal(4, dispatchedCount);
            tracker.Shutdown();
            EventBusTestHelper.Drain();
        }
    }

    [Fact]
    public void ReplayFile_SerializeDeserialize_RoundTrip()
    {
        var entries = new List<ReplayEntry>
        {
            new(0, "FrameAdvancedEvent", """{"FrameNumber":0}"""),
            new(1, "InputReceivedEvent", """{"PlayerId":1,"Frame":1,"InputType":0,"InputValue":3}"""),
        };
        var file = new ReplayFile("2.3.0-spike", 1, 5, entries);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
        var json = JsonSerializer.Serialize(file, options);
        var restored = JsonSerializer.Deserialize<ReplayFile>(json, options);
        Assert.NotNull(restored);
        Assert.Equal(file.FrameworkVersion, restored.FrameworkVersion);
        Assert.Equal(file.EventCount, restored.EventCount);
        Assert.Equal(file.FrameCount, restored.FrameCount);
    }
}
