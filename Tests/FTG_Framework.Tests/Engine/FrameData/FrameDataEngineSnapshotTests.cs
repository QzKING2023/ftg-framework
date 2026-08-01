#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public class FrameDataEngineSnapshotTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Flags);
    private FrameDataEngine? _engine;

    public void Dispose()
    {
        _eventBusScope.Dispose();
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
    // (Converted from the Control-level original — the ViewModel now owns the flow.)
    [Fact]
    public void Rewind_ThenStepForward_StepBackwardStillWorks()
    {
        InitEngine();
        var vm = new PlaybackControlsViewModel { FrameDataEngine = _engine };
        Action<FrameAdvancedEvent> forward = e => vm.OnFrameAdvanced(e.FrameNumber);
        EventBus.Instance.Subscribe(forward);
        try
        {
            int baseFrame = EventBus.Instance.CurrentFrame;
            _engine!.StartMove(1, "5LP");
            for (int i = 0; i < 5; i++)
            {
                _engine.Update();
                RunOneFrame();
            }

            vm.SetPaused(true);

            // Rewind two displayed frames: baseFrame+4 → baseFrame+2.
            vm.StepBackward();
            vm.StepBackward();
            Assert.Equal(baseFrame + 2, vm.DisplayFrame);

            // Replay two frames forward on the new branch.
            _engine.Update();
            RunOneFrame();
            _engine.Update();
            RunOneFrame();
            Assert.Equal(baseFrame + 4, vm.DisplayFrame);

            vm.StepBackward();

            Assert.Equal(baseFrame + 3, vm.DisplayFrame);
            Assert.Equal("5LP", _engine.GetCurrentMoveId(1));
        }
        finally
        {
            EventBus.Instance.Unsubscribe(forward);
        }
    }
}
