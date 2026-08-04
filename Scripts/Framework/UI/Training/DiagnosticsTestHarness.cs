#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.UI.Training;

public readonly record struct DiagnosticsPolicy(bool SettingEnabled, bool DebugBuild)
{
    public bool MutationEnabled => SettingEnabled && DebugBuild;
}

public readonly record struct DiagnosticsResult(bool Accepted, string Message);

public sealed class DiagnosticsTestHarness
{
    public const string TestModeText = "TEST MODE — injected outcomes are not gameplay evidence";
    public const string InjectionText = "TEST ONLY: injected state; not collision/gameplay evidence";
    private readonly DiagnosticsPolicy _policy;
    private CharacterState? _ownedState;

    public DiagnosticsTestHarness(DiagnosticsPolicy policy) => _policy = policy;

    public DiagnosticsResult TryInjectP2(IStateMachine stateMachine, CharacterState state)
    {
        if (!_policy.MutationEnabled)
            return new DiagnosticsResult(false,
                "[Diagnostics] Injection rejected: test harness requires enabled project setting and debug build.");
        if (state is not CharacterState.Hitstun and not CharacterState.Blockstun)
            return new DiagnosticsResult(false, "[Diagnostics] Injection rejected: unsupported state.");
        stateMachine.ReplaceState(2, state);
        _ownedState = state;
        return new DiagnosticsResult(true, $"[Diagnostics] {InjectionText}");
    }

    public void Reset(IStateMachine stateMachine)
    {
        if (_ownedState is { } owned && stateMachine.GetCurrentState(2) == owned)
            stateMachine.ReplaceState(2, CharacterState.Idle);
        _ownedState = null;
    }
}
