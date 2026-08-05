#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Characters;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.ScaffoldVerification;

public partial class ScaffoldSmokeTest : Node
{
    private const int InitializationTimeoutFrames = 600;
    private int _elapsedFrames;
    private int _scenarioFrame;
    private bool _scenarioStarted;
    private int _stage;
    private int _stageFrame;
    private Key _p2BackKey;
    private bool _sawP1AttackState;
    private int _collisionHitCount;
    private int _collisionBlockCount;
    private HitConnectedEvent _collisionHit;
    private int _collisionDispatchFrame = -1;
    private double _baselineMaximumDelta;
    private double _collisionWindowMaximumDelta;
    private Action<HitConnectedEvent>? _collisionHandler;
    private Action<KnockbackAppliedEvent>? _knockbackHandler;
    private int _knockbackEventCount;
    private bool _knockbackCompleted;
    private float _initialP2X;
    private float _finalP2X;
    private bool _hotReloadRequested;
    private string? _profilePath;
    private string? _originalProfileJson;
    private readonly HashSet<string> _visibleP1Inputs = new(StringComparer.Ordinal);

    public override void _EnterTree()
    {
        GetTree().Root.Size = new Vector2I(1280, 720);
    }

    public override void _Process(double delta)
    {
        _elapsedFrames++;
        var characters = FindNodes<CharacterController>(GetTree().Root).ToArray();
        var gameLoop = GetNodeOrNull<GameLoop>("/root/GameLoop");
        var inputLog = FindNodes<InputLog>(GetTree().Root).FirstOrDefault();

        if (!_scenarioStarted)
        {
            if (characters.Length == 2 && gameLoop?.InputHistory is not null && inputLog is not null)
            {
                var initialP1 = characters.Single(character => character.PlayerId == 1);
                var initialP2 = characters.Single(character => character.PlayerId == 2);
                _initialP2X = initialP2.GlobalPosition.X;
                _collisionHandler = hit =>
                {
                    if (hit.AttackerId == 1 && hit.DefenderId == 2 && hit.MoveId == "5LP")
                    {
                        _collisionHitCount++;
                        _collisionHit = hit;
                        _collisionDispatchFrame = EventBus.Instance.CurrentFrame - 1;
                    }
                };
                EventBus.Instance.Subscribe(_collisionHandler);
                EventBus.Instance.Subscribe<MoveBlockedEvent>(OnMoveBlocked);
                _knockbackHandler = applied =>
                {
                    if (applied.PlayerId != 2 || !applied.WorldX.HasValue)
                        return;
                    _knockbackEventCount++;
                    _finalP2X = applied.WorldX.Value;
                    _knockbackCompleted |= applied.Phase == KnockbackPhase.Completed;
                };
                EventBus.Instance.Subscribe(_knockbackHandler);
                inputLog.ShowP1 = true;
                inputLog.ShowP2 = false;
                inputLog.VisibleRowCount = 20;
                _profilePath = ProjectSettings.GlobalizePath(
                    "res://Scripts/Framework/Data/example_knockback_profiles.json");
                _originalProfileJson = System.IO.File.ReadAllText(_profilePath);
                string updatedProfileJson = _originalProfileJson.Replace(
                    "\"horizontal\": 0.5",
                    "\"horizontal\": 1.5",
                    StringComparison.Ordinal);
                if (updatedProfileJson == _originalProfileJson)
                {
                    FailAndExit("light_hit source profile was not at the expected baseline value");
                    return;
                }
                System.IO.File.WriteAllText(
                    _profilePath,
                    updatedProfileJson);
                _hotReloadRequested = true;
                _scenarioStarted = true;
                _stage = 1;
                return;
            }

            if (_elapsedFrames >= InitializationTimeoutFrames)
                FailAndExit("training did not initialize within 600 frames");
            return;
        }

        _scenarioFrame++;
        if (_scenarioFrame is >= 10 and < 50)
            _baselineMaximumDelta = Math.Max(_baselineMaximumDelta, delta);
        else if (_scenarioFrame is >= 50 and <= 60)
            _collisionWindowMaximumDelta = Math.Max(_collisionWindowMaximumDelta, delta);
        var p1 = characters.FirstOrDefault(character => character.PlayerId == 1);
        _sawP1AttackState |=
            p1?.StateDebugLabel?.Text.Contains("Attack", StringComparison.Ordinal) == true;
        CaptureVisibleP1Inputs(inputLog!);

        DriveScenario(characters, gameLoop!);

        bool settledAfterBlock = _collisionBlockCount == 1 &&
            gameLoop?.StateMachine?.GetCurrentState(1) == CharacterState.Idle &&
            gameLoop.StateMachine.GetCurrentState(2) == CharacterState.Idle;
        if (settledAfterBlock || _scenarioFrame == 500)
            VerifyAndExit(characters);
    }

