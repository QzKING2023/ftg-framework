#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FTG_Framework.Characters;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Input;
using Godot;

namespace FTG_Framework.ScaffoldVerification;

/// <summary>
/// Headless Godot end-to-end harness for Story 4.2 (S4.2-E) running inside the
/// live training scene.
///
/// Flow B (quiescent hash parity, E4.2-R): at spawn distance (400 px, out of
/// range) P1 whiffs a 5LP through the real input pipeline; the harness saves a
/// mid-move snapshot, starts state-scoped recording from snapshot.frame + 1,
/// captures an external per-frame BalanceFrameHash trail, then replays the file
/// through the real GameLoop wiring and requires frame-for-frame hash parity
/// with the recorded trail (E4.2-S: trails and replay file are persisted so a
/// second process can re-verify).
///
/// Flow A (mid-combo bootstrap + replay-end rebind, E4.2-G): P1 walks into
/// range and lands a real 5LP (ComboStartedEvent observed live); the harness
/// saves the snapshot mid-combo (knockback trajectory in flight — physics is
/// intentionally not re-advanced during playback, so no hash comparison here),
/// records, replays, and requires: zero StateRestored across bootstrap/
/// recording/replay, the frame domain rebinding to the last recorded frame on
/// the ReplayHandoff, and live play continuing cleanly afterward.
///
/// Restart mode (`--replay-verify` user arg, E4.2-S): a second process replays
/// the persisted Flow B replay file and compares its hash trail against the
/// persisted recorded trail — identical per-frame hashes across a process
/// restart.
/// </summary>
public partial class StateScopedReplaySmokeTest : Node
{
    private const int InitializationTimeoutFrames = 600;
    private const int MoveTimeoutFrames = 120;
    private const int WalkTimeoutFrames = 400;
    private const int RecordingFramesB = 30;
    private const int RecordingFramesA = 40;
    private const int PlaybackTimeoutFrames = 400;
    private const int LiveContinuationFrames = 10;
    private const float WalkStopDistance = 45.0f;

    private readonly List<string> _recordedTrail = new();
    private readonly List<string> _replayedTrail = new();
    private int _elapsedFrames;
    private int _stage;
    private int _stageFrames;
    private string? _failure;
    private GameLoop? _gameLoop;
    private CharacterController? _p1;
    private CharacterController? _p2;
    private bool _restartVerifyMode;

    private int _stateRestoredCount;
    private int _comboStartedCount;
    private int _comboEndedCount;
    private int _recordUntilFrame;

    private string _snapshotBPath = string.Empty;
    private string _snapshotAPath = string.Empty;
    private string _replayBPath = string.Empty;
    private string _replayAPath = string.Empty;
    private string _recordedTrailPath = string.Empty;
    private string _replayedTrailPath = string.Empty;

    private Action<StateRestoredEvent>? _stateRestoredHandler;
    private Action<ComboStartedEvent>? _comboStartedHandler;
    private Action<ComboEndedEvent>? _comboEndedHandler;

    public override void _Ready()
    {
        _snapshotBPath = ProjectSettings.GlobalizePath("user://s42-snapshot-b.json");
        _snapshotAPath = ProjectSettings.GlobalizePath("user://s42-snapshot-a.json");
        _replayBPath = ProjectSettings.GlobalizePath("user://s42-replay-b.json");
        _replayAPath = ProjectSettings.GlobalizePath("user://s42-replay-a.json");
        _recordedTrailPath = ProjectSettings.GlobalizePath("user://s42-recorded-trail.json");
        _replayedTrailPath = ProjectSettings.GlobalizePath("user://s42-replayed-trail.json");

        _restartVerifyMode = OS.GetCmdlineUserArgs().Contains("--replay-verify");

        _stateRestoredHandler = _ => _stateRestoredCount++;
        _comboStartedHandler = _ => _comboStartedCount++;
        _comboEndedHandler = _ => _comboEndedCount++;
        EventBus.Instance.Subscribe(_stateRestoredHandler);
        EventBus.Instance.Subscribe(_comboStartedHandler);
        EventBus.Instance.Subscribe(_comboEndedHandler);
    }

