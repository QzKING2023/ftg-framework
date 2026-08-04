using FTG_Framework.Core;
using FTG_Framework.UI.Training;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

public sealed class DiagnosticsTestHarnessTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Injection_IsRejectedUnlessSettingAndDebugBuildAreTrue(bool setting, bool debug)
    {
        var state = new StubStateMachine();
        var harness = new DiagnosticsTestHarness(new DiagnosticsPolicy(setting, debug));
        var result = harness.TryInjectP2(state, CharacterState.Hitstun);
        Assert.False(result.Accepted);
        Assert.Equal(CharacterState.Idle, state.Current);
        Assert.StartsWith("[Diagnostics]", result.Message);
    }

    [Fact]
    public void Injection_IsExplicitlyTestOnly_AndResetDoesNotClearNewerState()
    {
        var state = new StubStateMachine();
        var harness = new DiagnosticsTestHarness(new DiagnosticsPolicy(true, true));
        var result = harness.TryInjectP2(state, CharacterState.Blockstun);
        Assert.True(result.Accepted);
        Assert.Contains("TEST ONLY: injected state; not collision/gameplay evidence", result.Message);
        state.Current = CharacterState.Knockdown;
        harness.Reset(state);
        Assert.Equal(CharacterState.Knockdown, state.Current);
    }

    private sealed class StubStateMachine : IStateMachine
    {
        public CharacterState Current { get; set; } = CharacterState.Idle;
        public CharacterState GetCurrentState(int playerId) => Current;
        public System.Collections.Generic.IReadOnlyList<CharacterState> GetStack(int playerId) => [Current];
        public int GetStackDepth(int playerId) => 1;
        public void InitializePlayer(int playerId) { }
        public void PushState(int playerId, CharacterState state) => Current = state;
        public void PopState(int playerId) => Current = CharacterState.Idle;
        public void ReplaceState(int playerId, CharacterState newState) => Current = newState;
        public FTG_Framework.Data.PhysicsResponseProfile GetEffectivePhysicsProfile(int playerId) => new();
        public void RegisterStateProfile(CharacterState state, string physicsResponseProfileId) { }
        public void Initialize(IDataStore dataStore) { }
        public void Shutdown() { }
    }
}
