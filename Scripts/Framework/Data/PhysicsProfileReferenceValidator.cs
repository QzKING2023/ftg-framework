#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Data;

internal static class PhysicsProfileReferenceValidator
{
    public static void ValidateKnockbackProfiles(
        IDataStore dataStore,
        IReadOnlyCollection<KnockbackProfile> candidates)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(candidates);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in candidates)
            ids.Add(profile.ProfileId);

        foreach (var move in dataStore.GetAllMoves())
        {
            bool hasHitbox = false;
            foreach (var frame in move.CollisionFrames)
            {
                if (frame.Hitboxes.Count == 0)
                    continue;
                hasHitbox = true;
                break;
            }

            if (!hasHitbox)
                continue;
            if (string.IsNullOrWhiteSpace(move.KnockbackProfileId))
                throw new FormatException(
                    $"[Data] Hit-capable move '{move.MoveId}' must reference a KnockbackProfile.");
            if (!ids.Contains(move.KnockbackProfileId))
                throw new FormatException(
                    $"[Data] Hit-capable move '{move.MoveId}' references missing KnockbackProfile '{move.KnockbackProfileId}'.");
        }
    }

    public static void ValidateResponseProfiles(
        IReadOnlyCollection<PhysicsResponseProfile> candidates,
        IReadOnlyCollection<string> requiredProfileIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(requiredProfileIds);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in candidates)
            ids.Add(profile.ProfileId);

        if (!ids.Contains("default"))
            throw new FormatException("[Data] Required PhysicsResponseProfile 'default' is missing.");

        foreach (string requiredId in requiredProfileIds)
        {
            if (!ids.Contains(requiredId))
                throw new FormatException(
                    $"[Data] Required PhysicsResponseProfile '{requiredId}' is missing.");
        }
    }
}