    public override void _ExitTree()
    {
        if (_stateRestoredHandler is not null) EventBus.Instance.Unsubscribe(_stateRestoredHandler);
        if (_comboStartedHandler is not null) EventBus.Instance.Unsubscribe(_comboStartedHandler);
        if (_comboEndedHandler is not null) EventBus.Instance.Unsubscribe(_comboEndedHandler);
        _stateRestoredHandler = null;
        _comboStartedHandler = null;
        _comboEndedHandler = null;
    }

    public override void _Process(double delta)
    {
        _elapsedFrames++;
        if (_gameLoop is null)
            _gameLoop = GetNodeOrNull<GameLoop>("/root/GameLoop");
        var characters = FindNodes<CharacterController>(GetTree().Root).ToArray();
        _p1 ??= characters.FirstOrDefault(c => c.PlayerId == 1);
        _p2 ??= characters.FirstOrDefault(c => c.PlayerId == 2);

        if (_restartVerifyMode)
        {
            RunRestartVerifyStage();
            return;
        }

        switch (_stage)
        {
            case 0: // Wait for the training runtime to initialize.
                if (RuntimeReady())
                    EnterStage(1);
                else if (_elapsedFrames > InitializationTimeoutFrames)
                    Fail("training runtime did not initialize within 600 frames");
                break;

            case 1: // Flow B: start a whiffed 5LP at spawn distance (out of range).
                if (TryStartMovePlayback("s42whiff"))
                    EnterStage(2);
                else if (_stageFrames > MoveTimeoutFrames)
                    Fail("whiff playback could not start");
                break;

            case 2: // Save the snapshot mid-move (move active, playback finished).
                if (_gameLoop!.FrameDataEngine!.GetPhase(1) == MovePhase.Active &&
                    !_gameLoop.TrainingInputService!.IsPlaying)
                {
                    if (!_gameLoop.TrainingStateService!.TrySave(_snapshotBPath, out string saveError))
                    {
                        Fail($"Flow B snapshot save failed: {saveError}");
                        return;
                    }
                    EnterStage(3);
                }
                else if (_stageFrames > MoveTimeoutFrames)
                    Fail($"Flow B whiff move never reached Active (phase={_gameLoop.FrameDataEngine!.GetPhase(1)}, playing={_gameLoop.TrainingInputService!.IsPlaying})");
                break;

            case 3: // Start state-scoped recording from snapshot.frame + 1.
                if (!StartRecording(_snapshotBPath, out string startError))
                {
                    Fail($"Flow B state-scoped recording failed: {startError}");
                    return;
                }
                _recordUntilFrame = EventBus.Instance.CurrentFrame + RecordingFramesB;
                GD.Print($"[StateScopedReplaySmoke] Flow B recording from frame " +
                         $"{EventBus.Instance.CurrentFrame - RecordingFramesB + 1} (record until {_recordUntilFrame})");
                EnterStage(4);
                break;

            case 4: // Record the quiescent window with an external hash trail.
                if (!_gameLoop!.ReplayIsRecording)
                {
                    Fail("Flow B recording ended unexpectedly");
                    return;
                }
                _recordedTrail.Add(_gameLoop.ComputeFrameHash(EventBus.Instance.CurrentFrame - 1, 1));
                if (EventBus.Instance.CurrentFrame >= _recordUntilFrame)
                {
                    if (!_gameLoop.TryStopStateScopedRecording(_replayBPath, out string stopError))
                    {
                        Fail($"Flow B stop recording failed: {stopError}");
                        return;
                    }
                    if (!File.Exists(_replayBPath))
                    {
                        Fail("Flow B replay file was not written");
                        return;
                    }
                    EnterStage(5);
                }
                else if (_stageFrames > RecordingFramesB + 60)
                    Fail("Flow B recording window never completed");
                break;

            case 5: // Playback the Flow B file through the real GameLoop wiring.
                if (_gameLoop!.TryStartStateScopedPlayback(_replayBPath, out string playbackError))
                    EnterStage(6);
                else
                    Fail($"Flow B playback could not start: {playbackError}");
                break;

            case 6: // Hash trail during playback; compare with the recorded trail.
                if (_gameLoop!.ReplayIsPlaying)
                {
                    _replayedTrail.Add(_gameLoop.ComputeFrameHash(EventBus.Instance.CurrentFrame - 1, 1));
                }
                else if (_stageFrames > 0)
                {
                    if (!_gameLoop.ReplayIsRecording && _stateRestoredCount != 0)
                    {
                        Fail($"Flow B observed {_stateRestoredCount} StateRestored publications");
                        return;
                    }
                    if (EventBus.Instance.CurrentFrame != _recordUntilFrame)
                    {
                        Fail($"Flow B replay-end rebind frame mismatch: {EventBus.Instance.CurrentFrame} != {_recordUntilFrame}");
                        return;
                    }
                    if (_recordedTrail.Count == 0 || _replayedTrail.Count == 0)
                    {
                        Fail($"Flow B empty hash trail (recorded={_recordedTrail.Count}, replayed={_replayedTrail.Count})");
                        return;
                    }
                    if (!_recordedTrail.Take(_replayedTrail.Count)
                            .Select(StateComponents)
                            .SequenceEqual(_replayedTrail.Select(StateComponents)))
                    {
                        int firstMismatch = 0;
                        for (int i = 0; i < Math.Min(_recordedTrail.Count, _replayedTrail.Count); i++)
                        {
                            if (StateComponents(_recordedTrail[i]) != StateComponents(_replayedTrail[i]))
                            {
                                firstMismatch = i;
                                break;
                            }
                        }
                        Fail($"Flow B state-hash parity broken at index {firstMismatch} " +
                             $"(recorded={_recordedTrail[firstMismatch]}, replayed={_replayedTrail[firstMismatch]})");
                        return;
                    }
                    // The input history is live-only: recording accumulates a
                    // canonical neutral every frame, while replay restores it once
                    // from the snapshot and never writes it again. Asserting the
                    // replay trail's input component stays identical frame-over-frame
                    // proves the bootstrap froze the input history (S4.2-C).
                    if (_replayedTrail.Select(InputComponent).Distinct().Count() != 1)
                    {
                        Fail("Flow B input history changed during replay (expected frozen restore)");
                        return;
                    }
                    PersistTrails();
                    GD.Print($"[StateScopedReplaySmoke] Flow B PASS: {_replayedTrail.Count} frames " +
                             $"state-hash parity, rebind frame {EventBus.Instance.CurrentFrame}, " +
                             $"stateRestored={_stateRestoredCount}, input history frozen during replay");
                    EnterStage(7);
                }
                else if (_stageFrames > PlaybackTimeoutFrames)
                    Fail("Flow B playback did not complete");
                break;

            case 7: // Flow A: walk P1 into range of P2.
                if (TryStartWalkPlayback())
                    EnterStage(8);
                else if (_stageFrames > MoveTimeoutFrames)
                    Fail("walk playback could not start");
                break;

            case 8: // Stop walking once P1 is within 5LP contact distance.
                if (CharacterDistance() <= WalkStopDistance)
                {
                    _gameLoop!.TrainingInputService!.StopPlayback();
                    EnterStage(9);
                }
                else if (_stageFrames > WalkTimeoutFrames)
                    Fail("P1 never walked into range");
                break;

            case 9: // Land a real 5LP, then save the snapshot mid-combo.
                if (_comboStartedCount == 0 && !TryStartMovePlayback("s42hit", out string hitStartError))
                {
                    if (_stageFrames == 1)
                        GD.Print($"[StateScopedReplaySmoke] hit playback rejected: {hitStartError}");
                    if (_stageFrames > MoveTimeoutFrames)
                    {
                        GD.Print($"[StateScopedReplaySmoke] stage-9 debug: " +
                                 $"frame={EventBus.Instance.CurrentFrame}, p1X={_p1!.GlobalPosition.X:F1}, " +
                                 $"p2X={_p2!.GlobalPosition.X:F1}, gap={CharacterDistance():F1}, " +
                                 $"phase={_gameLoop!.FrameDataEngine!.GetPhase(1)}, " +
                                 $"moveId={_gameLoop.FrameDataEngine!.GetCurrentMoveId(1)}, " +
                                 $"playing={_gameLoop.TrainingInputService!.IsPlaying}, " +
                                 $"comboStarted={_comboStartedCount}");
                        Fail("hit playback could not start");
                    }
                }
                else if (_comboStartedCount > 0 && !_gameLoop!.TrainingInputService!.IsPlaying)
                {
                    if (!_gameLoop.TrainingStateService!.TrySave(_snapshotAPath, out string saveError))
                    {
                        Fail($"Flow A mid-combo snapshot save failed: {saveError}");
                        return;
                    }
                    EnterStage(10);
                }
                else if (_stageFrames > MoveTimeoutFrames + 60)
                    Fail($"Flow A combo never started (comboStarted={_comboStartedCount}, playing={_gameLoop!.TrainingInputService!.IsPlaying})");
                break;

            case 10: // Start state-scoped recording from the mid-combo snapshot.
                if (!StartRecording(_snapshotAPath, out string startErrorA))
                {
                    Fail($"Flow A state-scoped recording failed: {startErrorA}");
                    return;
                }
                _recordUntilFrame = EventBus.Instance.CurrentFrame + RecordingFramesA;
                GD.Print($"[StateScopedReplaySmoke] Flow A mid-combo recording from frame " +
                         $"{EventBus.Instance.CurrentFrame - RecordingFramesA + 1} (combo started: {_comboStartedCount})");
                EnterStage(11);
                break;

            case 11: // Record the mid-combo window (knockback in flight — no hash here).
                if (!_gameLoop!.ReplayIsRecording)
                {
                    Fail("Flow A recording ended unexpectedly");
                    return;
                }
                if (EventBus.Instance.CurrentFrame >= _recordUntilFrame)
                {
                    if (!_gameLoop.TryStopStateScopedRecording(_replayAPath, out string stopErrorA))
                    {
                        Fail($"Flow A stop recording failed: {stopErrorA}");
                        return;
                    }
                    if (!File.Exists(_replayAPath))
                    {
                        Fail("Flow A replay file was not written");
                        return;
                    }
                    EnterStage(12);
                }
                else if (_stageFrames > RecordingFramesA + 60)
                    Fail("Flow A recording window never completed");
                break;

            case 12: // Playback the mid-combo file.
                if (_gameLoop!.TryStartStateScopedPlayback(_replayAPath, out string playbackErrorA))
                    EnterStage(13);
                else
                    Fail($"Flow A playback could not start: {playbackErrorA}");
                break;

            case 13: // Wait for playback end; verify bootstrap/replay invariants.
                if (!_gameLoop!.ReplayIsPlaying && _stageFrames > 0)
                {
                    if (_stateRestoredCount != 0)
                    {
                        Fail($"Flow A observed {_stateRestoredCount} StateRestored publications across bootstrap+recording+replay");
                        return;
                    }
                    if (_comboEndedCount == 0)
                    {
                        Fail("Flow A combo never ended (mid-combo round trip incomplete)");
                        return;
                    }
                    if (_gameLoop.ReplayIsRecording)
                    {
                        Fail("Flow A recorder still attached after playback");
                        return;
                    }
                    if (EventBus.Instance.CurrentFrame != _recordUntilFrame)
                    {
                        Fail($"Flow A replay-end rebind frame mismatch: {EventBus.Instance.CurrentFrame} != {_recordUntilFrame}");
                        return;
                    }
                    GD.Print($"[StateScopedReplaySmoke] Flow A PASS: mid-combo bootstrap + replay-end rebind " +
                             $"(frame {EventBus.Instance.CurrentFrame}, comboStarted={_comboStartedCount}, " +
                             $"comboEnded={_comboEndedCount}, stateRestored={_stateRestoredCount})");
                    EnterStage(14);
                }
                else if (_stageFrames > PlaybackTimeoutFrames)
                    Fail("Flow A playback did not complete");
                break;

            case 14: // Live play continues after the rebind (frame domain advances).
                if (_stageFrames >= LiveContinuationFrames)
                {
                    if (_gameLoop!.ReplayIsPlaying)
                    {
                        Fail("replay re-activated during live continuation");
                        return;
                    }
                    GD.Print($"[StateScopedReplaySmoke] Flow A live continuation: {LiveContinuationFrames} frames " +
                             $"advanced to frame {EventBus.Instance.CurrentFrame}");
                    EnterStage(15);
                }
                break;

            case 15: // The live input pipeline still resolves moves after rebind.
                if (TryStartMovePlayback("s42live"))
                    EnterStage(16);
                else if (_stageFrames > MoveTimeoutFrames)
                    Fail("post-rebind live playback could not start");
                break;

            case 16: // Confirm the live move started.
                if (_gameLoop!.FrameDataEngine!.GetPhase(1) != MovePhase.Idle)
                {
                    GD.Print($"[StateScopedReplaySmoke] PASS: post-rebind live move '{_gameLoop.FrameDataEngine!.GetCurrentMoveId(1)}' started");
                    GetTree().Quit(0);
                }
                else if (_stageFrames > MoveTimeoutFrames)
                    Fail("post-rebind live move never started");
                break;
        }

        if (_stage > 0) _stageFrames++;
    }

