using System;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Scenes;
using FTG_Framework.UI.Training;
using Godot;
using Xunit;

namespace FTG_Framework.Tests.Scenes;

public class TrainingSceneTests
{
    [Fact]
    public void TrainingScene_ComposesRenderOnlyWorldAndIndependentScreenSpaceUi()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "Scenes", "TrainingScene.cs"));

        Assert.Contains("WorldPresentationRoot", source);
        Assert.Contains("TrainingPresentationCamera", source);
        Assert.Contains("new Camera2D", source);
        Assert.Contains("TrainingUiLayer", source);
        Assert.Contains("TrainingUiRoot", source);
        Assert.Contains("LeftDiagnosticsRegion", source);
        Assert.Contains("LeftDiagnosticsDrawer", source);
        Assert.Contains("OpenDiagnosticsDrawer", source);
        Assert.Contains("CloseDiagnosticsDrawer", source);
        Assert.Contains("TopRightTuningRegion", source);
        Assert.Contains("BottomRightPlaybackRegion", source);
        Assert.Contains("GameplayLegend", source);
        Assert.Contains("LayoutPreset.BottomWide", source);
        Assert.Contains("leftScroll.OffsetBottom = -148", source);
        Assert.DoesNotContain("leftContent.AddChild(new ControlsLegend())", source);
        Assert.Contains("TrainingPresentationAdapter", source);
        Assert.Contains("TrainingShortcutRouter", source);
        Assert.DoesNotContain("GetViewport().GetVisibleRect()", source);
    }

    [Fact]
    public void ResponsivePanels_DoNotAssignFinalWindowPositions()
    {
        string root = FindRepoRoot();
        string tuning = File.ReadAllText(Path.Combine(root,
            "Scripts", "Framework", "UI", "Training", "RuntimeTuningPanel.cs"));
        string playback = File.ReadAllText(Path.Combine(root,
            "Scripts", "Framework", "UI", "Training", "TrainingInputPlaybackPanel.cs"));

        Assert.DoesNotContain("Position = new Vector2(620, 10)", tuning);
        Assert.DoesNotContain("Position = new Vector2(620, 10)", playback);
        Assert.Contains("LayoutPreset.FullRect", tuning);
        Assert.Contains("LayoutPreset.FullRect", playback);
    }

    [Fact]
    public void PresentationAdapter_UsesPhysicalWindowSizeAndCameraOnly()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training",
            "TrainingPresentationAdapter.cs"));

        Assert.Contains("GetWindow().Size", source);
        Assert.Contains("WorldCamera.Zoom", source);
        Assert.DoesNotContain("GlobalPosition =", source);
        Assert.DoesNotContain("EventBus", source);
        string smoke = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scaffold", "verification", "Corr2ResponsiveSmokeTest.cs"));
        Assert.Contains("adapterParent.RemoveChild(adapter)", smoke);
        Assert.Contains("adapterParent.AddChild(adapter)", smoke);
        Assert.Contains("did not receive exactly one resize callback", smoke);
    }

    [Fact]
    public void TrainingShortcutRouter_UsesGuiAwareEventRoutingWithoutGameplayInjection()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training",
            "TrainingShortcutRouter.cs"));

        Assert.Contains("override void _UnhandledInput", source);
        Assert.Contains("GuiGetFocusOwner()", source);
        Assert.Contains("LineEdit or TextEdit or CodeEdit", source);
        Assert.Contains("allowEcho: false, exactMatch: true", source);
        Assert.Contains("TryResolveSingleCommand", source);
        Assert.Contains("SetInputAsHandled()", source);
        Assert.Contains("InputMap.ActionGetEvents(action)", source);
        Assert.DoesNotContain("ParseInputEvent", source);
        Assert.DoesNotContain("InputReceivedEvent", source);
    }

    [Fact]
    public void TrainingScene_ReleasesGuiFocusOnUnhandledPrimaryClickOnly()
    {
        Assert.True(TrainingScene.ShouldReleaseGuiFocus(
            MouseButton.Left, pressed: true, hasFocusOwner: true));
        Assert.False(TrainingScene.ShouldReleaseGuiFocus(
            MouseButton.Left, pressed: false, hasFocusOwner: true));
        Assert.False(TrainingScene.ShouldReleaseGuiFocus(
            MouseButton.Right, pressed: true, hasFocusOwner: true));
        Assert.False(TrainingScene.ShouldReleaseGuiFocus(
            MouseButton.Left, pressed: true, hasFocusOwner: false));
    }

    [Fact]
    public void RecordingConfirmation_DoesNotForceFocusBackOntoRecordToggle()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training",
            "TrainingInputPlaybackPanel.cs"));

        Assert.DoesNotContain("_recordToggle?.GrabFocus()", source);
    }

    [Fact]
    public void TryCalculateCharacterSpawnPositions_StandardViewport_CentersVisiblePair()
    {
        var visibleRect = new Rect2(Vector2.Zero, new Vector2(1152, 648));

        var supported = TrainingScene.TryCalculateCharacterSpawnPositions(
            visibleRect, out var p1, out var p2);

        Assert.True(supported);
        Assert.Equal(new Vector2(376, 324), p1);
        Assert.Equal(new Vector2(776, 324), p2);
        AssertLabelsContained(visibleRect, p1, p2);
    }

    [Fact]
    public void TryCalculateCharacterSpawnPositions_MinimumViewport_ContainsLabelsAtBoundaries()
    {
        var visibleRect = new Rect2(Vector2.Zero, new Vector2(500, 120));

        var supported = TrainingScene.TryCalculateCharacterSpawnPositions(
            visibleRect, out var p1, out var p2);

        Assert.True(supported);
        Assert.Equal(new Vector2(50, 60), p1);
        Assert.Equal(new Vector2(450, 60), p2);
        AssertLabelsContained(visibleRect, p1, p2);
    }

    [Fact]
    public void TryCalculateCharacterSpawnPositions_NonzeroOrigin_OffsetsBothPositions()
    {
        var visibleRect = new Rect2(new Vector2(100, 40), new Vector2(800, 400));

        var supported = TrainingScene.TryCalculateCharacterSpawnPositions(
            visibleRect, out var p1, out var p2);

        Assert.True(supported);
        Assert.Equal(new Vector2(300, 240), p1);
        Assert.Equal(new Vector2(700, 240), p2);
        AssertLabelsContained(visibleRect, p1, p2);
    }

    [Theory]
    [InlineData(499, 120)]
    [InlineData(500, 119)]
    [InlineData(0, 120)]
    [InlineData(500, 0)]
    [InlineData(-1, 120)]
    [InlineData(500, -1)]
    [InlineData(float.NaN, 120)]
    [InlineData(500, float.NaN)]
    [InlineData(float.PositiveInfinity, 120)]
    [InlineData(500, float.PositiveInfinity)]
    public void TryCalculateCharacterSpawnPositions_UnsupportedViewport_RejectsLayout(
        float width, float height)
    {
        var visibleRect = new Rect2(Vector2.Zero, new Vector2(width, height));

        var supported = TrainingScene.TryCalculateCharacterSpawnPositions(
            visibleRect, out var p1, out var p2);

        Assert.False(supported);
        Assert.Equal(Vector2.Zero, p1);
        Assert.Equal(Vector2.Zero, p2);
    }

    [Fact]
    public void CharacterTemplate_LabelBoundsMatchLayoutContract()
    {
        var template = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Characters", "character_template.tscn"));

        Assert.Contains("offset_left = -50.0", template);
        Assert.Contains("offset_top = -60.0", template);
        Assert.Contains("offset_right = 50.0", template);
        Assert.Contains("offset_bottom = -40.0", template);
    }

    [Fact]
    public void RuntimeTuningPanel_FocusedControlsRequestDeferredScrollVisibility()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training", "RuntimeTuningPanel.cs"));

        Assert.Contains("FocusEntered += () => EnsureFocusVisible(control)", source);
        Assert.Contains("CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, control)", source);
        Assert.Contains("RestoreFocus(_editors.Values.FirstOrDefault())", source);
        Assert.Contains("RestoreFocus(_apply)", source);
        Assert.Contains("RebuildFocusNavigation()", source);
        Assert.Contains("control.FocusNeighborTop", source);
        Assert.Contains("control.FocusNeighborBottom", source);
        Assert.Contains("control.FocusPrevious", source);
        Assert.Contains("control.FocusNext", source);
        Assert.Contains("control.GetPathTo(previous)", source);
        Assert.Contains("control.GetPathTo(next)", source);
    }

    [Fact]
    public void TrainingScene_ComposesAndExplicitlyShutsDownComboDisplay()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "Scenes", "TrainingScene.cs"));

        Assert.Contains("_comboDisplay = new ComboDisplay", source);
        Assert.Contains("AddChild(_comboDisplay)", source);
        Assert.Contains("_comboDisplay?.Shutdown()", source);
        Assert.Contains("_comboDisplay = null", source);
    }

    [Fact]
    public void TrainingScene_ComposesAndShutsDownTrainingInputPlaybackPanel()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "Scenes", "TrainingScene.cs"));
        Assert.Contains("_trainingInputPanel = new TrainingInputPlaybackPanel", source);
        Assert.Contains("AddChild(_trainingInputPanel)", source);
        Assert.Contains("_trainingInputPanel?.Shutdown()", source);
        Assert.Contains("TrainingInputService?.CancelForLifecycle()", source);
    }

    [Fact]
    public void TrainingInputPanel_UsesFocusNavigationScalingAndNonColorStatus()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training", "TrainingInputPlaybackPanel.cs"));
        Assert.Contains("ScrollContainer", source);
        Assert.DoesNotContain("CustomMinimumSize = new Vector2(390, 300)", source);
        Assert.Contains("FocusNeighborTop", source);
        Assert.Contains("FocusNeighborBottom", source);
        Assert.DoesNotContain("new SpinBox", source);
        Assert.DoesNotContain("Stop / Name", source);
        Assert.DoesNotContain("Confirm Replace", source);
        Assert.Contains("Start Recording", source);
        Assert.Contains("ConfirmationDialog", source);
        Assert.Contains("ExecuteRecordToggleCommand(_vm", source);
        Assert.Contains("return viewModel.ToggleCapture(name)", source);
        Assert.Contains("TrainingShortcutCommand.RecordToggle => ExecuteRecordToggle()", source);
        Assert.Contains("StatusText", source);
        Assert.Contains("ui_accept", source);
    }

    [Fact]
    public void TrainingScene_ComposesObserverOnlyTestPresentationAndLegend()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "Scenes", "TrainingScene.cs"));
        Assert.Contains("new TestMatchPresentation", source);
        Assert.Contains("new ControlsLegend", source);
        Assert.Contains("Key.Bracketright", source);
        Assert.Contains("Key.Bracketleft", source);
        Assert.DoesNotContain("Key.Right);\n        bool stepBack", source);
    }

    [Fact]
    public void EventBusDebugPanel_DoesNotConsumeP2ArrowKeys()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training", "EventBusDebugPanel.cs"));
        Assert.DoesNotContain("Key.Down", source);
        Assert.DoesNotContain("Key.Up", source);
        Assert.Contains("Key.Pagedown", source);
        Assert.Contains("Key.Pageup", source);
    }

    [Fact]
    public void ComboDisplay_IsThinReadableAdapterWithDeterministicText()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Scripts", "Framework", "UI", "Training", "ComboDisplay.cs"));

        Assert.Contains("new ComboDisplayViewModel", source);
        Assert.Contains("new ComboDisplayController", source);
        Assert.Contains("CustomMinimumSize", source);
        Assert.Contains("AutowrapMode", source);
        Assert.DoesNotContain("DataStore", source);
        Assert.Equal("P1 Combo: 2 Hits | Damage: 110", ComboDisplay.FormatDisplayText(
            1, new FTG_Framework.UI.Training.ViewModels.ComboDisplayState(2, 110)));
        Assert.Equal("P2 Combo: 0 Hits | Damage: 0", ComboDisplay.FormatDisplayText(
            2, FTG_Framework.UI.Training.ViewModels.ComboDisplayState.Empty));
    }

    [Fact]
    public void TrainingRecovery_HitAndBlockUseExactProcessedFrameDurations()
    {
        var state = new RecoveryStateMachine();
        var recovery = new TrainingStateRecovery();

        state.Current = CharacterState.Hitstun;
        recovery.Start(CharacterState.Hitstun);
        for (int i = 0; i < 29; i++) recovery.AdvanceProcessedFrame(state);
        Assert.Equal(CharacterState.Hitstun, state.Current);
        recovery.AdvanceProcessedFrame(state);
        Assert.Equal(CharacterState.Idle, state.Current);
        Assert.Equal(1, state.PopCount);

        state.Current = CharacterState.Blockstun;
        recovery.Start(CharacterState.Blockstun);
        for (int i = 0; i < 19; i++) recovery.AdvanceProcessedFrame(state);
        Assert.Equal(CharacterState.Blockstun, state.Current);
        recovery.AdvanceProcessedFrame(state);
        Assert.Equal(CharacterState.Idle, state.Current);
        Assert.Equal(2, state.PopCount);
    }

    [Fact]
    public void TrainingRecovery_PauseAndReplacementDoNotConsumeOrResetStaleState()
    {
        var state = new RecoveryStateMachine { Current = CharacterState.Hitstun };
        var recovery = new TrainingStateRecovery();
        recovery.Start(CharacterState.Hitstun);

        // Paused wall-clock frames do not call AdvanceProcessedFrame.
        for (int i = 0; i < 30; i++) { }
        Assert.Equal(CharacterState.Hitstun, state.Current);

        recovery.AdvanceProcessedFrame(state);
        state.Current = CharacterState.Knockdown;
        for (int i = 0; i < 40; i++) recovery.AdvanceProcessedFrame(state);
        Assert.Equal(CharacterState.Knockdown, state.Current);
    }

    [Fact]
    public void TrainingRecovery_SceneExitRestoresOnlyOwnedState()
    {
        var state = new RecoveryStateMachine { Current = CharacterState.Hitstun };
        var recovery = new TrainingStateRecovery();
        recovery.Start(CharacterState.Hitstun);

        recovery.RestoreIfOwned(state);
        Assert.Equal(CharacterState.Idle, state.Current);
        Assert.Equal(1, state.PopCount);

        state.Current = CharacterState.Knockdown;
        recovery.Start(CharacterState.Hitstun);
        recovery.RestoreIfOwned(state);
        Assert.Equal(CharacterState.Knockdown, state.Current);
        Assert.Equal(1, state.PopCount);
    }

    private static void AssertLabelsContained(Rect2 visibleRect, Vector2 p1, Vector2 p2)
    {
        Assert.Equal(400, p2.X - p1.X);
        Assert.True(p1.X < p2.X);

        foreach (var position in new[] { p1, p2 })
        {
            Assert.True(position.X - 50 >= visibleRect.Position.X);
            Assert.True(position.X + 50 <= visibleRect.End.X);
            Assert.True(position.Y - 60 >= visibleRect.Position.Y);
            Assert.True(position.Y - 40 <= visibleRect.End.Y);
        }
    }

    private static string FindRepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "project.godot")))
        {
            current = Directory.GetParent(current)?.FullName
                ?? throw new InvalidOperationException("Cannot find FTG Framework repository root.");
        }

        return current;
    }

    private sealed class RecoveryStateMachine : IStateMachine
    {
        public CharacterState Current { get; set; } = CharacterState.Idle;
        public int PopCount { get; private set; }
        public CharacterState GetCurrentState(int playerId) => Current;
        public System.Collections.Generic.IReadOnlyList<CharacterState> GetStack(int playerId) => new[] { Current };
        public int GetStackDepth(int playerId) => 1;
        public void InitializePlayer(int playerId) { }
        public void PushState(int playerId, CharacterState state) => Current = state;
        public void PopState(int playerId)
        {
            PopCount++;
            Current = CharacterState.Idle;
        }
        public void ReplaceState(int playerId, CharacterState newState) => Current = newState;
        public FTG_Framework.Data.PhysicsResponseProfile GetEffectivePhysicsProfile(int playerId) => new();
        public void RegisterStateProfile(CharacterState state, string physicsResponseProfileId) { }
        public void Initialize(IDataStore dataStore) { }
        public void Shutdown() { }
    }
}
