#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.UI.Training;
using Godot;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

public class PlaybackControlsTests : IDisposable
{
    private readonly List<PlaybackControls> _panels = new();

    private PlaybackControls CreatePanel()
    {
        EventBusTestHelper.Drain();
        var panel = new PlaybackControls();
        panel._Ready();
        _panels.Add(panel);
        return panel;
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
            panel._ExitTree();
        EventBus.Instance.Paused = false;
        EventBus.Instance.StepRequested = false;
        EventBusTestHelper.Drain();
    }

    private static void PublishAndProcess<T>(T evt) where T : struct
    {
        EventBus.Instance.Publish(evt);
        EventBus.Instance.ProcessFrame();
    }

    private static void AdvanceFrames(int count)
    {
        for (int i = 0; i < count; i++)
            EventBus.Instance.ProcessFrame();
    }

    [Fact]
    public void Ready_InitialState_ShowsRunningWithFrameZero()
    {
        var panel = CreatePanel();

        Assert.Equal(0, panel.DisplayFrame);
        Assert.Contains("RUNNING", panel.DisplayText);
        Assert.Contains("Frame: 0", panel.DisplayText);
        Assert.True(panel.IsLabelVisible);
    }

    [Fact]
    public void Pause_SetsEventBusPausedAndLabel()
    {
        var panel = CreatePanel();

        panel.Paused = true;

        Assert.True(EventBus.Instance.Paused);
        Assert.Contains("PAUSED", panel.DisplayText);
    }

    [Fact]
    public void TogglePause_Resume_RestoresRunningState()
    {
        var panel = CreatePanel();

        panel.Paused = true;
        panel.Paused = false;

        Assert.False(EventBus.Instance.Paused);
        Assert.Contains("RUNNING", panel.DisplayText);
    }

    [Fact]
    public void StepForward_SetsStepRequestedAndAutoPauses()
    {
        var panel = CreatePanel();

        Assert.False(EventBus.Instance.Paused);
        panel.StepForward();

        Assert.True(EventBus.Instance.Paused);
        Assert.True(EventBus.Instance.StepRequested);
    }

    [Fact]
    public void StepForward_IncrementsDisplayFrameAfterProcessFrame()
    {
        var panel = CreatePanel();

        // The bus frame counter is process-global across tests, so the display
        // syncs to it on the first processed frame. From there, each step must
        // advance the display by exactly one (AC 2).
        panel.StepForward();
        EventBus.Instance.ProcessFrame();
        int before = panel.DisplayFrame;

        EventBus.Instance.StepRequested = true;
        EventBus.Instance.ProcessFrame();

        Assert.Equal(before + 1, panel.DisplayFrame);
    }

    private sealed class FakeFrameDataEngine : IFrameDataEngine
    {
        public int EarliestSnapshotFrame { get; set; }
        public int RestoreCalls;
        public int LastRestoredFrame = -1;
        public bool RestoreResult = true;

        public void StartMove(int playerId, string moveId) { }
        public void RegisterHit(int attackerId, int defenderId, string moveId, bool isBlocked) { }
        public void Update() { }
        public MovePhase GetPhase(int playerId) => MovePhase.Idle;
        public int GetCurrentFrame(int playerId) => 0;
        public string? GetCurrentMoveId(int playerId) => null;
        public bool RestoreFrame(int frameNumber)
        {
            RestoreCalls++;
            LastRestoredFrame = frameNumber;
            return RestoreResult;
        }
    }

    private static void SetDisplayFrame(PlaybackControls panel, int frame)
    {
        EventBus.Instance.PublishImmediate(new FrameAdvancedEvent(frame));
        Assert.Equal(frame, panel.DisplayFrame);
    }

    // Spec 8.12: StepBackward with an engine restores the previous frame.
    [Fact]
    public void StepBackward_WithEngine_RestoresPreviousFrameAndDecrements()
    {
        var panel = CreatePanel();
        var engine = new FakeFrameDataEngine();
        panel.FrameDataEngine = engine;
        SetDisplayFrame(panel, 5);

        panel.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(4, engine.LastRestoredFrame);
        Assert.Equal(4, panel.DisplayFrame);
    }

