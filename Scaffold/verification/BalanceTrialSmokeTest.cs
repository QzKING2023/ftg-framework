#nullable enable
using System;
using System.IO;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Balance;
using FTG_Framework.Input;
using Godot;

namespace FTG_Framework.ScaffoldVerification;

/// <summary>
/// Headless Godot end-to-end harness for the Story 4.1 deterministic balance
/// testbed: saves a training snapshot containing a real recording, prepares and
/// starts a trial through the runtime BalanceTrialService, and waits for the
/// observation window to complete. Proves the runtime wiring (snapshot restore,
/// training playback re-simulation, per-frame hashes, metrics) end to end.
/// </summary>
public partial class BalanceTrialSmokeTest : Node
{
    private const int InitializationTimeoutFrames = 600;
    private const int TrialWindowFrames = 120;
    private const int TrialTimeoutFrames = 900;
    private int _elapsedFrames;
    private int _stage;
    private int _stageFrames;
    private string? _snapshotPath;
    private string? _failure;

    public override void _Process(double delta)
    {
        _elapsedFrames++;
        var gameLoop = GetNodeOrNull<GameLoop>("/root/GameLoop");
        var characters = FindNodes<FTG_Framework.Characters.CharacterController>(GetTree().Root).ToArray();

        switch (_stage)
        {
            case 0: // Wait for the training runtime to initialize.
                if (gameLoop?.BalanceTrialService is not null &&
                    gameLoop.TrainingStateService is not null &&
                    gameLoop.TrainingRecordingLibrary is not null &&
                    characters.Length == 2)
                    EnterStage(1);
                else if (_elapsedFrames > InitializationTimeoutFrames)
                    Fail("training runtime did not initialize within 600 frames");
                break;

            case 1: // Stage a recording and save the snapshot that binds it.
                _snapshotPath = ProjectSettings.GlobalizePath("user://balance-trial-snapshot.json");
                if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
                if (!gameLoop!.TrainingRecordingLibrary!.TryAddOrReplace(TrialRecording(), false, null, out string libraryError))
                {
                    Fail($"recording could not be staged: {libraryError}");
                    return;
                }
                if (!gameLoop.TrainingStateService!.TrySave(_snapshotPath, out string saveError))
                {
                    Fail($"snapshot save failed: {saveError}");
                    return;
                }
                EnterStage(2);
                break;

            case 2: // Prepare and start the trial.
                if (gameLoop!.BalanceTrialService!.TryPrepare(
                        new BalanceTrialCandidate(_snapshotPath!, "rtrial", 1, TrialWindowFrames), out string prepareError))
                {
                    if (!gameLoop.BalanceTrialService.TryStart(out string startError))
                    {
                        Fail($"trial start failed: {startError}");
                        return;
                    }
                    EnterStage(3);
                }
                else
                {
                    Fail($"trial prepare failed: {prepareError}");
                }
                break;

            case 3: // Wait for the observation window to complete.
                if (gameLoop!.BalanceTrialService!.State == BalanceTrialState.Completed)
                {
                    BalanceTrialResult? result = gameLoop.BalanceTrialService.Result;
                    if (result is null)
                    {
                        Fail("trial completed without a result");
                        return;
                    }
                    if (result.HashedFrames != TrialWindowFrames)
                    {
                        Fail($"trial hashed {result.HashedFrames} frames; expected {TrialWindowFrames}");
                        return;
                    }
                    if (result.Initiations.Count == 0)
                    {
                        Fail("trial recorded no move initiations");
                        return;
                    }
                    GD.Print($"[BalanceTrialSmoke] PASS: window={result.HashedFrames} damage={result.TotalDamage} " +
                             $"combo={result.MaxComboHits} initiations={result.Initiations.Count} " +
                             $"terminal={result.TerminalMoveId} posX={result.TerminalPositionX} " +
                             $"moves-v{result.Binding.Versions.MoveDatasetVersion} physics-v{result.Binding.Versions.PhysicsDatasetVersion}");
                    GetTree().Quit(0);
                }
                else if (_stageFrames > TrialTimeoutFrames)
                {
                    Fail($"trial did not complete within {TrialTimeoutFrames} frames (state={gameLoop.BalanceTrialService.State})");
                }
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
        GD.PushError($"[BalanceTrialSmoke] {failure}");
        GetTree().Quit(1);
    }

    /// <summary>Neutral held every frame plus three light presses — mirrors live
    /// capture cadence so the runtime input pipeline resolves 5LP initiations.</summary>
    private static TrainingInputRecording TrialRecording()
    {
        var entries = new System.Collections.Generic.List<TrainingInputRecordingEntry>();
        for (int frame = 0; frame <= 90; frame++)
        {
            entries.Add(new TrainingInputRecordingEntry(frame, 0, InputType.Directional, (int)DirectionValue.Neutral));
            if (frame is 0 or 30 or 60)
                entries.Add(new TrainingInputRecordingEntry(frame, 1, InputType.Button, (int)ButtonValue.A));
        }
        return new TrainingInputRecording(1, 1, 90, "rtrial", entries);
    }

    private static System.Collections.Generic.IEnumerable<T> FindNodes<T>(Node node) where T : Node
    {
        if (node is T match) yield return match;
        foreach (Node child in node.GetChildren())
        foreach (T descendant in FindNodes<T>(child))
            yield return descendant;
    }
}
