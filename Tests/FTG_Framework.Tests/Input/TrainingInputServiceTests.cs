#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Input;
using FTG_Framework.Core.Replay;
using Xunit;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public sealed class TrainingInputServiceTests : System.IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();

    public void Dispose() => _eventBusScope.Dispose();
    [Fact]
    public void Capture_UsesRelativeFramesAndExplicitSameFrameSequence()
    {
        var injected = new List<(int Player, InputType Type, int Value)>();
        var service = new TrainingInputService((p, t, v) => injected.Add((p, t, v)));
        Assert.True(service.TryStartCapture(1, 100, out _));

        service.AcceptCanonicalInput(1, InputType.Directional, (int)DirectionValue.Down, 100);
        service.AcceptCanonicalInput(1, InputType.Button, (int)ButtonValue.A, 100);
        service.AcceptCanonicalInput(1, InputType.Directional, (int)DirectionValue.Neutral, 103);
        TrainingInputRecording recording = service.StopCapture(105, "sample");

        Assert.Equal(new[] { 0, 0, 3 }, recording.Entries.Select(e => e.RelativeFrame));
        Assert.Equal(new[] { 0, 1, 0 }, recording.Entries.Select(e => e.WithinFrameSequence));
        Assert.Equal(105 - 100, recording.DurationFrames);
        Assert.Equal(3, injected.Count);
    }

    [Fact]
    public void Capture_LimitFrameCompletesExactlyOnceAfterAllTerminalInputs()
    {
        var live = new List<int>();
        var service = new TrainingInputService((_, _, value) => live.Add(value));
        Assert.True(service.TryStartCapture(1, 100, 3, out _));

        int terminal = 100 + TrainingInputRecording.MaxDurationFrames;
        service.AcceptCanonicalInput(1, InputType.Directional, (int)DirectionValue.Forward, terminal);
        service.AcceptCanonicalInput(1, InputType.Button, (int)ButtonValue.A, terminal);
        service.CompleteCaptureFrame(terminal);
        service.CompleteCaptureFrame(terminal);

        Assert.False(service.IsCapturing);
        Assert.True(service.TryTakeCompletedCapture(out TrainingInputCaptureCompletion completion));
        Assert.Equal(TrainingInputCaptureCompletionReason.DurationLimit, completion.Reason);
        Assert.Equal(TrainingInputRecording.MaxDurationFrames, completion.Recording.DurationFrames);
        Assert.Equal(2, completion.Recording.Entries.Count);
        Assert.False(service.TryTakeCompletedCapture(out _));
        Assert.Equal(2, live.Count);
    }

    [Fact]
    public void Capture_LimitCompletesEvenWhenTerminalFrameHasNoInput()
    {
        var service = new TrainingInputService((_, _, _) => { });
        Assert.True(service.TryStartCapture(1, 10, 8, out _));

        service.CompleteCaptureFrame(10 + TrainingInputRecording.MaxDurationFrames);

        Assert.True(service.TryTakeCompletedCapture(out TrainingInputCaptureCompletion completion));
        Assert.Empty(completion.Recording.Entries);
        Assert.Equal((ulong)8, completion.Epoch);
    }

    [Fact]
    public void Capture_ZeroDurationCanPlayOnceButCannotLoop()
    {
        var service = new TrainingInputService((_, _, _) => { });
        Assert.True(service.TryStartCapture(1, int.MaxValue, 8, out _));
        TrainingInputRecording recording = service.StopCapture(int.MaxValue, "zero");

        Assert.Equal(0, recording.DurationFrames);
        Assert.Empty(recording.Entries);
        Assert.True(service.TryStartPlayback(recording, 2, 0, false, 8, out _));
        service.StopPlayback();
        Assert.False(service.TryStartPlayback(recording, 2, 0, true, 8, out string error));
        Assert.Contains("cannot loop", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Capture_FirstPostLimitInputRemainsLiveAndOutsideCompletedCandidate()
    {
        var live = new List<int>();
        var service = new TrainingInputService((_, _, value) => live.Add(value));
        Assert.True(service.TryStartCapture(1, 0, 8, out _));
        int terminal = TrainingInputRecording.MaxDurationFrames;
        service.AcceptCanonicalInput(1, InputType.Button, (int)ButtonValue.A, terminal);
        service.CompleteCaptureFrame(terminal);

        service.AcceptCanonicalInput(1, InputType.Button, (int)ButtonValue.B, terminal + 1);

        Assert.True(service.TryTakeCompletedCapture(out var completion));
        Assert.Single(completion.Recording.Entries);
        Assert.Equal((int)ButtonValue.A, completion.Recording.Entries[0].InputValue);
        Assert.Equal(new[] { (int)ButtonValue.A, (int)ButtonValue.B }, live);
    }

    [Fact]
    public void Capture_InvalidExplicitStopRetainsCaptureAndOwnership()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        Assert.True(service.TryStartCapture(1, 100, 4, out _));

        Assert.False(service.TryStopCapture(99, "bad", out _, out string error));

        Assert.True(service.IsCapturing);
        Assert.Equal(1, modes.CapturePlayer);
        Assert.Contains("before", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Capture_EntryLimitFailureCancelsAndReleasesOwnershipExactlyOnce()
    {
        int liveInputs = 0;
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => liveInputs++, modes);
        Assert.True(service.TryStartCapture(1, 0, 4, out _));

        for (int i = 0; i <= TrainingInputRecording.MaxEntries; i++)
            service.AcceptCanonicalInput(1, InputType.Button, (int)ButtonValue.A, 0);

        Assert.False(service.IsCapturing);
        Assert.Equal(0, modes.CapturePlayer);
        Assert.False(service.HasCompletedCapture);
        Assert.True(service.TryTakeOperationStatus(out string status));
        Assert.Contains("Entry count", status, System.StringComparison.Ordinal);
        Assert.Equal(TrainingInputRecording.MaxEntries + 1, liveInputs);
        Assert.True(service.TryStartCapture(1, 1, 4, out _));
    }

    [Fact]
    public void TrainingTransientReset_ReportsInvalidBoundaryParameter()
    {
        var history = new InputHistory(16);
        try
        {
            var error = Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                history.ResetTrainingTransient(1, -1));
            Assert.Equal("fromFrame", error.ParamName);
        }
        finally
        {
            history.Shutdown();
        }
    }

    [Fact]
    public void Playback_RebasesAndInjectsTerminalBeforeExactOnceStop()
    {
        var injected = new List<(int Player, InputType Type, int Value, bool Playing)>();
        TrainingInputService? service = null;
        service = new TrainingInputService((p, t, v) => injected.Add((p, t, v, service!.IsPlaying)));
        var recording = Recording(2,
            new(0, 0, InputType.Directional, (int)DirectionValue.Back),
            new(2, 0, InputType.Button, (int)ButtonValue.A));

        Assert.True(service.TryStartPlayback(recording, 2, 10_000, loop: false, epoch: 7, out _));
        Assert.Equal(1, service.ProcessPlaybackFrame(10_000, 7));
        Assert.Equal(0, service.ProcessPlaybackFrame(10_001, 7));
        Assert.Equal(1, service.ProcessPlaybackFrame(10_002, 7));

        Assert.True(service.IsPlaying);
        Assert.All(injected, item => Assert.True(item.Playing));
        Assert.Equal(new[] { (int)DirectionValue.Back, (int)ButtonValue.A }, injected.Select(i => i.Value));
        service.CompleteFrame();
        Assert.False(service.IsPlaying);
        Assert.Equal(0, service.ProcessPlaybackFrame(10_003, 7));
    }

    [Fact]
    public void Playback_DoesNotRecursivelyEnterActiveCapture()
    {
        var service = new TrainingInputService((_, _, _) => { });
        Assert.True(service.TryStartCapture(1, 0, 1, out _));
        var recording = Recording(0, new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.A));
        Assert.True(service.TryStartPlayback(recording, 2, 10, false, 1, out _));

        service.ProcessPlaybackFrame(10, 1);
        TrainingInputRecording captured = service.StopCapture(10, "live-only");

        Assert.Empty(captured.Entries);
    }

    [Fact]
    public void Playback_DifferentAbsoluteStartsProduceSameRelativeInjection()
    {
        var first = RunAt(0);
        var second = RunAt(10_000);
        Assert.Equal(first, second);

        static (int Offset, InputType Type, int Value)[] RunAt(int start)
        {
            var values = new List<(int Frame, InputType Type, int Value)>();
            int frame = start;
            var service = new TrainingInputService((_, t, v) => values.Add((frame, t, v)));
            var recording = Recording(3,
                new(0, 0, InputType.Directional, (int)DirectionValue.Forward),
                new(3, 0, InputType.Button, (int)ButtonValue.A));
            Assert.True(service.TryStartPlayback(recording, 2, start, false, 2, out _));
            for (frame = start; frame <= start + 3; frame++)
            {
                service.ProcessPlaybackFrame(frame, 2);
                service.CompleteFrame();
            }
            return values.Select(v => (v.Frame - start, v.Type, v.Value)).ToArray();
        }
    }

    [Fact]
    public void Playback_PreflightOverflowPreservesExistingPlayback()
    {
        var service = new TrainingInputService((_, _, _) => { });
        var current = Recording(1, new TrainingInputRecordingEntry(0, 0, InputType.Directional, 5));
        Assert.True(service.TryStartPlayback(current, 2, 10, false, 1, out _));

        Assert.False(service.TryStartPlayback(Recording(1), 1, int.MaxValue, false, 1, out string error));

        Assert.True(service.IsPlaying);
        Assert.Equal(2, service.PlaybackPlayer);
        Assert.Contains("overflow", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loop_LaterRebaseOverflowStopsOnceAndPublishesOneShotStatus()
    {
        var modes = new PlaybackModeCoordinator();
        int resets = 0;
        var service = new TrainingInputService(
            (_, _, _) => { }, modes, (_, _) => resets++);
        var recording = Recording(1,
            new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Back),
            new TrainingInputRecordingEntry(1, 0, InputType.Button, (int)ButtonValue.A));
        int start = int.MaxValue - 3;
        Assert.True(service.TryStartPlayback(recording, 2, start, true, 9, out _));

        service.ProcessPlaybackFrame(start, 9);
        service.CompleteFrame();
        service.ProcessPlaybackFrame(start + 1, 9);
        service.CompleteFrame();
        service.ProcessPlaybackFrame(int.MaxValue - 1, 9);
        service.CompleteFrame();
        service.ProcessPlaybackFrame(int.MaxValue, 9);
        service.CompleteFrame();

        Assert.False(service.IsPlaying);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.Equal(2, resets);
        Assert.True(service.TryTakeOperationStatus(out string status));
        Assert.Contains("exhausted", status, System.StringComparison.OrdinalIgnoreCase);
        Assert.False(service.TryTakeOperationStatus(out _));
        Assert.Equal(0, service.ProcessPlaybackFrame(int.MaxValue, 9));
    }

    [Fact]
    public void SharedAuthority_RejectsReplayCaptureAndSamePlayerConflictsAtomically()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        Assert.True(service.TryStartCapture(1, 0, out _));
        Assert.Throws<System.InvalidOperationException>(() =>
            modes.Enter(RuntimePlaybackMode.AuthoritativeReplay, 1));
        Assert.False(service.TryStartPlayback(Recording(1), 1, 0, false, 1, out _));
        Assert.True(service.TryStartPlayback(Recording(1), 2, 0, false, 1, out _));
        Assert.True(service.IsCapturing);
        Assert.True(service.IsPlaying);
        Assert.Equal(RuntimePlaybackMode.TrainingInput, modes.ActiveMode);
    }

    [Fact]
    public void Loop_InjectsNeutralAndResetsTransientBeforeRebase()
    {
        var trace = new List<string>();
        var service = new TrainingInputService(
            (_, type, value) => trace.Add($"inject:{type}:{value}"),
            resetTransient: (player, fromFrame) => trace.Add($"reset:{player}:{fromFrame}"));
        var recording = Recording(2,
            new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Back));
        Assert.True(service.TryStartPlayback(recording, 2, 10, true, 5, out _));
        service.ProcessPlaybackFrame(10, 5);
        trace.Clear();

        service.ProcessPlaybackFrame(12, 5);
        service.CompleteFrame();

        Assert.Equal(new[] { "reset:2:10", "inject:Directional:5" }, trace);
        Assert.True(service.IsPlaying);
        Assert.Equal(1, service.ProcessPlaybackFrame(13, 5));
    }

    [Fact]
    public void Loop_TerminalAndNextRelativeZeroUseConsecutiveAbsoluteFrames()
    {
        int frame = 10;
        var trace = new List<(int Frame, int Value)>();
        var service = new TrainingInputService((_, type, value) =>
        {
            if (type == InputType.Button) trace.Add((frame, value));
        });
        var recording = Recording(2,
            new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.A),
            new TrainingInputRecordingEntry(2, 0, InputType.Button, (int)ButtonValue.B));
        Assert.True(service.TryStartPlayback(recording, 2, 10, true, 5, out _));

        for (frame = 10; frame <= 13; frame++)
        {
            service.ProcessPlaybackFrame(frame, 5);
            service.CompleteFrame();
        }

        Assert.Equal(new[] { (10, (int)ButtonValue.A), (12, (int)ButtonValue.B), (13, (int)ButtonValue.A) }, trace);
    }

    [Fact]
    public void Loop_DurationOneRunsThreeIterationsWithOrderedTerminalResetAndOwnerIsolation()
    {
        int frame = 10;
        var trace = new List<string>();
        var service = new TrainingInputService(
            (player, type, value) => trace.Add($"{frame}:inject:{player}:{type}:{value}"),
            resetTransient: (player, _) => trace.Add($"{frame}:reset:{player}"));
        var recording = Recording(1,
            new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.A),
            new TrainingInputRecordingEntry(1, 0, InputType.Button, (int)ButtonValue.B),
            new TrainingInputRecordingEntry(1, 1, InputType.Button, (int)ButtonValue.C));
        Assert.True(service.TryStartPlayback(recording, 2, 10, true, 5, out _));

        for (frame = 10; frame <= 14; frame++)
        {
            service.ProcessPlaybackFrame(frame, 5);
            service.CompleteFrame();
        }

        Assert.Equal(new[]
        {
            "10:inject:2:Button:0",
            "11:inject:2:Button:1", "11:inject:2:Button:2",
            "11:reset:2", "11:inject:2:Directional:5",
            "12:inject:2:Button:0",
            "13:inject:2:Button:1", "13:inject:2:Button:2",
            "13:reset:2", "13:inject:2:Directional:5",
            "14:inject:2:Button:0"
        }, trace);
        Assert.DoesNotContain(trace, item => item.Contains(":1:"));
    }

    [Fact]
    public void EpochMismatch_CancelsWithoutInjectionAndReleasesMode()
    {
        var modes = new PlaybackModeCoordinator();
        int injections = 0;
        var service = new TrainingInputService((_, _, _) => injections++, modes);
        Assert.True(service.TryStartPlayback(Recording(1,
            new TrainingInputRecordingEntry(1, 0, InputType.Button, 0)), 2, 10, false, 7, out _));

        Assert.Equal(0, service.ProcessPlaybackFrame(11, 8));

        Assert.Equal(0, injections);
        Assert.False(service.IsPlaying);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
    }

    [Fact]
    public void StopAndShutdown_AreIdempotentAndReleaseAllOwnership()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        Assert.True(service.TryStartCapture(1, 0, 4, out _));
        Assert.True(service.TryStartPlayback(Recording(1), 2, 0, false, 4, out _));

        service.Shutdown();
        service.Shutdown();
        service.StopPlayback();

        Assert.False(service.IsCapturing);
        Assert.False(service.IsPlaying);
        Assert.Equal(0, modes.CapturePlayer);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
    }

    [Fact]
    public void LifecycleSubscription_CancelsCaptureAndPlaybackWithoutLeaking()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        service.StartLifecycleSubscriptions();
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        Assert.True(service.TryStartCapture(1, 0, epoch, out _));
        Assert.True(service.TryStartPlayback(Recording(1), 2, 0, false, epoch, out _));

        EventBus.Instance.PublishImmediate(new MatchInitializedEvent("p1", "p2"));

        Assert.False(service.IsCapturing);
        Assert.False(service.IsPlaying);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.False(service.HasCompletedCapture);
        Assert.False(service.TryTakeOperationStatus(out _));
        service.Shutdown();
        Assert.Equal(0, EventBus.Instance.GetSubscriberCount<MatchInitializedEvent>());
    }

    [Fact]
    public void Playback_SameFrameEventBusObservationMatchesRecordedSequenceDespiteLifo()
    {
        var history = new InputHistory(16);
        var observed = new List<InputReceivedEvent>();
        System.Action<InputReceivedEvent> handler = observed.Add;
        EventBus.Instance.Subscribe(handler);
        try
        {
            var service = new TrainingInputService(
                history.RecordInput, injectCanonicalBatch: history.RecordPlaybackInputs);
            var recording = Recording(0,
                new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Down),
                new TrainingInputRecordingEntry(0, 1, InputType.Button, (int)ButtonValue.A));
            Assert.True(service.TryStartPlayback(
                recording, 2, EventBus.Instance.CurrentFrame, false,
                EventBus.Instance.LifecycleEpoch, out _));

            service.ProcessPlaybackFrame(EventBus.Instance.CurrentFrame, EventBus.Instance.LifecycleEpoch);
            EventBus.Instance.ProcessFrame();

            Assert.Equal(new[]
            {
                ((int)InputType.Directional, (int)DirectionValue.Down),
                ((int)InputType.Button, (int)ButtonValue.A)
            }, observed.Select(item => (item.InputType, item.InputValue)));
            service.CancelForLifecycle();
        }
        finally
        {
            EventBus.Instance.Unsubscribe(handler);
            history.Shutdown();
        }
    }

    [Theory]
    [InlineData(2202)]
    [InlineData(2203)]
    [InlineData(2204)]
    [InlineData(2205)]
    [InlineData(2206)]
    public void DeterminismWindow_DifferentStartsProduceSameSixHundredFrameHash(int seed)
    {
        string first = HashRun(seed, 0);
        string second = HashRun(seed, 10_000);
        Assert.Equal(first, second);

        static string HashRun(int seed, int start)
        {
            var trace = new StringBuilder();
            int frame = start;
            var service = new TrainingInputService((player, type, value) =>
                trace.Append(frame - start).Append(':').Append(player).Append(':')
                    .Append((int)type).Append(':').Append(value).Append(';'));
            var entries = new[]
            {
                new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Down),
                new TrainingInputRecordingEntry(30 + seed % 5, 0, InputType.Button, (int)ButtonValue.A),
                new TrainingInputRecordingEntry(120, 0, InputType.Directional, (int)DirectionValue.Neutral)
            };
            var recording = new TrainingInputRecording(1, 1, 120, "deterministic", entries);
            Assert.True(service.TryStartPlayback(recording, 2, start, false, 11, out _));
            for (frame = start; frame < start + 600; frame++)
            {
                service.ProcessPlaybackFrame(frame, 11);
                service.CompleteFrame();
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString())));
        }
    }

    private static TrainingInputRecording Recording(int duration,
        params TrainingInputRecordingEntry[] entries) => new(1, 1, duration, "recording", entries);
}