    private void RunRestartVerifyStage()
    {
        switch (_stage)
        {
            case 0: // Wait for the fresh runtime to initialize.
                if (RuntimeReady())
                    EnterStage(1);
                else if (_elapsedFrames > InitializationTimeoutFrames)
                    Fail("verify runtime did not initialize within 600 frames");
                break;

            case 1: // Replay the persisted Flow B file in this clean process.
                if (_gameLoop!.TryStartStateScopedPlayback(_replayBPath, out string playbackError))
                    EnterStage(2);
                else
                    Fail($"verify playback could not start: {playbackError}");
                break;

            case 2: // Hash trail; compare with the persisted recorded trail.
                if (_gameLoop!.ReplayIsPlaying)
                {
                    _replayedTrail.Add(_gameLoop.ComputeFrameHash(EventBus.Instance.CurrentFrame - 1, 1));
                }
                else if (_stageFrames > 0)
                {
                    if (_stateRestoredCount != 0)
                    {
                        Fail($"verify observed {_stateRestoredCount} StateRestored publications");
                        return;
                    }
                    string[] recorded;
                    try
                    {
                        recorded = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_recordedTrailPath))
                            ?? Array.Empty<string>();
                    }
                    catch (Exception ex) when (ex is IOException or JsonException)
                    {
                        Fail($"verify could not read the recorded trail: {ex.Message}");
                        return;
                    }
                    if (recorded.Length == 0 || _replayedTrail.Count == 0)
                    {
                        Fail($"verify empty trail (recorded={recorded.Length}, replayed={_replayedTrail.Count})");
                        return;
                    }
                    if (!recorded.Take(_replayedTrail.Count)
                            .Select(StateComponents)
                            .SequenceEqual(_replayedTrail.Select(StateComponents)))
                    {
                        int firstMismatch = 0;
                        for (int i = 0; i < Math.Min(recorded.Length, _replayedTrail.Count); i++)
                        {
                            if (StateComponents(recorded[i]) != StateComponents(_replayedTrail[i]))
                            {
                                firstMismatch = i;
                                break;
                            }
                        }
                        Fail($"verify state-hash parity broken at index {firstMismatch} " +
                             $"(recorded={recorded[firstMismatch]}, replayed={_replayedTrail[firstMismatch]})");
                        return;
                    }
                    if (_replayedTrail.Select(InputComponent).Distinct().Count() != 1)
                    {
                        Fail("verify input history changed during replay (expected frozen restore)");
                        return;
                    }
                    GD.Print($"[StateScopedReplaySmoke] PASS: restart portability — {_replayedTrail.Count} frames " +
                             $"state-hash parity after process restart, stateRestored={_stateRestoredCount}, " +
                             $"input history frozen during replay");
                    GetTree().Quit(0);
                }
                else if (_stageFrames > PlaybackTimeoutFrames)
                    Fail("verify playback did not complete");
                break;
        }

        if (_stage > 0) _stageFrames++;
    }

    private void EnterStage(int stage)
    {
        _stage = stage;
        _stageFrames = 0;
    }

    private void Fail(string failure)
    {
        if (_failure is not null) return;
        _failure = failure;
        GD.PushError($"[StateScopedReplaySmoke] {failure}");
        GetTree().Quit(1);
    }

    private bool RuntimeReady() =>
        _gameLoop is not null &&
        _gameLoop.TrainingStateService is not null &&
        _gameLoop.TrainingInputService is not null &&
        _gameLoop.TrainingRecordingLibrary is not null &&
        _gameLoop.FrameDataEngine is not null &&
        _p1 is not null && _p2 is not null;

    private bool StartRecording(string snapshotPath, out string error) =>
        _gameLoop!.TryStartStateScopedRecording(snapshotPath, out error) &&
        VerifyRecordingStarted(out error);

    private bool VerifyRecordingStarted(out string error)
    {
        if (!_gameLoop!.ReplayIsRecording)
        {
            error = "recorder is not active";
            return false;
        }
        if (_gameLoop.ReplayIsPlaying)
        {
            error = "playback is active";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private bool TryStartMovePlayback(string name) =>
        TryStartMovePlayback(name, out _);

    private bool TryStartMovePlayback(string name, out string error)
    {
        // During playback live input for the player is suppressed, so the
        // neutral must accompany the button in the same frame or TryMatch
        // (which requires the direction and button at the current frame for
        // a neutral normal) never resolves.
        var entries = new List<TrainingInputRecordingEntry>
        {
            new(1, 0, InputType.Directional, (int)DirectionValue.Neutral),
            new(1, 1, InputType.Button, (int)ButtonValue.A)
        };
        return _gameLoop!.TrainingInputService!.TryStartPlayback(
            new TrainingInputRecording(1, 1, 4, name, entries), 1,
            Math.Max(0, EventBus.Instance.CurrentFrame), false,
            EventBus.Instance.LifecycleEpoch, out error);
    }

    private bool TryStartWalkPlayback()
    {
        var entries = new List<TrainingInputRecordingEntry>();
        for (int frame = 0; frame < 300; frame++)
            entries.Add(new TrainingInputRecordingEntry(frame, 0, InputType.Directional, (int)DirectionValue.Forward));
        return _gameLoop!.TrainingInputService!.TryStartPlayback(
            new TrainingInputRecording(1, 1, 300, "s42walk", entries), 1,
            Math.Max(0, EventBus.Instance.CurrentFrame), false,
            EventBus.Instance.LifecycleEpoch, out _);
    }

    private float CharacterDistance()
    {
        if (_p1 is null || _p2 is null) return float.MaxValue;
        return Math.Abs(_p1.GlobalPosition.X - _p2.GlobalPosition.X);
    }

    // The BalanceFrameHash input component ("in:...;") is live-only: recording
    // accumulates one canonical neutral per frame from the live input block,
    // while replay restores the history once from the snapshot and never writes
    // it again. Parity holds for the deterministic state components only.
    private static string StateComponents(string hash)
    {
        int inputIndex = hash.IndexOf("in:", StringComparison.Ordinal);
        return inputIndex < 0 ? hash : hash[..inputIndex];
    }

    private static string InputComponent(string hash)
    {
        int inputIndex = hash.IndexOf("in:", StringComparison.Ordinal);
        return inputIndex < 0 ? string.Empty : hash[inputIndex..];
    }

    private void PersistTrails()
    {
        try
        {
            File.WriteAllText(_recordedTrailPath, JsonSerializer.Serialize(_recordedTrail.ToArray()));
            File.WriteAllText(_replayedTrailPath, JsonSerializer.Serialize(_replayedTrail.ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"[StateScopedReplaySmoke] could not persist trails: {ex.Message}");
        }
    }

    private static IEnumerable<T> FindNodes<T>(Node node) where T : Node
    {
        if (node is T match) yield return match;
        foreach (Node child in node.GetChildren())
        foreach (T descendant in FindNodes<T>(child))
            yield return descendant;
    }
}
