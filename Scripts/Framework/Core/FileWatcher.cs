#nullable enable
using System;
using System.IO;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Core;

/// <summary>
/// Thin wrapper around <see cref="System.IO.FileSystemWatcher"/> that detects
/// JSON file changes on OS background threads and enqueues
/// <see cref="DataReloadedEvent"/> for drain at step 0 of
/// <see cref="EventBus.ProcessFrame"/>.
///
/// Does NOT reload data itself — only detects changes and publishes events.
/// The Data layer subscribes to <see cref="DataReloadedEvent"/> and performs
/// the actual reload per AD-15.
/// </summary>
internal sealed class FileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    public FileWatcher(string directory, string filter)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(filter);

        _watcher = new FileSystemWatcher(directory, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = false
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.FullPath));
    }

    private static void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.OldFullPath));
        EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.FullPath));
    }

    private static void OnWatcherError(object sender, ErrorEventArgs e)
    {
        FrameworkLog.Error?.Invoke($"[FileWatcher] Internal buffer overflow or directory error: {e.GetException().Message}. Hot-reload may be disabled until restart.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Deleted -= OnFileChanged;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
    }
}
