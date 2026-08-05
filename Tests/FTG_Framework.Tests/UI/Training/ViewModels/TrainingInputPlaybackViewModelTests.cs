#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Input;
using FTG_Framework.UI.Training;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class TrainingInputPlaybackViewModelTests
{
    [Fact]
    public void ViewModel_ListsSelectsAssignsAndConfirmsReplacement()
    {
        var library = new TrainingInputRecordingLibrary();
        var vm = new TrainingInputPlaybackViewModel(library);
        vm.SourcePlayer = 1;
        vm.DummyPlayer = 2;

        Assert.True(vm.AddRecording(Recording("jab", 10), confirmReplace: false));
        Assert.False(vm.AddRecording(Recording("jab", 20), confirmReplace: false));
        Assert.Contains("confirm", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.AddRecording(Recording("jab", 20), confirmReplace: true));
        Assert.True(vm.Select("jab"));
        Assert.True(vm.AssignSelected());

        Assert.Equal(20, library.GetAssigned(2)!.DurationFrames);
        Assert.Equal("jab", vm.SelectedName);
        Assert.Single(vm.Recordings);
    }

    [Fact]
    public void ViewModel_RejectsSameSourceAndDummyWithoutLosingSelection()
    {
        var library = new TrainingInputRecordingLibrary();
        var vm = new TrainingInputPlaybackViewModel(library);
        Assert.True(vm.AddRecording(Recording("safe", 1), false));
        Assert.True(vm.Select("safe"));
        vm.SourcePlayer = 1;
        vm.DummyPlayer = 1;

        Assert.False(vm.AssignSelected());
        Assert.Equal("safe", vm.SelectedName);
        Assert.Contains("different", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_DrivesCaptureFinalizeAssignPlayAndStop()
    {
        int frame = 100;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 9);
        vm.SourcePlayer = 1;
        vm.DummyPlayer = 2;
        Assert.Equal("Start Recording", vm.RecordActionText);
        Assert.True(vm.ToggleCapture("setup"));
        Assert.Equal("Stop Recording", vm.RecordActionText);
        service.AcceptCanonicalInput(1, InputType.Directional, (int)DirectionValue.Down, frame);
        frame = 110;
        Assert.True(vm.ToggleCapture("setup"));
        Assert.Equal("Start Recording", vm.RecordActionText);
        Assert.True(vm.Select("setup"));
        Assert.True(vm.AssignSelected());
        Assert.True(vm.StartPlayback(loop: false));
        Assert.True(vm.IsPlaying);
        vm.StopPlayback();

        Assert.False(vm.IsPlaying);
        Assert.Contains("Stopped", vm.StatusText);
    }

    [Fact]
    public void PanelAndShortcutRoutesShareOneToggleCommandAndPlaybackStopDoesNotStopCapture()
    {
        int frame = 10;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 9);

        Assert.True(TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(vm, "setup"));
        Assert.Equal("Stop Recording", vm.RecordActionText);
        vm.StopPlayback();
        Assert.True(vm.IsCapturing);

        frame = 12;
        int dispatches = 0;
        Assert.True(TrainingShortcutRouter.DispatchMatchedActions(
            new[] { TrainingShortcutRouter.RecordToggleAction },
            command =>
            {
                Assert.Equal(TrainingShortcutCommand.RecordToggle, command);
                dispatches++;
                TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(vm, "setup");
            },
            _ => throw new Xunit.Sdk.XunitException("Unexpected shortcut rejection.")));

        Assert.Equal(1, dispatches);
        Assert.Equal("Start Recording", vm.RecordActionText);
        Assert.Single(library.Recordings);
    }

    [Fact]
    public void ToggleFailureAndRapidPendingTransitionDoNotDoubleAcquireOrFinalize()
    {
        int frame = 0;
        var modes = new FTG_Framework.Core.Replay.PlaybackModeCoordinator();
        modes.Enter(FTG_Framework.Core.Replay.RuntimePlaybackMode.AuthoritativeReplay, 4);
        var blocked = new TrainingInputPlaybackViewModel(
            new TrainingInputRecordingLibrary(),
            new TrainingInputService((_, _, _) => { }, modes), () => frame, () => 4);
        Assert.False(TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(blocked, "blocked"));
        Assert.False(blocked.IsCapturing);

        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 5);
        Assert.True(vm.AddRecording(Recording("same", 1), false));
        Assert.True(vm.Select("same"));
        Assert.True(vm.AssignSelected());
        Assert.True(TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(vm, "same"));
        frame = 1;
        Assert.False(TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(vm, "same"));
        Assert.NotNull(vm.PendingRecording);
        Assert.False(TrainingInputPlaybackPanel.ExecuteRecordToggleCommand(vm, "same"));
        Assert.False(vm.IsCapturing);
        Assert.Same(library.Recordings["same"], library.GetAssigned(2));
    }

    [Fact]
    public void ViewModel_PendingReplacementBlocksCaptureUntilConfirmedOrCanceled()
    {
        int frame = 10;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 3);
        Assert.True(vm.AddRecording(Recording("setup", 1), false));

        Assert.True(vm.ToggleCapture("setup"));
        frame = 12;
        Assert.False(vm.ToggleCapture("setup"));
        Assert.NotNull(vm.PendingRecording);
        Assert.False(vm.ToggleCapture("other"));
        Assert.True(vm.ConfirmPendingOverwrite());

        Assert.Equal(2, library.Recordings["setup"].DurationFrames);
        Assert.Null(vm.PendingRecording);
    }

    [Fact]
    public void ViewModel_CancelPendingOverwritePreservesSelectionAssignmentAndOldContent()
    {
        int frame = 10;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 3);
        var original = Recording("setup", 1);
        Assert.True(vm.AddRecording(original, false));
        Assert.True(vm.Select("setup"));
        Assert.True(vm.AssignSelected());
        Assert.True(vm.ToggleCapture("setup"));
        frame = 12;
        Assert.False(vm.ToggleCapture("setup"));

        vm.CancelPending();

        Assert.Null(vm.PendingRecording);
        Assert.Equal("setup", vm.SelectedName);
        Assert.Same(original, library.Recordings["setup"]);
        Assert.Same(original, library.GetAssigned(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ViewModel_InvalidExplicitStopNameRetainsCapture(string invalidName)
    {
        int frame = 10;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 3);
        Assert.True(vm.ToggleCapture("unused"));
        frame = 12;

        Assert.False(vm.ToggleCapture(invalidName));

        Assert.True(service.IsCapturing);
        Assert.Equal("Stop Recording", vm.RecordActionText);
        Assert.Null(vm.PendingRecording);
        Assert.Empty(library.Recordings);
    }

    [Fact]
    public void ViewModel_OverLimitExplicitStopNameRetainsCapture()
    {
        int frame = 10;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 3);
        Assert.True(vm.ToggleCapture("unused"));
        frame = 12;

        Assert.False(vm.ToggleCapture(new string('x', TrainingInputRecording.MaxNameScalars + 1)));

        Assert.True(service.IsCapturing);
        Assert.Equal("Stop Recording", vm.RecordActionText);
        Assert.Null(vm.PendingRecording);
    }

    [Fact]
    public void ViewModel_ConsumesAutomaticCompletionExactlyOnce()
    {
        int frame = 0;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 7);
        Assert.True(vm.ToggleCapture("limit"));
        frame = TrainingInputRecording.MaxDurationFrames;
        service.CompleteCaptureFrame(frame);

        Assert.True(vm.ConsumeServiceCompletion("limit"));
        Assert.False(vm.ConsumeServiceCompletion("limit"));
        Assert.Equal(TrainingInputRecording.MaxDurationFrames, library.Recordings["limit"].DurationFrames);
        Assert.Contains("limit", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_InvalidNameDoesNotConsumeAutomaticCompletion()
    {
        int frame = 0;
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => frame, () => 7);
        Assert.True(vm.ToggleCapture("unused"));
        frame = TrainingInputRecording.MaxDurationFrames;
        service.CompleteCaptureFrame(frame);

        Assert.False(vm.ConsumeServiceCompletion(""));
        Assert.Contains("limit", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.ConsumeServiceCompletion(""));
        Assert.Contains("limit", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.ConsumeServiceCompletion("limit"));
        Assert.Contains("limit", library.Recordings.Keys);
    }

    [Fact]
    public void ViewModel_IdleCompletionPollingDoesNotValidateOrChangeStatus()
    {
        var vm = new TrainingInputPlaybackViewModel(
            new TrainingInputRecordingLibrary(),
            new TrainingInputService((_, _, _) => { }));

        Assert.False(vm.ConsumeServiceCompletion(""));

        Assert.Equal("Ready", vm.StatusText);
    }

    [Fact]
    public void ViewModel_AddRecordingRejectsReplacementOfActiveTarget()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { });
        var vm = new TrainingInputPlaybackViewModel(library, service, () => 0, () => 7);
        var original = Recording("active", 1);
        Assert.True(vm.AddRecording(original, false));
        Assert.True(vm.Select("active"));
        Assert.True(vm.AssignSelected());
        Assert.True(vm.StartPlayback(loop: true));

        Assert.False(vm.AddRecording(Recording("active", 2), confirmReplace: true));

        Assert.Same(original, library.Recordings["active"]);
        Assert.Contains("stop playback", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_ReportsAuthoritativeReplayOwnershipWithoutLosingAssignment()
    {
        var modes = new FTG_Framework.Core.Replay.PlaybackModeCoordinator();
        modes.Enter(FTG_Framework.Core.Replay.RuntimePlaybackMode.AuthoritativeReplay, 3);
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        var vm = new TrainingInputPlaybackViewModel(library, service, () => 0, () => 3);
        Assert.True(vm.AddRecording(Recording("safe", 1), false));
        Assert.True(vm.Select("safe"));
        Assert.True(vm.AssignSelected());

        Assert.False(vm.StartPlayback(loop: false));

        Assert.Contains("Replay", vm.StatusText);
        Assert.NotNull(library.GetAssigned(2));
    }

    private static TrainingInputRecording Recording(string name, int duration) => new(
        1, 1, duration, name,
        duration == 0 ? [] : [new(0, 0, InputType.Directional, (int)DirectionValue.Neutral)]);
}
