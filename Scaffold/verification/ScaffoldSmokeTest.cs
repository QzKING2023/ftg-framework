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
    private bool _sawP1AttackState;
    private int _collisionHitCount;
    private HitConnectedEvent _collisionHit;
    private int _collisionDispatchFrame = -1;
    private double _baselineMaximumDelta;
    private double _collisionWindowMaximumDelta;
    private Action<HitConnectedEvent>? _collisionHandler;
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
                initialP1.GlobalPosition = new Vector2(600, 360);
                initialP2.GlobalPosition = new Vector2(650, 360);
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
                inputLog.ShowP1 = true;
                inputLog.ShowP2 = false;
                inputLog.VisibleRowCount = 20;
                _scenarioStarted = true;
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

        InjectKeyAt(30, Key.A);
        InjectKeyAt(35, Key.D);
        InjectKeyAt(40, Key.S);
        InjectKeyAt(45, Key.Space);
        InjectKeyAt(50, Key.U);

        if (_scenarioFrame == 100)
            VerifyAndExit(characters);
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
        if (_collisionHitCount == 1 && _collisionHit.ContactFrame < 0)
            failures.Add($"collision ContactFrame was {_collisionHit.ContactFrame}");
        if (_collisionHitCount == 1 && _collisionHit.ContactFrame != _collisionDispatchFrame)
            failures.Add(
                $"collision contact frame {_collisionHit.ContactFrame} did not match " +
                $"dispatch frame {_collisionDispatchFrame}");
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
        var directions = history?.GetDirectionalHistory(1).Select(entry => entry.Value).ToHashSet() ?? [];
        foreach (var expected in new[]
                 {
                     (int)DirectionValue.Back, (int)DirectionValue.Forward,
                     (int)DirectionValue.Down, (int)DirectionValue.Up
                 })
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
            GD.Print($"[ScaffoldSmoke] PASS: auto-start, input/state flow, one same-frame 5LP collision at frame {_collisionHit.ContactFrame}.");
            GetTree().Quit(0);
            return;
        }

        foreach (var failure in failures)
            GD.PushError($"[ScaffoldSmoke] {failure}");
        if (_collisionHandler is not null)
            EventBus.Instance.Unsubscribe(_collisionHandler);
        GetTree().Quit(1);
    }

    private void InjectKeyAt(int pressFrame, Key key)
    {
        if (_scenarioFrame == pressFrame)
            Godot.Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        else if (_scenarioFrame == pressFrame + 2)
            Godot.Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private void FailAndExit(string failure)
    {
        GD.PushError($"[ScaffoldSmoke] {failure}");
        GetTree().Quit(1);
    }

    private void CaptureVisibleP1Inputs(InputLog inputLog)
    {
        foreach (var expected in ExpectedVisibleP1Inputs())
            if (inputLog.DisplayText.Contains(expected, StringComparison.Ordinal))
                _visibleP1Inputs.Add(expected);
    }

    private static string[] ExpectedVisibleP1Inputs() =>
    [
        $"P1  D  {InputLog.FormatDirection((int)DirectionValue.Back)}",
        $"P1  D  {InputLog.FormatDirection((int)DirectionValue.Forward)}",
        $"P1  D  {InputLog.FormatDirection((int)DirectionValue.Down)}",
        $"P1  D  {InputLog.FormatDirection((int)DirectionValue.Up)}",
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
