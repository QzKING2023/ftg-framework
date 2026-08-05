#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Characters;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.ScaffoldVerification;

public partial class Corr2ResponsiveSmokeTest : Node
{
    private int _frames;
    private bool _started;

    public override void _Process(double delta)
    {
        _frames++;
        var adapter = FindNodes<TrainingPresentationAdapter>(GetTree().Root).FirstOrDefault();
        var characters = FindNodes<CharacterController>(GetTree().Root).OrderBy(c => c.PlayerId).ToArray();
        if (adapter is null || characters.Length != 2)
        {
            if (_frames >= 600) Fail("training presentation did not initialize within 600 frames");
            return;
        }
        if (_started) return;
        _started = true;
        RunValidation(adapter, characters);
    }

    private async void RunValidation(
        TrainingPresentationAdapter adapter, CharacterController[] characters)
    {
        try
        {
            await RunValidationAsync(adapter, characters);
        }
        catch (Exception ex)
        {
            Fail($"validation threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task RunValidationAsync(
        TrainingPresentationAdapter adapter, CharacterController[] characters)
    {
        var failures = new List<string>();
        Window window = GetWindow();
        Vector2[] positions = characters.Select(c => c.GlobalPosition).ToArray();
        ulong epoch = FTG_Framework.Core.EventBus.Instance.LifecycleEpoch;
        foreach ((int width, int height) in new[]
        {
            (960, 540), (1152, 648), (1280, 720), (1920, 1080), (2560, 1080)
        })
        foreach (double uiScale in new[] { 1.0, 1.5, 2.0 })
        {
            window.Size = new Vector2I(width, height);
            adapter.UiScale = uiScale;
            adapter.ApplyLayout();
            WorldPresentationLayout world = adapter.CurrentWorldLayout;
            TrainingUiLayout ui = adapter.CurrentUiLayout;
            if (Math.Abs(world.ScaleX - world.ScaleY) > 0.00001)
                failures.Add($"non-uniform world scale at {width}x{height}");
            if (Math.Abs(world.CameraCenterX - TrainingPresentationLayout.DesignWidth / 2) > 0.00001 ||
                Math.Abs(world.CameraCenterY - TrainingPresentationLayout.DesignHeight / 2) > 0.00001)
                failures.Add($"world camera is not centered at {width}x{height}");
            if (characters.Any(c =>
                    c.GlobalPosition.X < world.VisibleWorld.Left ||
                    c.GlobalPosition.X > world.VisibleWorld.Right ||
                    c.GlobalPosition.Y < world.VisibleWorld.Top ||
                    c.GlobalPosition.Y > world.VisibleWorld.Bottom))
                failures.Add($"character is outside visible world at {width}x{height}");
            if (width == 2560 && height == 1080 &&
                world.VisibleWorld.Width <= TrainingPresentationLayout.DesignWidth)
                failures.Add("ultra-wide viewport did not reveal additional world width");
            if (ui.LeftDiagnostics.Intersects(ui.TopRightTuning) ||
                ui.LeftDiagnostics.Intersects(ui.BottomRightPlayback) ||
                ui.TopRightTuning.Intersects(ui.BottomRightPlayback))
                failures.Add($"region overlap at {width}x{height} ui={uiScale}");
            if (characters.Where((c, i) => c.GlobalPosition != positions[i]).Any())
                failures.Add($"resize mutated authoritative character position at {width}x{height}");
            if (FTG_Framework.Core.EventBus.Instance.LifecycleEpoch != epoch)
                failures.Add($"resize mutated lifecycle epoch at {width}x{height}");
            ValidateControlReachability(adapter, failures, width, height, uiScale);
        }

        if (!string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            Window.ModeEnum originalMode = window.Mode;
            window.Mode = Window.ModeEnum.Fullscreen;
            if (!await WaitForWindowMode(window, Window.ModeEnum.Fullscreen))
                failures.Add("window did not enter fullscreen mode");
            window.Mode = Window.ModeEnum.Windowed;
            if (!await WaitForWindowMode(window, Window.ModeEnum.Windowed))
                failures.Add("window did not return to windowed mode");
            window.Mode = originalMode;
            if (!await WaitForWindowMode(window, originalMode))
                failures.Add($"window did not restore {originalMode} mode");
        }
        else
        {
            GD.Print("[Corr2ResponsiveSmoke] fullscreen transition N/A under headless DisplayServer.");
        }

        int beforeExitSignal = adapter.LayoutRevision;
        Node adapterParent = adapter.GetParent() ??
            throw new InvalidOperationException("training presentation adapter has no parent");
        adapterParent.RemoveChild(adapter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Root.EmitSignal(Viewport.SignalName.SizeChanged);
        if (adapter.LayoutRevision != beforeExitSignal)
            failures.Add("detached adapter still received viewport resize");
        adapterParent.AddChild(adapter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int afterReady = adapter.LayoutRevision;
        GetTree().Root.EmitSignal(Viewport.SignalName.SizeChanged);
        if (adapter.LayoutRevision != afterReady + 1)
            failures.Add("re-entered adapter did not receive exactly one resize callback");

        var actions = new[]
        {
            (TrainingShortcutRouter.RecordToggleAction, JoyButton.Back),
            (TrainingShortcutRouter.PlayOnceAction, JoyButton.Start),
            (TrainingShortcutRouter.LoopToggleAction, JoyButton.LeftShoulder),
            (TrainingShortcutRouter.StopPlaybackAction, JoyButton.RightShoulder)
        };
        foreach ((string action, JoyButton button) in actions)
        {
            if (!InputMap.HasAction(action)) failures.Add($"missing InputMap action {action}");
            if (!InputMap.ActionGetEvents(action).OfType<InputEventKey>().Any(e => e.CtrlPressed && e.ShiftPressed))
                failures.Add($"missing Ctrl+Shift keyboard binding for {action}");
            var controllerBinding = new InputEventJoypadButton { ButtonIndex = button };
            try
            {
                InputMap.ActionAddEvent(action, controllerBinding);
                if (!InputMap.ActionGetEvents(action).OfType<InputEventJoypadButton>()
                        .Any(e => e.ButtonIndex == button) ||
                    !TrainingShortcutRouter.DescribeBindings(action)
                        .Contains(controllerBinding.AsText(), StringComparison.Ordinal))
                    failures.Add($"configured controller binding was not resolved for {action}");
            }
            finally
            {
                InputMap.ActionEraseEvent(action, controllerBinding);
            }
        }

        if (!TrainingShortcutRouter.IsTextEditing(new LineEdit()) ||
            !TrainingShortcutRouter.IsTextEditing(new TextEdit()) ||
            !TrainingShortcutRouter.IsTextEditing(new CodeEdit()))
            failures.Add("text editing focus suppression does not cover all supported editors");
        if (TrainingShortcutRouter.TryResolveSingleCommand(new[]
            {
                TrainingShortcutRouter.RecordToggleAction,
                TrainingShortcutRouter.PlayOnceAction
            }, out _, out string conflict) || !conflict.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            failures.Add("ambiguous shortcut binding was not rejected visibly");

        if (failures.Count == 0)
        {
            GD.Print("[Corr2ResponsiveSmoke] PASS: viewport/UI matrix, authoritative positions, epoch, regions, and InputMap parity.");
            GetTree().Quit(0);
            return;
        }
        foreach (string failure in failures) GD.PushError($"[Corr2ResponsiveSmoke] {failure}");
        GetTree().Quit(1);
    }

    private async System.Threading.Tasks.Task<bool> WaitForWindowMode(
        Window window, Window.ModeEnum expected, int maxFrames = 120)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (window.Mode == expected) return true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return window.Mode == expected;
    }

    private static void ValidateControlReachability(
        TrainingPresentationAdapter adapter,
        ICollection<string> failures,
        int width,
        int height,
        double uiScale)
    {
        TrainingUiLayout ui = adapter.CurrentUiLayout;
        if (adapter.LeftDiagnosticsDrawer is null || adapter.DiagnosticsToggle is null) return;
        if (ui.LeftCollapsed)
        {
            if (adapter.LeftDiagnosticsDrawer.Visible)
                failures.Add($"collapsed drawer starts open at {width}x{height} ui={uiScale}");
            if (!adapter.DiagnosticsToggle.Visible || adapter.DiagnosticsToggle.FocusMode != Control.FocusModeEnum.All)
                failures.Add($"collapsed diagnostics rail is not keyboard reachable at {width}x{height} ui={uiScale}");
            adapter.ToggleDiagnosticsDrawer();
            if (!adapter.LeftDiagnosticsDrawer.Visible)
                failures.Add($"collapsed diagnostics drawer cannot open at {width}x{height} ui={uiScale}");
            adapter.ToggleDiagnosticsDrawer();
        }
        else if (!adapter.LeftDiagnosticsDrawer.Visible)
        {
            failures.Add($"expanded diagnostics drawer is hidden at {width}x{height} ui={uiScale}");
        }
        if (ui.LeftDiagnosticsDrawer.Intersects(ui.TopRightTuning) ||
            ui.LeftDiagnosticsDrawer.Intersects(ui.BottomRightPlayback))
            failures.Add($"diagnostics drawer covers right-side actions at {width}x{height} ui={uiScale}");
    }

    private void Fail(string failure)
    {
        GD.PushError($"[Corr2ResponsiveSmoke] {failure}");
        GetTree().Quit(1);
    }

    private static IEnumerable<T> FindNodes<T>(Node node) where T : Node
    {
        if (node is T match) yield return match;
        foreach (Node child in node.GetChildren())
        foreach (T descendant in FindNodes<T>(child))
            yield return descendant;
    }
}
