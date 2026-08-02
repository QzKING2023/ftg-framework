#nullable enable
using System;
using System.IO;
using System.Text;

namespace FTG_Framework.Data;

internal static class PhysicsDataPersistence
{
    internal static void WriteAtomically(string path, string content, Action? beforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("[Data] Destination has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        string temporary = string.Empty;
        try
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            temporary = StageSameDirectory(fullPath, bytes);
            beforeCommit?.Invoke();
            ReplaceStaged(temporary, fullPath);
        }
        finally
        {
            CleanupOwnedStaging(temporary);
        }
    }

    internal static string StageSameDirectory(
        string destination, ReadOnlySpan<byte> bytes, Action? beforeFlush = null)
    {
        string fullPath = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("[Data] Destination has no parent directory.", nameof(destination));
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            beforeFlush?.Invoke();
            stream.Flush(flushToDisk: true);
            return temporary;
        }
        catch
        {
            CleanupOwnedStaging(temporary);
            throw;
        }
    }

    internal static void ReplaceStaged(string staged, string destination)
    {
        if (File.Exists(destination))
            File.Replace(staged, destination, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(staged, destination);
    }

    internal static void CleanupOwnedStaging(string staged)
    {
        if (!string.IsNullOrEmpty(staged) && File.Exists(staged))
            File.Delete(staged);
    }
}
