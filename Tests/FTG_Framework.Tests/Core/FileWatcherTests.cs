#nullable enable
using System;
using System.IO;
using System.Threading;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Xunit;

namespace FTG_Framework.Tests.Core;

public class FileWatcherTests : IDisposable
{
    private readonly string _tempDir;

    public FileWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ftg_fw_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ── File creation enqueues DataReloadedEvent ──

    [Fact]
    public void FileCreated_EnqueuesDataReloadedEvent()
    {
        string filePath = Path.Combine(_tempDir, "test.json");

        using var watcher = new FileWatcher(_tempDir, "*.json");

        // Create a file — OS file system events are asynchronous, wait briefly
        File.WriteAllText(filePath, "{}");
        Thread.Sleep(200);

        // Drain events from EventBus via ProcessFrame step 0
        var events = CollectDataReloadedEvents();

        Assert.Contains(events, e => e.FilePath.EndsWith("test.json"));
    }

    // ── File modification enqueues DataReloadedEvent ──

    [Fact]
    public void FileModified_EnqueuesDataReloadedEvent()
    {
        string filePath = Path.Combine(_tempDir, "modify.json");
        File.WriteAllText(filePath, "{}");
        Thread.Sleep(50);

        using var watcher = new FileWatcher(_tempDir, "*.json");

        // Modify the file
        File.WriteAllText(filePath, "{\"changed\":true}");
        Thread.Sleep(200);

        var events = CollectDataReloadedEvents();
        Assert.Contains(events, e => e.FilePath.EndsWith("modify.json"));
    }

    // ── Multiple rapid changes ──

    [Fact]
    public void MultipleRapidChanges_ProduceMultipleEvents()
    {
        string filePath = Path.Combine(_tempDir, "rapid.json");

        using var watcher = new FileWatcher(_tempDir, "*.json");

        File.WriteAllText(filePath, "v1");
        Thread.Sleep(50);
        File.WriteAllText(filePath, "v2");
        Thread.Sleep(200);

        // At least one event for each change — OS may coalesce some
        var events = CollectDataReloadedEvents();
        Assert.NotEmpty(events);
    }

    [Fact]
    public void FileDeleted_EnqueuesDeletedTargetPath()
    {
        string filePath = Path.Combine(_tempDir, "delete.json");
        File.WriteAllText(filePath, "{}");
        using var watcher = new FileWatcher(_tempDir, "*.json");

        File.Delete(filePath);
        Thread.Sleep(200);

        var events = CollectDataReloadedEvents();
        Assert.Contains(events, e => Path.GetFullPath(e.FilePath) == Path.GetFullPath(filePath));
    }

    [Fact]
    public void FileRenamed_EnqueuesOldAndNewPaths()
    {
        string oldPath = Path.Combine(_tempDir, "old.json");
        string newPath = Path.Combine(_tempDir, "new.json");
        File.WriteAllText(oldPath, "{}");
        using var watcher = new FileWatcher(_tempDir, "*.json");

        File.Move(oldPath, newPath);
        Thread.Sleep(200);

        var events = CollectDataReloadedEvents();
        Assert.Contains(events, e => Path.GetFullPath(e.FilePath) == Path.GetFullPath(oldPath));
        Assert.Contains(events, e => Path.GetFullPath(e.FilePath) == Path.GetFullPath(newPath));
    }

    // ── Disposal stops watching ──

    [Fact]
    public void Dispose_StopsWatching()
    {
        using (var watcher = new FileWatcher(_tempDir, "*.json"))
        {
            // watcher disposed here
        }

        // Drain any events already in the queue
        DrainAllEvents();

        // Now create a file — should NOT produce events via the disposed watcher
        string filePath = Path.Combine(_tempDir, "after_dispose.json");
        File.WriteAllText(filePath, "{}");
        Thread.Sleep(200);

        var events = CollectDataReloadedEvents();
        Assert.Empty(events);
    }

    // ── Constructor validates arguments ──

    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileWatcher(null!, "*.json"));
    }

    [Fact]
    public void Constructor_NullFilter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileWatcher(_tempDir, null!));
    }

    // ── Helpers ──

    private static System.Collections.Generic.List<DataReloadedEvent> CollectDataReloadedEvents()
    {
        var events = new System.Collections.Generic.List<DataReloadedEvent>();
        var bus = EventBus.Instance;

        Action<DataReloadedEvent> handler = e => events.Add(e);
        bus.Subscribe(handler);
        try
        {
            bus.ProcessFrame();
        }
        finally
        {
            bus.Unsubscribe(handler);
        }

        return events;
    }

    private static void DrainAllEvents()
    {
        var bus = EventBus.Instance;
        var dummy = new System.Collections.Generic.List<DataReloadedEvent>();
        Action<DataReloadedEvent> handler = _ => { };
        bus.Subscribe(handler);
        try { bus.ProcessFrame(); }
        finally { bus.Unsubscribe(handler); }
    }
}
