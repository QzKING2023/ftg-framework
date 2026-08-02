#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;

namespace FTG_Framework.Data;

internal sealed class DataStore : IDataStore
{
    private MoveDatasetState _moveDataset;
    private readonly Dictionary<string, GatlingTable> _gatlingTables;
    private readonly Dictionary<string, CharacterDefinition> _characters;
    private Dictionary<string, KnockbackProfile> _knockbackProfiles;
    private Dictionary<string, PhysicsResponseProfile> _physicsResponseProfiles;
    private readonly object _physicsDatasetSync = new();
    private ulong _physicsDatasetVersion;
    private readonly object _moveDatasetSync = new();
    private ulong _moveDatasetVersion;
    private MoveContentIdentity _moveContentIdentity = new(string.Empty);
    private DataContentIdentity _knockbackContentIdentity = new(string.Empty);
    private DataContentIdentity _responseContentIdentity = new(string.Empty);

    internal ulong MoveDatasetVersion
    {
        get { lock (_moveDatasetSync) return _moveDatasetVersion; }
    }

    internal ulong PhysicsDatasetVersion
    {
        get { lock (_physicsDatasetSync) return _physicsDatasetVersion; }
    }

    internal void ObserveMoveContentIdentity(MoveContentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (_moveDatasetSync) _moveContentIdentity = identity;
    }

    internal void ObservePhysicsContentIdentity(PhysicsDocumentKind kind, DataContentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (_physicsDatasetSync)
        {
            if (kind == PhysicsDocumentKind.Knockback) _knockbackContentIdentity = identity;
            else _responseContentIdentity = identity;
        }
    }

    internal MoveDatasetBaseline CaptureMoveBaseline()
    {
        lock (_moveDatasetSync)
            return new MoveDatasetBaseline(_moveDataset.Moves.Values.ToArray(), _moveDatasetVersion, _moveContentIdentity);
    }

    internal PhysicsDatasetBaseline CapturePhysicsBaseline()
    {
        lock (_physicsDatasetSync)
            return new PhysicsDatasetBaseline(_knockbackProfiles.Values.ToArray(),
                _physicsResponseProfiles.Values.ToArray(), _physicsDatasetVersion,
                _knockbackContentIdentity, _responseContentIdentity);
    }

    public DataStore(
        MoveDefinition[] moves,
        GatlingTable[]? gatlingTables = null,
        KnockbackProfile[]? knockbackProfiles = null,
        PhysicsResponseProfile[]? physicsResponseProfiles = null)
    {
        ArgumentNullException.ThrowIfNull(moves);
        _moveDataset = BuildMoveCandidate(moves);

        _gatlingTables = new Dictionary<string, GatlingTable>();
        if (gatlingTables is not null)
        {
            foreach (var table in gatlingTables)
                _gatlingTables[table.CharacterId] = ValidateTable(table);
        }

        _characters = new Dictionary<string, CharacterDefinition>();

        _knockbackProfiles = new Dictionary<string, KnockbackProfile>();
        _physicsResponseProfiles = new Dictionary<string, PhysicsResponseProfile>();
        if (knockbackProfiles is not null)
            SetKnockbackProfiles(knockbackProfiles);
        if (physicsResponseProfiles is not null)
            SetPhysicsResponseProfiles(physicsResponseProfiles);
    }

