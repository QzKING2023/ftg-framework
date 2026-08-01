#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

[Collection(EventBusTestCollection.Name)]
public class PlaybackControlsViewModelTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Flags);
    public PlaybackControlsViewModelTests()
    {
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    [Fact]
    public void InitialState_ShowsRunningAtFrameZero()
    {
        var vm = new PlaybackControlsViewModel();

        Assert.Equal(0, vm.DisplayFrame);
        Assert.False(vm.Paused);
        Assert.Equal("Frame: 0 [RUNNING]", vm.DisplayText);
    }

    [Fact]
    public void OnFrameAdvanced_UpdatesFrameNumber()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(42);

        Assert.Equal(42, vm.DisplayFrame);
        Assert.Contains("42", vm.DisplayText);
    }

    [Fact]
    public void SetPaused_UpdatesLabel()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(10);
        vm.SetPaused(true);

        Assert.True(vm.Paused);
        Assert.Equal("Frame: 10 [PAUSED]", vm.DisplayText);
    }

    [Fact]
    public void SetPaused_False_RestoresRunning()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);
        vm.SetPaused(false);

        Assert.False(vm.Paused);
        Assert.Contains("RUNNING", vm.DisplayText);
    }

    [Fact]
    public void Toggle_FlipsPauseState()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);
        Assert.True(vm.Paused);

        vm.SetPaused(false);
        Assert.False(vm.Paused);
    }

    [Fact]
    public void OnStepBackward_UpdatesDisplayFrame()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(10);
        vm.SetPaused(true);
        vm.OnStepBackward(9);

        Assert.Equal(9, vm.DisplayFrame);
    }

    [Fact]
    public void CanStepBackward_AtFrameZero_ReturnsFalse()
    {
        var vm = new PlaybackControlsViewModel();

        Assert.False(vm.CanStepBackward(-1));
    }

    [Fact]
    public void CanStepBackward_BeforeEarliestSnapshot_ReturnsFalse()
    {
        var vm = new PlaybackControlsViewModel();
        vm.OnFrameAdvanced(5);

        Assert.False(vm.CanStepBackward(5));
    }

    [Fact]
    public void CanStepBackward_AtOrAfterEarliestSnapshot_ReturnsTrue()
    {
        var vm = new PlaybackControlsViewModel();
        vm.OnFrameAdvanced(5);

        Assert.True(vm.CanStepBackward(4));
        Assert.False(vm.CanStepBackward(5));
        Assert.True(vm.CanStepBackward(-1));
    }

    // --- EventBus synchronization (converted from Control-level tests) ---

    [Fact]
    public void SetPaused_True_SetsEventBusPausedAndLabel()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);

        Assert.True(EventBus.Instance.Paused);
        Assert.Contains("PAUSED", vm.DisplayText);
    }

    [Fact]
    public void SetPaused_Resume_ClearsEventBusPaused()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);
        vm.SetPaused(false);

        Assert.False(EventBus.Instance.Paused);
        Assert.Contains("RUNNING", vm.DisplayText);
    }

    [Fact]
    public void StepForward_SetsStepRequestedAndAutoPauses()
    {
        var vm = new PlaybackControlsViewModel();

        Assert.False(EventBus.Instance.Paused);
        vm.StepForward();

        Assert.True(EventBus.Instance.Paused);
        Assert.True(EventBus.Instance.StepRequested);
    }

    [Fact]
    public void StepForward_IncrementsDisplayFrameAfterProcessFrame()
    {
        var vm = new PlaybackControlsViewModel();
        Action<FrameAdvancedEvent> forward = e => vm.OnFrameAdvanced(e.FrameNumber);
        EventBus.Instance.Subscribe(forward);
        try
        {
            // The bus frame counter is process-global across tests, so the display
            // syncs to it on the first processed frame. From there, each step must
            // advance the display by exactly one (AC 2).
            vm.StepForward();
            EventBus.Instance.ProcessFrame();
            int before = vm.DisplayFrame;

            EventBus.Instance.StepRequested = true;
            EventBus.Instance.ProcessFrame();

            Assert.Equal(before + 1, vm.DisplayFrame);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(forward);
        }
    }

    // --- StepBackward with engine (converted from Control-level tests) ---

    private sealed class FakeFrameDataEngine : IFrameDataEngine
    {
        public int EarliestSnapshotFrame { get; set; }
        public int RestoreCalls;
        public int LastRestoredFrame = -1;
        public bool RestoreResult = true;

        public void StartMove(int playerId, string moveId) { }
        public void Update() { }
        public EvaluatedMoveFrame GetLastEvaluatedFrame(int playerId) => EvaluatedMoveFrame.Idle;
        public MovePhase GetPhase(int playerId) => MovePhase.Idle;
        public int GetCurrentFrame(int playerId) => 0;
        public string? GetCurrentMoveId(int playerId) => null;
        public bool RestoreFrame(int frameNumber)
        {
            RestoreCalls++;
            LastRestoredFrame = frameNumber;
            return RestoreResult;
        }
        public void InterruptAndStart(int playerId, string moveId) { }
        public FrameStateSnapshot? TryGetSnapshot(int frameNumber) => null;
        public void RestoreFromReplaySnapshot(FrameStateSnapshot snapshot) { }
    }

    [Fact]
    public void StepBackward_WithEngine_RestoresPreviousFrameAndDecrements()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine();
        vm.FrameDataEngine = engine;
        vm.OnFrameAdvanced(5);

        vm.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(4, engine.LastRestoredFrame);
        Assert.Equal(4, vm.DisplayFrame);
    }

    // At frame 0 there is nothing to step back to.
    [Fact]
    public void StepBackward_AtFrameZero_DoesNothing()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine();
        vm.FrameDataEngine = engine;

        vm.StepBackward();

        Assert.Equal(0, engine.RestoreCalls);
        Assert.Equal(0, vm.DisplayFrame);
    }

    // The re-derived guard allows frame 1 → 0.
    [Fact]
    public void StepBackward_FromFrameOne_ReachesFrameZero()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine();
        vm.FrameDataEngine = engine;
        vm.OnFrameAdvanced(1);

        vm.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(0, engine.LastRestoredFrame);
        Assert.Equal(0, vm.DisplayFrame);
    }

    [Fact]
    public void StepBackward_WithoutEngine_DoesNothing()
    {
        var vm = new PlaybackControlsViewModel();
        vm.OnFrameAdvanced(5);

        vm.StepBackward();

        Assert.Equal(5, vm.DisplayFrame);
    }

    [Fact]
    public void StepBackward_RestoreFails_DisplayStays()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine { RestoreResult = false };
        vm.FrameDataEngine = engine;
        vm.OnFrameAdvanced(5);

        vm.StepBackward();

        Assert.Equal(1, engine.RestoreCalls);
        Assert.Equal(5, vm.DisplayFrame);
    }

    [Fact]
    public void StepBackward_WhileRunning_AutoPauses()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine();
        vm.FrameDataEngine = engine;
        vm.OnFrameAdvanced(5);

        Assert.False(vm.Paused);
        vm.StepBackward();

        Assert.True(vm.Paused);
        Assert.True(EventBus.Instance.Paused);
    }

    // --- DisplayFrameLimit synchronization ---

    [Fact]
    public void SetPaused_False_RaisesLimitReset()
    {
        var vm = new PlaybackControlsViewModel();
        int? raised = null;
        vm.DisplayFrameLimitChanged = limit => raised = limit;

        vm.SetPaused(true);
        vm.SetPaused(false);

        Assert.Equal(int.MaxValue, raised);
    }

    [Fact]
    public void OnFrameAdvanced_WhenPaused_RaisesDisplayFrameAsLimit()
    {
        var vm = new PlaybackControlsViewModel();
        int? raised = null;
        vm.DisplayFrameLimitChanged = limit => raised = limit;

        vm.SetPaused(true);
        vm.OnFrameAdvanced(7);

        Assert.Equal(7, raised);
    }

    [Fact]
    public void StepBackward_RaisesNewFrameAsLimit()
    {
        var vm = new PlaybackControlsViewModel();
        var engine = new FakeFrameDataEngine();
        vm.FrameDataEngine = engine;
        int? raised = null;
        vm.DisplayFrameLimitChanged = limit => raised = limit;

        vm.OnFrameAdvanced(5);
        vm.StepBackward();

        Assert.Equal(4, raised);
    }

    [Fact]
    public void OnExitTree_WhenPaused_ResetsBusAndRaisesLimitReset()
    {
        var vm = new PlaybackControlsViewModel();
        int? raised = null;
        vm.DisplayFrameLimitChanged = limit => raised = limit;

        vm.SetPaused(true);
        EventBus.Instance.StepRequested = true;

        vm.OnExitTree();

        Assert.False(EventBus.Instance.Paused);
        Assert.False(EventBus.Instance.StepRequested);
        Assert.False(vm.Paused);
        Assert.Equal(int.MaxValue, raised);
    }

    [Fact]
    public void OnExitTree_WhenRunning_NoOp()
    {
        var vm = new PlaybackControlsViewModel();
        int? raised = null;
        vm.DisplayFrameLimitChanged = limit => raised = limit;

        vm.OnExitTree();

        Assert.Null(raised);
        Assert.False(vm.Paused);
    }
}