    // Spec 8.13 (re-derived guard): at frame 0 there is nothing to step back to.
    [Fact]
    public void StepBackward_AtFrameZero_DoesNothing()
    {
        var panel = CreatePanel();
        var engine = new FakeFrameDataEngine();
        panel.FrameDataEngine = engine;

        panel.StepBackward();

        Assert.Equal(0, engine.RestoreCalls);
        Assert.Equal(0, panel.DisplayFrame);
    }

    // The re-derived guard allows frame 1 → 0 (spec 4.9's `<= 1` was superseded).
    [Fact]
    public void StepBackward_FromFrameOne_ReachesFrameZero()
    {
        var panel = CreatePanel();
        var engine = new FakeFrameDataEngine();
        panel.FrameDataEngine = engine;
        SetDisplayFrame(panel, 1);

        panel.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(0, engine.LastRestoredFrame);
        Assert.Equal(0, panel.DisplayFrame);
    }

    // Spec 8.14: StepBackward without an engine reference does nothing.
    [Fact]
    public void StepBackward_WithoutEngine_DoesNothing()
    {
        var panel = CreatePanel();
        SetDisplayFrame(panel, 5);

        panel.StepBackward();

        Assert.Equal(5, panel.DisplayFrame);
    }

    [Fact]
    public void StepBackward_RestoreFails_DisplayStays()
    {
        var panel = CreatePanel();
        var engine = new FakeFrameDataEngine { RestoreResult = false };
        panel.FrameDataEngine = engine;
        SetDisplayFrame(panel, 5);

        panel.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(5, panel.DisplayFrame);
    }
}

public class FrameDataEngineSnapshotTests : IDisposable
{
    private FrameDataEngine? _engine;

    public void Dispose()
    {
        EventBus.Instance.Paused = false;
        EventBus.Instance.StepRequested = false;
        EventBusTestHelper.Drain();
    }

    private static Data.DataStore MakeDataStore()
    {
        var move = new Data.MoveDefinition
        {
            MoveId = "5LP",
            Startup = 3,
            Active = 2,
            Recovery = 4,
            HitAdvantage = 2,
            BlockAdvantage = -3,
            Damage = 30
        };
        return new Data.DataStore(new[] { move });
    }

    private void InitEngine()
    {
        _engine = new FrameDataEngine(MakeDataStore());
    }

    // Simulates one game frame: GameLoop calls Update() then ProcessFrame().
    // ProcessFrame advances EventBus.CurrentFrame which SaveSnapshot uses.
    private static void RunOneFrame()
    {
        EventBus.Instance.ProcessFrame();
    }

    [Fact]
    public void SaveSnapshot_StoresTimelineStateEachUpdate()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");

        // One frame: Update saves snapshot (at baseFrame), then ProcessFrame advances
        _engine.Update();
        RunOneFrame();