    private void DriveScenario(CharacterController[] characters, GameLoop gameLoop)
    {
        var p1 = characters.Single(character => character.PlayerId == 1);
        var p2 = characters.Single(character => character.PlayerId == 2);
        float gap = Math.Abs(p2.GlobalPosition.X - p1.GlobalPosition.X);
        _stageFrame++;

        switch (_stage)
        {
            case 1:
                if (_stageFrame == 1) SetKey(Key.D, true);
                if (gap <= 45f)
                {
                    SetKey(Key.D, false);
                    AdvanceStage(2);
                }
                break;
            case 2:
                if (_stageFrame == 3) SetKey(Key.U, true);
                if (_stageFrame == 5)
                {
                    SetKey(Key.U, false);
                    AdvanceStage(3);
                }
                break;
            case 3:
                if (_collisionHitCount == 1 && _knockbackCompleted &&
                    gameLoop.StateMachine?.GetCurrentState(1) == CharacterState.Idle &&
                    gameLoop.StateMachine.GetCurrentState(2) == CharacterState.Idle)
                {
                    SetKey(p1.GlobalPosition.X < p2.GlobalPosition.X ? Key.D : Key.A, true);
                    AdvanceStage(4);
                }
                break;
            case 4:
                if (gap <= 28f)
                {
                    SetKey(p1.GlobalPosition.X < p2.GlobalPosition.X ? Key.D : Key.A, false);
                    _p2BackKey = p2.GlobalPosition.X > p1.GlobalPosition.X ? Key.Right : Key.Left;
                    SetKey(_p2BackKey, true);
                    AdvanceStage(5);
                }
                break;
            case 5:
                if (_stageFrame == 2)
                {
                    int direction = gameLoop.InputHistory?.GetDirectionalHistory(2).LastOrDefault().Value ?? -1;
                    GD.Print($"[ScaffoldSmoke] block setup: gap={gap:F1}, p1X={p1.GlobalPosition.X:F1}, " +
                        $"p2X={p2.GlobalPosition.X:F1}, p2Dir={(DirectionValue)direction}, " +
                        $"p2State={gameLoop.StateMachine?.GetCurrentState(2)}, " +
                        $"Key.Left={(long)Key.Left}, Key.Right={(long)Key.Right}");
                    SetKey(Key.U, true);
                }
                if (_stageFrame == 4) SetKey(Key.U, false);
                if (_stageFrame == 8)
                {
                    SetKey(_p2BackKey, false);
                    AdvanceStage(6);
                }
                break;
        }
    }

    private void AdvanceStage(int next)
    {
        _stage = next;
        _stageFrame = 0;
    }

