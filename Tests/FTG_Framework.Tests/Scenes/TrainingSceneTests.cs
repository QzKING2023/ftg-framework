using System;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Scenes;
using Godot;
using Xunit;

namespace FTG_Framework.Tests.Scenes;

public class TrainingSceneTests
{
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
