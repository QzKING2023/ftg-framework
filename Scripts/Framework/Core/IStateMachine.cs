#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

public interface IStateMachine : IModule
{
    CharacterState GetCurrentState(int playerId);
    IReadOnlyList<CharacterState> GetStack(int playerId);
    int GetStackDepth(int playerId);
    void InitializePlayer(int playerId);
    void PushState(int playerId, CharacterState state);
    void PopState(int playerId);
    void ReplaceState(int playerId, CharacterState newState);
    PhysicsResponseProfile GetEffectivePhysicsProfile(int playerId);
    void RegisterStateProfile(CharacterState state, string physicsResponseProfileId);
}