    private void VerifyAndExit(CharacterController[] characters)
    {
        var failures = new List<string>();
        if (characters.Length != 2)
            failures.Add($"expected 2 CharacterController nodes, found {characters.Length}");

        var labels = characters.Select(character => character.StateDebugLabel?.Text ?? "").ToArray();
        if (!labels.Contains("[P1] Idle", StringComparer.Ordinal))
            failures.Add($"missing final [P1] Idle label ({string.Join(", ", labels)})");
        if (!labels.Contains("[P2] Idle", StringComparer.Ordinal))
            failures.Add($"missing [P2] Idle label ({string.Join(", ", labels)})");
        if (!_sawP1AttackState)
            failures.Add("P1 label never exposed an attack state after U/5LP");
        if (_collisionHitCount != 1)
            failures.Add($"expected exactly one 5LP collision event, observed {_collisionHitCount}");
        if (_collisionBlockCount != 1)
            failures.Add($"expected exactly one real P2 Back block, observed {_collisionBlockCount}");
        if (_collisionHitCount == 1 && _collisionHit.ContactFrame < 0)
            failures.Add($"collision ContactFrame was {_collisionHit.ContactFrame}");
        if (_collisionHitCount == 1 && _collisionHit.ContactFrame != _collisionDispatchFrame)
            failures.Add(
                $"collision contact frame {_collisionHit.ContactFrame} did not match " +
                $"dispatch frame {_collisionDispatchFrame}");
        if (_knockbackEventCount == 0)
            failures.Add("expected visible P2 knockback position events");
        if (_finalP2X <= _initialP2X)
            failures.Add($"P2 did not move away from P1 ({_initialP2X} -> {_finalP2X})");
        if (!_knockbackCompleted)
            failures.Add("knockback trajectory never published the Completed phase");
        if (Godot.Engine.PhysicsTicksPerSecond != 60)
            failures.Add(
                $"physics tick rate was {Godot.Engine.PhysicsTicksPerSecond}, expected 60 Hz");
        var collisionFrameBudget = Math.Max(2.0 / 60.0, _baselineMaximumDelta * 2.0);
        if (_collisionWindowMaximumDelta > collisionFrameBudget)
            failures.Add(
                $"collision window observed a {_collisionWindowMaximumDelta * 1000.0:F2} ms frame " +
                $"against {_baselineMaximumDelta * 1000.0:F2} ms baseline");

        var gameLoop = GetNodeOrNull<GameLoop>("/root/GameLoop");
        var history = gameLoop?.InputHistory;
        if (!_hotReloadRequested)
            failures.Add("physics profile hot-reload was not requested");
        var reloadedProfile = gameLoop?.DataStore?.GetKnockbackProfile("light_hit");
        if (reloadedProfile?.Horizontal != 1.5f)
            failures.Add(
                $"light_hit hot-reload was not visible (horizontal={reloadedProfile?.Horizontal})");
        var directions = history?.GetDirectionalHistory(1).Select(entry => entry.Value).ToHashSet() ?? [];
        foreach (var expected in new[] { (int)DirectionValue.Forward })
        {
            if (!directions.Contains(expected))
                failures.Add($"directional history missing {(DirectionValue)expected}");
        }
        var buttons = history?.GetButtonHistory(1).Select(entry => entry.Value).ToHashSet() ?? [];
        if (!buttons.Contains((int)ButtonValue.A))
            failures.Add("button history missing A from U/5LP");

        var inputLog = FindNodes<InputLog>(GetTree().Root).FirstOrDefault();
        if (inputLog is null)
        {
            failures.Add("visible P1 InputLog was not found");
        }
        else
        {
            if (inputLog.TrackedPlayer != 1)
                failures.Add($"visible InputLog tracks P{inputLog.TrackedPlayer}, expected P1");
            foreach (var expected in ExpectedVisibleP1Inputs())
            {
                if (!_visibleP1Inputs.Contains(expected))
                    failures.Add($"visible P1 InputLog missing '{expected}'");
            }
        }

        if (failures.Count == 0)
        {
            if (_collisionHandler is not null)
                EventBus.Instance.Unsubscribe(_collisionHandler);
            if (_knockbackHandler is not null)
                EventBus.Instance.Unsubscribe(_knockbackHandler);
            EventBus.Instance.Unsubscribe<MoveBlockedEvent>(OnMoveBlocked);
            GD.Print($"[ScaffoldSmoke] PASS: production movement reached range; one real 5LP hit and one facing-relative P2 Back block; P2 knockback {_initialP2X:F1}->{_finalP2X:F1}.");
            GetTree().Quit(0);
            return;
        }

        foreach (var failure in failures)
            GD.PushError($"[ScaffoldSmoke] {failure}");
        if (_collisionHandler is not null)
            EventBus.Instance.Unsubscribe(_collisionHandler);
        if (_knockbackHandler is not null)
            EventBus.Instance.Unsubscribe(_knockbackHandler);
        EventBus.Instance.Unsubscribe<MoveBlockedEvent>(OnMoveBlocked);
        GetTree().Quit(1);
    }

    private static void SetKey(Key key, bool pressed) =>
        Godot.Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key, PhysicalKeycode = key, Pressed = pressed
        });

    private void OnMoveBlocked(MoveBlockedEvent blocked)
    {
        if (blocked.AttackerId == 1 && blocked.DefenderId == 2 && blocked.MoveId == "5LP")
            _collisionBlockCount++;
    }

    private void FailAndExit(string failure)
    {
        GD.PushError($"[ScaffoldSmoke] {failure}");
        GetTree().Quit(1);
    }

    public override void _ExitTree()
    {
        if (_profilePath is not null && _originalProfileJson is not null)
            System.IO.File.WriteAllText(_profilePath, _originalProfileJson);
    }

    private void CaptureVisibleP1Inputs(InputLog inputLog)
    {
        foreach (var expected in ExpectedVisibleP1Inputs())
            if (inputLog.DisplayText.Contains(expected, StringComparison.Ordinal))
                _visibleP1Inputs.Add(expected);
    }

    private static string[] ExpectedVisibleP1Inputs() =>
    [
        $"P1  D  {InputLog.FormatDirection((int)DirectionValue.Forward)}",
        $"P1  B  {InputLog.FormatButton((int)ButtonValue.A)}"
    ];

    private static IEnumerable<T> FindNodes<T>(Node node) where T : Node
    {
        if (node is T match)
            yield return match;
        foreach (var child in node.GetChildren())
            foreach (var descendant in FindNodes<T>(child))
                yield return descendant;
    }
}