        Assert.Single(_engine.Snapshots);
        var s0 = _engine.Snapshots[0];
        Assert.Equal(baseFrame, s0.Frame);
        Assert.NotNull(s0.P1MoveId);
        Assert.Equal(MovePhase.Startup, s0.P1Phase);
        Assert.Equal(0, s0.P1CurrentFrame);
    }

    [Fact]
    public void SaveSnapshot_TracksPhaseTransitions()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");

        // Advance 3 game frames
        for (int i = 0; i < 3; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        Assert.Equal(3, _engine.Snapshots.Count);

        var s2 = _engine.Snapshots[2];
        Assert.Equal(baseFrame + 2, s2.Frame);
        Assert.Equal(MovePhase.Startup, s2.P1Phase);

        // Advance 5 more
        for (int i = 0; i < 5; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        Assert.Equal(8, _engine.Snapshots.Count);
    }

    [Fact]
    public void RestoreFrame_RestoresBothPlayerTimelines()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");
        _engine.StartMove(2, "5LP");

        for (int i = 0; i < 4; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        _engine.RestoreFrame(baseFrame + 1);

        // Exact-replay restore: the timeline resumes from snapshot baseFrame+2
        // (the post-frame state of baseFrame+1, ready to re-execute baseFrame+2);
        // the panel publish replays snapshot baseFrame+1's displayed state.
        Assert.Equal(MovePhase.Startup, _engine.GetPhase(1));
        Assert.Equal(2, _engine.GetCurrentFrame(1));
        Assert.Equal("5LP", _engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, _engine.GetPhase(2));
    }

    [Fact]
    public void RestoreFrame_PublishesRestoredStateForBothPlayers()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");

        for (int i = 0; i < 3; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            _engine.RestoreFrame(baseFrame + 1);
        });

        // Panel-facing publish replays snapshot baseFrame+1's displayed state:
        // P1 mid-move at frame 1. Idle players are published too, so panels
        // never keep rendering an abandoned future's move after rewind-to-idle.
        Assert.Contains(events, e => e.PlayerId == 1 && e.CurrentFrame == 1 && e.Phase == MovePhase.Startup);
        Assert.Contains(events, e => e.PlayerId == 2 && e.Phase == MovePhase.Idle);
    }

    [Fact]
    public void RestoreFrame_NoMatchingSnapshot_DoesNothing()
    {
        InitEngine();

        var phaseBefore = _engine!.GetPhase(1);
        _engine.RestoreFrame(99999);

        Assert.Equal(phaseBefore, _engine.GetPhase(1));
    }

    [Fact]
    public void SnapshotEviction_RemovesOldestWhenOverCapacity()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.SnapshotCapacity = 5;
        _engine.StartMove(1, "5LP");

        for (int i = 0; i < 10; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        Assert.Equal(5, _engine.Snapshots.Count);

        int firstFrame = _engine.Snapshots[0].Frame;
        Assert.True(firstFrame >= baseFrame + 5);
    }

    [Fact]
    public void EarliestSnapshotFrame_ReturnsFirstSnapshotFrame()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");

        for (int i = 0; i < 5; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        Assert.Equal(baseFrame, _engine.EarliestSnapshotFrame);

        // After reducing capacity, oldest snapshots are evicted
        _engine.SnapshotCapacity = 2;
        _engine.Update();
        RunOneFrame();

        // Only 2 snapshots remain, EarliestSnapshotFrame shifts forward beyond the first snapshots
        Assert.True(_engine.EarliestSnapshotFrame > baseFrame);
    }

    [Fact]
    public void RestoreFrame_RewindsBusFrameCounter()
    {
        InitEngine();

        int baseFrame = EventBus.Instance.CurrentFrame;
        _engine!.StartMove(1, "5LP");

        for (int i = 0; i < 4; i++)
        {
            _engine.Update();
            RunOneFrame();
        }

        _engine.RestoreFrame(baseFrame + 1);

        // Single frame domain: the next processed frame re-executes as baseFrame+2.
        Assert.Equal(baseFrame + 2, EventBus.Instance.CurrentFrame);
        RunOneFrame();
        Assert.Equal(baseFrame + 3, EventBus.Instance.CurrentFrame);
    }

    // Regression for the round-2 review: after rewind + ≥2 forward steps,
    // step-backward must not silently no-op on a divergent frame domain.
    [Fact]
    public void Rewind_ThenStepForward_StepBackwardStillWorks()
    {
        InitEngine();
        var panel = new PlaybackControls { FrameDataEngine = _engine };
        panel._Ready();
        try
        {
            int baseFrame = EventBus.Instance.CurrentFrame;
            _engine!.StartMove(1, "5LP");
            for (int i = 0; i < 5; i++)
            {
                _engine.Update();
                RunOneFrame();
            }

            panel.Paused = true;

            // Rewind two displayed frames: baseFrame+4 → baseFrame+2.
            panel.StepBackward();
            panel.StepBackward();
            Assert.Equal(baseFrame + 2, panel.DisplayFrame);

            // Replay two frames forward on the new branch.
            _engine.Update();
            RunOneFrame();
            _engine.Update();
            RunOneFrame();
            Assert.Equal(baseFrame + 4, panel.DisplayFrame);

            panel.StepBackward();

            Assert.Equal(baseFrame + 3, panel.DisplayFrame);
            Assert.Equal("5LP", _engine.GetCurrentMoveId(1));
        }
        finally
        {
            panel._ExitTree();
        }
    }
}