    public void SetGatlingTable(GatlingTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(table.CharacterId))
            throw new FormatException("[Data] Gatling table has null or empty character_id.");
        _gatlingTables[table.CharacterId] = ValidateTable(table);
    }

    private GatlingTable ValidateTable(GatlingTable table)
    {
        MoveDatasetState moveDataset;
        lock (_moveDatasetSync)
            moveDataset = _moveDataset;
        if (table.Entries is null)
            throw new FormatException($"[Data] Gatling table '{table.CharacterId}': entries is null.");

        var validatedEntries = new List<GatlingEntry>();
        foreach (var entry in table.Entries)
        {
            if (entry.TargetMoves is null)
                throw new FormatException($"[Data] Gatling table '{table.CharacterId}': entry for source_move '{entry.SourceMove}' has null target_moves.");

            if (!moveDataset.Moves.ContainsKey(entry.SourceMove))
            {
                FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': source_move '{entry.SourceMove}' not found in registered moves. Entry ignored.");
                continue;
            }

            if (!moveDataset.KnownCategories.Contains(entry.CancelCategory))
            {
                FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': cancel_category '{entry.CancelCategory}' in entry for '{entry.SourceMove}' not found in any registered move's cancel windows. Entry ignored.");
                continue;
            }

            if (entry.TargetMoves.Contains(entry.SourceMove))
            {
                if (!moveDataset.Moves.TryGetValue(entry.SourceMove, out var moveDef) || !moveDef.ChainRepeatable)
                    throw new FormatException($"[Data] Gatling table '{table.CharacterId}': self-cancel entry for source_move '{entry.SourceMove}' requires chain_repeatable=true on the move definition.");
            }

            var validTargets = new List<string>();
            foreach (var target in entry.TargetMoves)
            {
                if (string.IsNullOrEmpty(target))
                    continue;

                if (!moveDataset.Moves.ContainsKey(target))
                {
                    FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': target_move '{target}' in entry for '{entry.SourceMove}' not found in registered moves. Target ignored.");
                    continue;
                }
                validTargets.Add(target);
            }

            if (validTargets.Count > 0)
            {
                validatedEntries.Add(new GatlingEntry
                {
                    SourceMove = entry.SourceMove,
                    TargetMoves = validTargets,
                    CancelCategory = entry.CancelCategory
                });
            }
        }

        return new GatlingTable
        {
            CharacterId = table.CharacterId,
            Entries = validatedEntries
        };
    }

    public MoveDefinition? GetMove(string moveId)
    {
        if (moveId is null)
            return null;
        lock (_moveDatasetSync)
        {
            _moveDataset.Moves.TryGetValue(moveId, out var move);
            return move;
        }
    }

    public IReadOnlyList<MoveDefinition> GetAllMoves()
    {
        lock (_moveDatasetSync)
            return _moveDataset.Moves.Values.ToList();
    }

    internal bool TryCommitMoveDataset(
        MoveDefinition[] moves, ulong expectedVersion, Func<bool> commitFile,
        MoveContentIdentity? committedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(moves);
        ArgumentNullException.ThrowIfNull(commitFile);
        var candidate = BuildMoveCandidate(moves);
        lock (_moveDatasetSync)
        {
            if (_moveDatasetVersion != expectedVersion)
                return false;
            ulong nextVersion = checked(_moveDatasetVersion + 1);
            if (!commitFile())
                return false;
            _moveDataset = candidate;
            _moveDatasetVersion = nextVersion;
            if (committedIdentity is not null) _moveContentIdentity = committedIdentity;
            return true;
        }
    }

    private static MoveDatasetState BuildMoveCandidate(IEnumerable<MoveDefinition> moves)
    {
        var candidateMoves = new Dictionary<string, MoveDefinition>(StringComparer.Ordinal);
        var candidateCategories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var move in moves)
        {
            ArgumentNullException.ThrowIfNull(move);
            if (!candidateMoves.TryAdd(move.MoveId, move))
                throw new FormatException($"[Data] Duplicate move_id: '{move.MoveId}'.");
            foreach (var window in move.CancelWindows)
                if (!string.IsNullOrEmpty(window.TargetCategory))
                    candidateCategories.Add(window.TargetCategory);
        }
        return new MoveDatasetState(candidateMoves, candidateCategories);
    }

    private sealed record MoveDatasetState(
        IReadOnlyDictionary<string, MoveDefinition> Moves,
        IReadOnlySet<string> KnownCategories);

    public GatlingTable? GetGatlingTable(string characterId)
    {
        if (characterId is null)
            return null;
        if (_gatlingTables.TryGetValue(characterId, out var table))
            return table;
        return new GatlingTable { CharacterId = characterId };
    }

    public IReadOnlyList<GatlingTable> GetAllGatlingTables()
    {
        return _gatlingTables.Values.ToList();
    }

    public CharacterDefinition? GetCharacter(string characterId)
    {
        if (characterId is null)
            return null;
        _characters.TryGetValue(characterId, out var character);
        return character;
    }

    public IReadOnlyList<CharacterDefinition> GetAllCharacters()
    {
        return _characters.Values.ToList();
    }

    public void RegisterCharacter(CharacterDefinition character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (string.IsNullOrEmpty(character.CharacterId))
            throw new ArgumentException("[Data] Character has null or empty CharacterId.", nameof(character));
        if (string.IsNullOrEmpty(character.DisplayName))
            throw new ArgumentException("[Data] Character has null or empty DisplayName.", nameof(character));
        if (!_characters.TryAdd(character.CharacterId, character))
            throw new InvalidOperationException($"[Data] Duplicate CharacterId registration: '{character.CharacterId}'.");
    }

    public KnockbackProfile? GetKnockbackProfile(string profileId)
    {
        if (profileId is null)
            return null;
        _knockbackProfiles.TryGetValue(profileId, out var profile);
        return profile;
    }

    public IReadOnlyList<KnockbackProfile> GetAllKnockbackProfiles()
    {
        return _knockbackProfiles.Values.ToList();
    }

    public void SetKnockbackProfiles(KnockbackProfile[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var candidate = new Dictionary<string, KnockbackProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null)
                throw new FormatException("[Data] KnockbackProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] KnockbackProfile has null or empty profile_id.");
            if (!candidate.TryAdd(profile.ProfileId, profile))
                throw new FormatException($"[Data] Duplicate knockback_profile_id: '{profile.ProfileId}'.");
        }
        lock (_physicsDatasetSync)
        {
            ulong nextVersion = checked(_physicsDatasetVersion + 1);
            _knockbackProfiles = candidate;
            _physicsDatasetVersion = nextVersion;
        }
    }

    internal bool TryCommitKnockbackProfiles(KnockbackProfile[] profiles, ulong expectedVersion,
        DataContentIdentity? committedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        PhysicsResponseProfile[] responses;
        lock (_physicsDatasetSync)
            responses = _physicsResponseProfiles.Values.ToArray();
        return TryCommitPhysicsDataset(profiles, responses, expectedVersion,
            static () => true, PhysicsDocumentKind.Knockback, committedIdentity);
    }

    public PhysicsResponseProfile? GetPhysicsResponseProfile(string profileId)
    {
        if (profileId is null)
            return null;
        _physicsResponseProfiles.TryGetValue(profileId, out var profile);
        return profile;
    }

    public IReadOnlyList<PhysicsResponseProfile> GetAllPhysicsResponseProfiles()
    {
        return _physicsResponseProfiles.Values.ToList();
    }

    public void SetPhysicsResponseProfiles(PhysicsResponseProfile[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var candidate = new Dictionary<string, PhysicsResponseProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null)
                throw new FormatException("[Data] PhysicsResponseProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] PhysicsResponseProfile has null or empty profile_id.");
            if (!candidate.TryAdd(profile.ProfileId, profile))
                throw new FormatException($"[Data] Duplicate physics_response_profile_id: '{profile.ProfileId}'.");
        }
        lock (_physicsDatasetSync)
        {
            ulong nextVersion = checked(_physicsDatasetVersion + 1);
            _physicsResponseProfiles = candidate;
            _physicsDatasetVersion = nextVersion;
        }
    }

    internal bool TryCommitPhysicsResponseProfiles(PhysicsResponseProfile[] profiles, ulong expectedVersion,
        DataContentIdentity? committedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        KnockbackProfile[] knockbacks;
        lock (_physicsDatasetSync)
            knockbacks = _knockbackProfiles.Values.ToArray();
        return TryCommitPhysicsDataset(knockbacks, profiles, expectedVersion,
            static () => true, PhysicsDocumentKind.Response, committedIdentity);
    }

    internal bool TryCommitPhysicsDataset(
        KnockbackProfile[] knockbackProfiles,
        PhysicsResponseProfile[] responseProfiles,
        ulong expectedVersion)
        => TryCommitPhysicsDataset(knockbackProfiles, responseProfiles, expectedVersion,
            static () => true, null, null);

    internal bool TryCommitPhysicsDataset(
        KnockbackProfile[] knockbackProfiles,
        PhysicsResponseProfile[] responseProfiles,
        ulong expectedVersion,
        Func<bool> commitFile,
        PhysicsDocumentKind? committedKind,
        DataContentIdentity? committedIdentity)
    {
        ArgumentNullException.ThrowIfNull(knockbackProfiles);
        ArgumentNullException.ThrowIfNull(responseProfiles);
        ArgumentNullException.ThrowIfNull(commitFile);
        var knockbackCandidate = BuildKnockbackCandidate(knockbackProfiles);
        var responseCandidate = BuildResponseCandidate(responseProfiles);
        lock (_physicsDatasetSync)
        {
            if (_physicsDatasetVersion != expectedVersion)
                return false;
            ulong nextVersion = checked(_physicsDatasetVersion + 1);
            if (!commitFile()) return false;
            _knockbackProfiles = knockbackCandidate;
            _physicsResponseProfiles = responseCandidate;
            _physicsDatasetVersion = nextVersion;
            if (committedIdentity is not null && committedKind == PhysicsDocumentKind.Knockback)
                _knockbackContentIdentity = committedIdentity;
            if (committedIdentity is not null && committedKind == PhysicsDocumentKind.Response)
                _responseContentIdentity = committedIdentity;
            return true;
        }
    }

    private static Dictionary<string, KnockbackProfile> BuildKnockbackCandidate(KnockbackProfile[] profiles)
    {
        var candidate = new Dictionary<string, KnockbackProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null)
                throw new FormatException("[Data] KnockbackProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] KnockbackProfile has null or empty profile_id.");
            if (!candidate.TryAdd(profile.ProfileId, profile))
                throw new FormatException($"[Data] Duplicate knockback_profile_id: '{profile.ProfileId}'.");
        }
        return candidate;
    }

    private static Dictionary<string, PhysicsResponseProfile> BuildResponseCandidate(PhysicsResponseProfile[] profiles)
    {
        var candidate = new Dictionary<string, PhysicsResponseProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null)
                throw new FormatException("[Data] PhysicsResponseProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] PhysicsResponseProfile has null or empty profile_id.");
            if (!candidate.TryAdd(profile.ProfileId, profile))
                throw new FormatException($"[Data] Duplicate physics_response_profile_id: '{profile.ProfileId}'.");
        }
        return candidate;
    }
}
