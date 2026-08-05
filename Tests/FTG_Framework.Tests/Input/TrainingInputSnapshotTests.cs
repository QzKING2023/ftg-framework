#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Core.Replay;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class TrainingInputSnapshotTests
{
    private static TrainingInputService Service(Action<int, InputType, int>? inject = null,
        PlaybackModeCoordinator? modes = null) =>
        new(inject ?? ((_, _, _) => { }), modes ?? new PlaybackModeCoordinator());

    private static TrainingInputRecording Recording(int duration, string name,
        params TrainingInputRecordingEntry[] entries) => new(1, 1, duration, name, entries);

    private static TrainingInputRecordingEntry Entry(int frame, int value,
        InputType type = InputType.Directional) =>
        new(frame, 0, type, value);

    private static string Payload(TrainingInputRecording recording) =>
        Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording));

    // ─── Capture ───────────────────────────────────────────────────

    [Fact]
    public void Capture_Snapshot_IncludesLibraryAssignmentsSelectionAndSession()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, library: library);
        var recording = Recording(30, "combo-setup", Entry(0, 5), Entry(10, 6));
        Assert.True(library.TryAddOrReplace(recording, false, null, out _));
        service.SelectedRecordingName = "combo-setup";
        Assert.True(service.TryStartPlayback(recording, 2, 4, false, 7, out _));
        service.ProcessPlaybackFrame(4, 7);
        service.CompleteFrame();
        service.ProcessPlaybackFrame(5, 7);
        service.CompleteFrame();

        TrainingInputRuntimeSnapshot snapshot = service.CaptureTrainingState();

        Assert.Single(snapshot.Library);
        Assert.Equal("combo-setup", snapshot.Library[0].Name);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Library[0].CodecPayloadBase64));
        Assert.Equal("combo-setup", snapshot.SelectedRecording);
        Assert.Null(snapshot.P1Assignment);
        Assert.Null(snapshot.P2Assignment);
        Assert.NotNull(snapshot.Session);
        Assert.Equal("combo-setup", snapshot.Session!.RecordingName);
        Assert.Equal(2, snapshot.Session.DummyPlayer);
        Assert.Equal(4, snapshot.Session.StartFrame);
        Assert.Equal(1, snapshot.Session.EntryIndex);
        Assert.False(snapshot.Session.Loop);
        Assert.Equal(7UL, snapshot.Session.Epoch);
    }

    [Fact]
    public void Capture_WithLibraryAssignments_RoundTripsAssignmentNames()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, library: library);
        var recording = Recording(5, "a", Entry(0, 1));
        Assert.True(library.TryAddOrReplace(recording, false, null, out _));
        Assert.True(library.TryAssign(1, "a", out _));
        service.SelectedRecordingName = "a";

        TrainingInputRuntimeSnapshot snapshot = service.CaptureTrainingState();

        Assert.Equal("a", snapshot.P1Assignment);
        Assert.Null(snapshot.P2Assignment);
        Assert.Equal("a", snapshot.SelectedRecording);
        Assert.Single(snapshot.Library);
        Assert.Equal("a", snapshot.Library[0].Name);
        Assert.Null(snapshot.Session);
    }

    // ─── Prepare ───────────────────────────────────────────────────

    [Fact]
    public void Prepare_RebindsSessionEpochToReservedEpoch()
    {
        var service = Service();
        var recording = Recording(30, "r", Entry(0, 5));
        service.SelectedRecordingName = "r";
        Assert.True(service.TryStartPlayback(recording, 2, 0, false, 1, out _));
        service.ProcessPlaybackFrame(9, 1);
        service.CompleteFrame();
        TrainingInputRuntimeSnapshot captured = service.CaptureTrainingState();
        Assert.Equal(1UL, captured.Session!.Epoch);

        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            captured, new SnapshotPrepareContext(SourceEpoch: 1, ReservedEpoch: 42, Frame: 9, SnapshotRestoreMode.Normal));

        Assert.Equal(42UL, prepared.Session!.Epoch);
        Assert.Equal(captured.Session.StartFrame, prepared.Session.StartFrame);
        Assert.Equal(captured.Session.EntryIndex, prepared.Session.EntryIndex);
    }

    [Fact]
    public void Prepare_UnknownSessionRecording_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            Array.Empty<TrainingInputRecordingRuntimeSnapshot>(), null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("ghost", 2, 0, 0, false, false, false, 1));

        var ex = Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));

        Assert.Equal(SnapshotParticipantCatalog.TrainingInput, ex.FaultPoint);
        Assert.Contains("ghost", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_InvalidDummyPlayer_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(30, "r", Entry(0, 1)))) }, null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 3, 0, 0, false, false, false, 1));

        Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
    }

    [Fact]
    public void Prepare_StartFrameBeyondSnapshotFrame_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(30, "r", Entry(0, 1)))) }, null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 99, 0, false, false, false, 1));

        var ex = Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 50, SnapshotRestoreMode.Normal)));
        Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_ConflictingPendingFlags_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(30, "r", Entry(0, 1)))) }, null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, true, false, true, 1));

        Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
    }

    [Fact]
    public void Prepare_NegativeEntryIndex_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(30, "r", Entry(0, 1)))) }, null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, -1, false, false, false, 1));

        Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
    }

    [Fact]
    public void Prepare_AssignmentWithoutLibraryEntry_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(30, "r", Entry(0, 1)))) }, "missing", null, null, null);

        Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
    }

    // ─── Install + promotion ───────────────────────────────────────

    [Fact]
    public void Install_DoesNotActivateLivePlayback_ButPromotesAtResumeFrame()
    {
        var injected = new List<string>();
        var modes = new PlaybackModeCoordinator();
        var service = Service((player, type, value) =>
            injected.Add($"{player}:{(int)type}:{value}"), modes);
        var recording = Recording(20, "r", Entry(10, 5), Entry(16, 7));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot(
                "r", Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording))) },
            null, null, "r",
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 1));
        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 10, SnapshotRestoreMode.Normal));

        service.InstallTrainingState(prepared);

        Assert.False(service.IsPlaying, "Install must not activate live playback.");
        Assert.Equal(RuntimePlaybackMode.TrainingInput, modes.ActiveMode);
        Assert.Equal(9UL, modes.ActiveEpoch);

        service.ProcessPlaybackFrame(10, 9);
        Assert.True(service.IsPlaying, "First resumed frame promotes the restored session.");
        service.CompleteFrame();

        service.ProcessPlaybackFrame(16, 9);
        service.CompleteFrame();

        Assert.Equal(new[] { "2:0:5", "2:0:7" }, injected);
    }

    [Fact]
    public void Install_StaleEpochSession_IsDroppedNotResurrected()
    {
        var injected = new List<string>();
        var service = Service((player, type, value) => injected.Add($"{player}:{value}"));
        var recording = Recording(20, "r", Entry(0, 5));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot(
                "r", Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording))) },
            null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 1));
        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 0, SnapshotRestoreMode.Normal));
        service.InstallTrainingState(prepared);

        service.ProcessPlaybackFrame(1, 10);
        Assert.False(service.IsPlaying);
        Assert.Empty(injected);
    }

    [Fact]
    public void RestoreContinuation_ProducesIdenticalInjectionHashAsLiveSession()
    {
        const int captureAt = 17;
        var entries = new[]
        {
            Entry(0, 3), Entry(5, 4), Entry(18, 5), Entry(25, 6),
            new TrainingInputRecordingEntry(34, 0, InputType.Button, (int)ButtonValue.A)
        };
        var recording = Recording(40, "mid-combo", entries);

        // Live session A: runs frames 0..40 without any restore; compare its
        // post-capture window (frames after the restore point) against the
        // restored session B that continues from that same point.
        string liveTrace = HashWindow(Trace(recording, restoreFrame: null), fromFrame: captureAt + 1);
        string restoredTrace = HashWindow(Trace(recording, restoreFrame: captureAt), fromFrame: captureAt + 1);

        Assert.Equal(liveTrace, restoredTrace);

        static string HashWindow(List<(int Frame, string Entry)> injections, int fromFrame) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Concat(injections.Where(item => item.Frame >= fromFrame).Select(item => item.Entry)))));

        static List<(int Frame, string Entry)> Trace(TrainingInputRecording recording, int? restoreFrame)
        {
            var injections = new List<(int Frame, string Entry)>();
            int currentFrame = 0;
            ulong epoch = 1;
            var service = new TrainingInputService((player, type, value) =>
                    injections.Add((currentFrame, $";{(int)type}:{value}")),
                injectCanonicalBatch: (player, batch) =>
                {
                    foreach (var entry in batch)
                        injections.Add((currentFrame, $";{(int)entry.InputType}:{entry.InputValue}"));
                });
            Assert.True(service.TryStartPlayback(recording, 2, 0, false, epoch, out _));
            for (int frame = 0; frame <= 40; frame++)
            {
                currentFrame = frame;
                service.ProcessPlaybackFrame(frame, epoch);
                service.CompleteFrame();
                if (restoreFrame is { } at && frame == at)
                {
                    TrainingInputRuntimeSnapshot capturedState = service.CaptureTrainingState();
                    var fresh = new TrainingInputService((player, type, value) =>
                            injections.Add((currentFrame, $";{(int)type}:{value}")),
                        injectCanonicalBatch: (player, batch) =>
                        {
                            foreach (var entry in batch)
                                injections.Add((currentFrame, $";{(int)entry.InputType}:{entry.InputValue}"));
                        });
                    TrainingInputService.PreparedTrainingInputState prepared = fresh.PrepareTrainingState(
                        capturedState, new SnapshotPrepareContext(1, 9, frame, SnapshotRestoreMode.Normal));
                    fresh.InstallTrainingState(prepared);
                    service = fresh;
                    epoch = 9;
                }
            }
            return injections;
        }
    }

    [Fact]
    public void LoopContinuation_AfterRestore_KeepsCyclePhase()
    {
        const int captureAt = 7;
        var entries = new[]
        {
            Entry(0, 1), new TrainingInputRecordingEntry(5, 0, InputType.Button, (int)ButtonValue.A)
        };
        var recording = Recording(5, "loop", entries);

        string live = Tail(Trace(recording, restoreAt: null), captureAt + 1);
        string restored = Tail(Trace(recording, restoreAt: captureAt), captureAt + 1);
        Assert.Equal(live, restored);

        // The restored service starts emitting at the first frame after capture;
        // the live trace must be cut at the same point for an apples-to-apples
        // cycle-phase comparison.
        static string Tail(List<(int Frame, string Entry)> injections, int fromFrame) =>
            string.Concat(injections.Where(item => item.Frame >= fromFrame).Select(item => item.Entry));

        static List<(int Frame, string Entry)> Trace(TrainingInputRecording recording, int? restoreAt)
        {
            var injections = new List<(int Frame, string Entry)>();
            int currentFrame = 0;
            ulong epoch = 1;
            var service = new TrainingInputService((player, type, value) =>
                injections.Add((currentFrame, $";{(int)type}:{value}")));
            Assert.True(service.TryStartPlayback(recording, 2, 0, true, epoch, out _));
            for (int frame = 0; frame <= 24; frame++)
            {
                currentFrame = frame;
                service.ProcessPlaybackFrame(frame, epoch);
                service.CompleteFrame();
                if (restoreAt is { } at && frame == at)
                {
                    TrainingInputRuntimeSnapshot captured = service.CaptureTrainingState();
                    var fresh = new TrainingInputService((player, type, value) =>
                        injections.Add((currentFrame, $";{(int)type}:{value}")));
                    TrainingInputService.PreparedTrainingInputState prepared = fresh.PrepareTrainingState(
                        captured, new SnapshotPrepareContext(1, 9, frame, SnapshotRestoreMode.Normal));
                    fresh.InstallTrainingState(prepared);
                    service = fresh;
                    epoch = 9;
                }
            }
            return injections;
        }
    }

    [Fact]
    public void Install_RebuildsLibraryAndAssignmentsFromCodecPayloads()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, library: library);
        var recording = Recording(5, "r", Entry(0, 1));
        var other = Recording(7, "r2", Entry(1, 2));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[]
            {
                new TrainingInputRecordingRuntimeSnapshot("r",
                    Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording))),
                new TrainingInputRecordingRuntimeSnapshot("r2",
                    Convert.ToBase64String(TrainingInputRecordingCodec.Encode(other)))
            },
            "r", "r2", "r2", null);

        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal));
        service.InstallTrainingState(prepared);

        Assert.Equal(2, library.Recordings.Count);
        Assert.Equal("r", library.Recordings["r"].Name);
        Assert.Equal("r2", library.Recordings["r2"].Name);
        Assert.Equal("r2", service.SelectedRecordingName);
        Assert.Equal(7, library.Recordings["r2"].DurationFrames);
        Assert.Equal("r", library.GetAssigned(1)!.Name);
        Assert.Equal("r2", library.GetAssigned(2)!.Name);
    }

    [Fact]
    public void Prepare_EntryIndexBeyondRecordingEntries_Rejects()
    {
        var service = Service();
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(Recording(5, "r", Entry(0, 1)))) },
            null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 99, false, false, false, 1));

        var ex = Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal)));
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Install_ReplayMode_DoesNotTouchLiveLibrary()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, library: library);
        var live = Recording(5, "live", Entry(0, 1));
        Assert.True(library.TryAddOrReplace(live, false, null, out _));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("other", Payload(Recording(5, "other", Entry(0, 1)))) },
            null, null, null, null);

        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.ReplayBootstrap));
        service.InstallTrainingState(prepared);

        Assert.Single(library.Recordings);
        Assert.True(library.Recordings.ContainsKey("live"));
    }

    [Fact]
    public void Capture_IncludesPendingRestoredSession()
    {
        var service = Service();
        var recording = Recording(5, "r", Entry(0, 1));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(recording)) },
            null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 1));
        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal));
        service.InstallTrainingState(prepared);

        TrainingInputRuntimeSnapshot recaptured = service.CaptureTrainingState();

        Assert.NotNull(recaptured.Session);
        Assert.Equal("r", recaptured.Session!.RecordingName);
        Assert.Equal(9UL, recaptured.Session.Epoch);
    }

    [Fact]
    public void Promote_YieldsToNewPlaybackStartedAfterSkippedModeEnter()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        Assert.True(service.TryStartCapture(1, 0, 7, out _));
        var recording = Recording(5, "r", Entry(0, 1));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", Payload(recording)) },
            null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 1));
        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal));
        service.InstallTrainingState(prepared);
        // StateRestored releases the old-epoch capture after commit; the mode
        // was never entered, so a new playback in the window would succeed and
        // must not be hijacked by the pending restored session.
        service.CancelForLifecycle();
        Assert.Equal(0, modes.CapturePlayer);

        var fresh = Recording(5, "fresh", Entry(0, 3));
        Assert.True(service.TryStartPlayback(fresh, 2, 0, false, 9, out _));
        service.ProcessPlaybackFrame(6, 9);

        Assert.True(service.IsPlaying);
        Assert.Same(fresh, service.ActivePlaybackRecording);
    }

    [Fact]
    public void Prepare_MalformedPayload_RejectsBeforeAnyInstall()
    {
        var library = new TrainingInputRecordingLibrary();
        var service = new TrainingInputService((_, _, _) => { }, library: library);
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[] { new TrainingInputRecordingRuntimeSnapshot("r", "!!!not-base64!!!") },
            null, null, null, null);

        var ex = Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal)));
        Assert.Equal(SnapshotParticipantCatalog.TrainingInput, ex.FaultPoint);
        Assert.Empty(library.Recordings);
    }

    [Fact]
    public void Prepare_DecodedNameMismatch_Rejects()
    {
        var service = Service();
        var recording = Recording(5, "declared", Entry(0, 1));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[]
            {
                new TrainingInputRecordingRuntimeSnapshot("other",
                    Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording)))
            },
            null, null, null, null);

        var ex = Assert.Throws<SnapshotPrepareException>(() => service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal)));
        Assert.Contains("different name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Install_WithLiveOldEpochCapture_SkipsModeEnterWithoutThrowing()
    {
        var modes = new PlaybackModeCoordinator();
        var service = new TrainingInputService((_, _, _) => { }, modes);
        Assert.True(service.TryStartCapture(1, 0, 7, out _));
        var recording = Recording(5, "r", Entry(0, 1));
        var snapshot = new TrainingInputRuntimeSnapshot(
            new[]
            {
                new TrainingInputRecordingRuntimeSnapshot("r",
                    Convert.ToBase64String(TrainingInputRecordingCodec.Encode(recording)))
            },
            null, null, null,
            new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 1));

        TrainingInputService.PreparedTrainingInputState prepared = service.PrepareTrainingState(
            snapshot, new SnapshotPrepareContext(1, 9, 5, SnapshotRestoreMode.Normal));
        service.InstallTrainingState(prepared);

        // The mode enter is skipped (no throw); the old-epoch capture is released
        // on StateRestored and the restored session still promotes.
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.Equal(1, modes.CapturePlayer);
        service.ProcessPlaybackFrame(6, 9);
        Assert.True(service.IsPlaying);
    }
}
