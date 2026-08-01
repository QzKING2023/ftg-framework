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
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            beforeCommit?.Invoke();
            if (File.Exists(fullPath))
                // File.Replace is the existing-file commit point: same-volume atomic
                // replacement. No backup is retained; destination ACL/metadata handling
                // follows the platform File.Replace contract. All failures occur before
                // this call or are reported without changing the DataStore.
                File.Replace(temporary, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                // Same-directory rename is the new-file commit point.
                File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
