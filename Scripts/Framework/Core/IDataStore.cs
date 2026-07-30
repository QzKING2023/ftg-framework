#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

public interface IDataStore
{
    MoveDefinition? GetMove(string moveId);
    IReadOnlyList<MoveDefinition> GetAllMoves();
    GatlingTable? GetGatlingTable(string characterId);
    IReadOnlyList<GatlingTable> GetAllGatlingTables();
    void SetGatlingTable(GatlingTable table);
    CharacterDefinition? GetCharacter(string characterId);
    IReadOnlyList<CharacterDefinition> GetAllCharacters();
    void RegisterCharacter(CharacterDefinition character);
    KnockbackProfile? GetKnockbackProfile(string profileId);
    IReadOnlyList<KnockbackProfile> GetAllKnockbackProfiles();
    void SetKnockbackProfiles(KnockbackProfile[] profiles);
    PhysicsResponseProfile? GetPhysicsResponseProfile(string profileId);
    IReadOnlyList<PhysicsResponseProfile> GetAllPhysicsResponseProfiles();
    void SetPhysicsResponseProfiles(PhysicsResponseProfile[] profiles);
}
