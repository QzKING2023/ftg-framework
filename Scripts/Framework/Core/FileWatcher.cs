#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
    private readonly object _callbackGate = new();
    private int _activeCallbacks;
    private bool _disposeStarted;
    private bool _disposeCompleted;

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

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        RunCallback(() => EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.FullPath)));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        RunCallback(() =>
        {
            EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.OldFullPath));
            EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(e.FullPath));
        });
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        RunCallback(() => FrameworkLog.Error?.Invoke($"[FileWatcher] Internal buffer overflow or directory error: {e.GetException().Message}. Hot-reload may be disabled until restart."));
    }

    private void RunCallback(Action callback)
    {
        lock (_callbackGate)
        {
            if (_disposeStarted)
                return;
            _activeCallbacks++;
        }
        try
        {
            callback();
        }
        finally
        {
            lock (_callbackGate)
            {
                _activeCallbacks--;
                if (_activeCallbacks == 0)
                    Monitor.PulseAll(_callbackGate);
            }
        }
    }

    public void Dispose()
    {
        lock (_callbackGate)
        {
            if (_disposeStarted)
            {
                while (!_disposeCompleted)
                    Monitor.Wait(_callbackGate);
                return;
            }
            _disposeStarted = true;
        }

        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Deleted -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();

            var deadline = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(5);
            lock (_callbackGate)
            {
                while (_activeCallbacks > 0)
                {
                    TimeSpan remaining = timeout - deadline.Elapsed;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_callbackGate, remaining))
                        throw new TimeoutException($"[FileWatcher] Timed out waiting for {_activeCallbacks} callback(s) to quiesce.");
                }
            }
        }
        finally
        {
            lock (_callbackGate)
            {
                _disposeCompleted = true;
                Monitor.PulseAll(_callbackGate);
            }
        }
    }
}