public class MoveTimelineRestoreTests
{
    private static Data.DataStore MakeDataStore()
    {
        var move = new Data.MoveDefinition
        {
            MoveId = "5LP",
            Startup = 3,
            Active = 2,
            Recovery = 4,
            HitAdvantage = 2,
            BlockAdvantage = -3,
            Damage = 30
        };
        return new Data.DataStore(new[] { move });
    }

    [Fact]
    public void Restore_WithValidMoveId_RestoresCorrectPhaseAndFrame()
    {
        var dataStore = MakeDataStore();
        var timeline = new MoveTimeline { DataStore = dataStore };

        timeline.Restore("5LP", 2, MovePhase.Startup);

        Assert.Equal(MovePhase.Startup, timeline.Phase);
        Assert.Equal(2, timeline.CurrentFrame);
        Assert.Equal("5LP", timeline.MoveId);
        Assert.True(timeline.TotalFrames > 0);
    }

    [Fact]
    public void Restore_WithNullMoveId_ResetsToIdle()
    {
        var timeline = new MoveTimeline();

        timeline.Restore(null, 0, MovePhase.Idle);

        Assert.Equal(MovePhase.Idle, timeline.Phase);
        Assert.Equal(0, timeline.CurrentFrame);
        Assert.Null(timeline.MoveId);
    }

    [Fact]
    public void Restore_WithUnknownMoveId_ResetsToIdle()
    {
        var dataStore = MakeDataStore();
        var timeline = new MoveTimeline { DataStore = dataStore };

        timeline.Restore("nonexistent_move", 5, MovePhase.Active);

        Assert.Equal(MovePhase.Idle, timeline.Phase);
    }
}

public class InputLogDisplayFrameLimitTests : IDisposable
{
    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void DisplayFrameLimit_FiltersEntriesBeyondLimit()
    {
        var log = new InputLog();
        log._Ready();

        // Publish entries at different frames
        EventBusTestHelper.Drain();
        for (int frame = 1; frame <= 5; frame++)
        {
            EventBus.Instance.Publish(new InputReceivedEvent(1, frame, (int)InputType.Directional, 6));
            EventBus.Instance.ProcessFrame();
        }

        log.DisplayFrameLimit = 3;
        log._RefreshDisplay();

        // Entries stamped frames 1..5; limit 3 keeps exactly frames 1,2,3.
        Assert.Equal(3, log.FilteredCount);

        log._ExitTree();
    }

    [Fact]
    public void FrameRewound_TrimsAbandonedFutureEntries()
    {
        var log = new InputLog();
        log._Ready();
        try
        {
            EventBusTestHelper.Drain();
            for (int frame = 1; frame <= 5; frame++)
            {
                EventBus.Instance.Publish(new InputReceivedEvent(1, frame, (int)InputType.Directional, 6));
                EventBus.Instance.ProcessFrame();
            }
            Assert.Equal(5, log.EntryCount);

            EventBus.Instance.PublishImmediate(new FrameRewoundEvent(3));

            // Entries from the abandoned future (frames 4,5) must not linger —
            // they would resurrect on resume when the display limit lifts.
            Assert.Equal(3, log.EntryCount);
            Assert.DoesNotContain(log.Entries, e => e.Frame > 3);
        }
        finally
        {
            log._ExitTree();
        }
    }

