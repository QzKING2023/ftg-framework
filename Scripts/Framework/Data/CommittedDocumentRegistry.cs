#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;

namespace FTG_Framework.Data;

internal static class CommittedDocumentRegistry
{
    private static readonly ConcurrentDictionary<string, DataContentIdentity> Identities = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static void Observe(string path, DataContentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identities[Path.GetFullPath(path)] = identity;
    }

    internal static bool IsObserved(string path, DataContentIdentity identity) =>
        Identities.TryGetValue(Path.GetFullPath(path), out DataContentIdentity? observed) && observed == identity;
}
