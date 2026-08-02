#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;

namespace FTG_Framework.Data;

internal static class CanonicalDestinationCoordinator
{
    private static readonly ConcurrentDictionary<string, object> Locks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static T Execute<T>(string path, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        string canonical = Path.GetFullPath(path);
        lock (Locks.GetOrAdd(canonical, static _ => new object())) return action();
    }
}