    [Fact]
    public void DisplayFrameLimit_DefaultShowsAll()
    {
        var log = new InputLog();
        log._Ready();

        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        EventBus.Instance.ProcessFrame();
        EventBus.Instance.Publish(new InputReceivedEvent(1, 2, (int)InputType.Button, 0));
        EventBus.Instance.ProcessFrame();

        int beforeFilter = log.FilteredCount;

        log.DisplayFrameLimit = int.MaxValue;
        log._RefreshDisplay();

        Assert.Equal(beforeFilter, log.FilteredCount);

        log._ExitTree();
    }
}

public class HitboxOverlayTests
{
    [Fact]
    public void SetHitboxes_AddsBoxes()
    {
        var overlay = new HitboxOverlay();

        var hitboxes = new Rect2[]
        {
            new(10, 20, 30, 40),
            new(50, 60, 70, 80)
        };
        overlay.SetHitboxes(hitboxes);

        Assert.Equal(2, overlay.BoxCount);
    }

    [Fact]
    public void SetHurtboxes_AddsBoxes()
    {
        var overlay = new HitboxOverlay();

        var hurtboxes = new Rect2[]
        {
            new(5, 5, 10, 20)
        };
        overlay.SetHurtboxes(hurtboxes);

        Assert.Equal(1, overlay.BoxCount);
    }

    [Fact]
    public void Clear_RemovesAllBoxes()
    {
        var overlay = new HitboxOverlay();

        overlay.SetHitboxes(new[] { new Rect2(0, 0, 10, 10) });
        overlay.SetHurtboxes(new[] { new Rect2(5, 5, 10, 10) });
        Assert.Equal(2, overlay.BoxCount);

        overlay.Clear();

        Assert.Equal(0, overlay.BoxCount);
    }

    [Fact]
    public void Enabled_False_DoesNotAffectBoxStorage()
    {
        var overlay = new HitboxOverlay { Enabled = false };

        overlay.SetHitboxes(new[] { new Rect2(0, 0, 10, 10) });

        Assert.Equal(1, overlay.BoxCount);
    }
}

public class GameLoopPauseTests : IDisposable
{
    public void Dispose()
    {
        EventBus.Instance.Paused = false;
        EventBus.Instance.StepRequested = false;
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void Paused_ProcessFrameNotCalled()
    {
        EventBusTestHelper.Drain();

        // Drive the shipped gate, not a re-implemented expression: paused with
        // no step request must not process a frame.
        Assert.False(GameLoop.ShouldProcessFrame(paused: true, stepRequested: false));
    }

    [Fact]
    public void StepRequested_BypassesPauseForOneFrame()
    {
        EventBusTestHelper.Drain();

        Assert.True(GameLoop.ShouldProcessFrame(paused: true, stepRequested: true));

        // GameLoop clears StepRequested before the tick, so the bypass lasts
        // exactly one frame — simulate the tick it permits.
        EventBus.Instance.Paused = true;
        EventBus.Instance.StepRequested = false;
        int frameBefore = EventBus.Instance.CurrentFrame;
        EventBus.Instance.ProcessFrame();
        Assert.Equal(frameBefore + 1, EventBus.Instance.CurrentFrame);
    }
}

public class EventBusPauseTests : IDisposable
{
    public void Dispose()
    {
        EventBus.Instance.Paused = false;
        EventBus.Instance.StepRequested = false;
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void ProcessFrame_AlwaysProcessesOneFrame_RegardlessOfPaused()
    {
        EventBusTestHelper.Drain();
        EventBus.Instance.Paused = true;

        int frameBefore = EventBus.Instance.CurrentFrame;

        // ProcessFrame itself ignores Paused — it always processes one frame
        EventBus.Instance.ProcessFrame();

        Assert.Equal(frameBefore + 1, EventBus.Instance.CurrentFrame);
    }
}
