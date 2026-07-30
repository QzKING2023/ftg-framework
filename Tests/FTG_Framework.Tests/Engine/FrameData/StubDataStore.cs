#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Tests;

internal sealed class StubDataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves = new();
    private readonly Dictionary<string, GatlingTable> _gatlingTables = new();
    private readonly Dictionary<string, CharacterDefinition> _characters = new();

    public void SetMove(MoveDefinition move) => _moves[move.MoveId] = move;

    public void SetGatlingTable(GatlingTable table) => _gatlingTables[table.CharacterId] = table;

    public MoveDefinition? GetMove(string moveId) =>
        _moves.TryGetValue(moveId, out var move) ? move : null;

    public IReadOnlyList<MoveDefinition> GetAllMoves() => new List<MoveDefinition>(_moves.Values);

    public GatlingTable? GetGatlingTable(string characterId)
    {
        if (characterId is null)
            return null;
        if (_gatlingTables.TryGetValue(characterId, out var table))
            return table;
        return new GatlingTable { CharacterId = characterId };
    }

    public IReadOnlyList<GatlingTable> GetAllGatlingTables() =>
        new List<GatlingTable>(_gatlingTables.Values);

    public CharacterDefinition? GetCharacter(string characterId)
    {
        if (characterId is null)
            return null;
        _characters.TryGetValue(characterId, out var character);
        return character;
    }

    public IReadOnlyList<CharacterDefinition> GetAllCharacters() =>
        new List<CharacterDefinition>(_characters.Values);

    public void RegisterCharacter(CharacterDefinition character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (string.IsNullOrEmpty(character.CharacterId))
            throw new ArgumentException("[Data] Character has null or empty CharacterId.", nameof(character));
        if (!_characters.TryAdd(character.CharacterId, character))
            throw new InvalidOperationException($"[Data] Duplicate CharacterId registration: '{character.CharacterId}'.");
    }

    private readonly Dictionary<string, KnockbackProfile> _knockbackProfiles = new();
    private readonly Dictionary<string, PhysicsResponseProfile> _physicsResponseProfiles = new();

    public KnockbackProfile? GetKnockbackProfile(string profileId)
    {
        if (profileId is null)
            return null;
        _knockbackProfiles.TryGetValue(profileId, out var profile);
        return profile;
    }

    public IReadOnlyList<KnockbackProfile> GetAllKnockbackProfiles() =>
        new List<KnockbackProfile>(_knockbackProfiles.Values);

    public void SetKnockbackProfiles(KnockbackProfile[] profiles)
    {
        _knockbackProfiles.Clear();
        foreach (var p in profiles)
            _knockbackProfiles[p.ProfileId] = p;
    }

    public PhysicsResponseProfile? GetPhysicsResponseProfile(string profileId)
    {
        if (profileId is null)
            return null;
        _physicsResponseProfiles.TryGetValue(profileId, out var profile);
        return profile;
    }

    public IReadOnlyList<PhysicsResponseProfile> GetAllPhysicsResponseProfiles() =>
        new List<PhysicsResponseProfile>(_physicsResponseProfiles.Values);

    public void SetPhysicsResponseProfiles(PhysicsResponseProfile[] profiles)
    {
        _physicsResponseProfiles.Clear();
        foreach (var p in profiles)
            _physicsResponseProfiles[p.ProfileId] = p;
    }

    public void SetPhysicsResponseProfile(PhysicsResponseProfile profile)
    {
        _physicsResponseProfiles[profile.ProfileId] = profile;
    }
}
