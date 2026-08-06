#nullable enable
// xUnit1031 (no blocking task operations in test methods) is intentionally disabled here:
// EventBus's test scope is bound to the owning test thread (EventBus.BeginTestScope/
// EndTestScope, EventBus.EnsureTestOwner) and xUnit v2 async tests may resume on a
// different thread after await, which would trip that guard in the class Dispose. The
// waits below poll on the same thread for the same reason.
#pragma warning disable xUnit1031
using System;
using System.IO;
using System.Threading.Tasks;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class FileWatcherTests : IDisposable
{
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(5);
    private readonly EventBusTestScope _eventBusScope = new();
    private readonly string _tempDir;

    public FileWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ftg_fw_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        finally
        {
            EventBus.Instance.ProcessFrame(); // Watchers are quiesced; drain notifications admitted before disposal.
            _eventBusScope.Dispose();
        }
    }

    [Fact]
    public void FileCreated_ObservesCanonicalPathAndCommittedContent()
    {
        string path = Path.Combine(_tempDir, "create.json");
        const string token = "create-v1";
        using var observation = new FileWatcherObservation();
        using var watcher = new FileWatcher(_tempDir, "*.json");
        long sequence = observation.Sequence;

        File.WriteAllText(path, token);

        observation.WaitForOutcomeAsync(
            () => observation.SawAfter(sequence, path) && File.ReadAllText(path) == token,
            ObservationTimeout, path, token).GetAwaiter().GetResult();
    }

    [Fact]
    public void FileModified_ObservesCanonicalPathAndFinalVersion()
    {
        string path = Path.Combine(_tempDir, "modify.json");
        File.WriteAllText(path, "initial");
        using var observation = new FileWatcherObservation();
        using var watcher = new FileWatcher(_tempDir, "*.json");
        long sequence = observation.Sequence;

        const string token = "modify-v2";
        File.WriteAllText(path, token);

        observation.WaitForOutcomeAsync(
            () => observation.SawAfter(sequence, path) && File.ReadAllText(path) == token,
            ObservationTimeout, path, token).GetAwaiter().GetResult();
    }

    [Fact]
    public void RapidWrites_AssertFinalOutcome_NotCallbackCount()
    {
        string path = Path.Combine(_tempDir, "rapid.json");
        using var observation = new FileWatcherObservation();
        using var watcher = new FileWatcher(_tempDir, "*.json");
        long sequence = observation.Sequence;

        File.WriteAllText(path, "rapid-v1");
        File.WriteAllText(path, "rapid-v2");

        observation.WaitForOutcomeAsync(
            () => observation.SawAfter(sequence, path) && File.ReadAllText(path) == "rapid-v2",
            ObservationTimeout, path, "rapid-v2").GetAwaiter().GetResult();
    }

    [Fact]
    public void FileDeleted_ObservesPathAndFinalAbsence()
    {
        string path = Path.Combine(_tempDir, "delete.json");
        File.WriteAllText(path, "delete-v1");
        using var observation = new FileWatcherObservation();
        using var watcher = new FileWatcher(_tempDir, "*.json");
        long sequence = observation.Sequence;

        File.Delete(path);

        observation.WaitForOutcomeAsync(
            () => observation.SawAfter(sequence, path) && !File.Exists(path),
            ObservationTimeout, path, "deleted").GetAwaiter().GetResult();
    }

    [Fact]
    public void FileRenamed_ObservesBothPathsAndFinalState()
    {
        string oldPath = Path.Combine(_tempDir, "old.json");
        string newPath = Path.Combine(_tempDir, "new.json");
        File.WriteAllText(oldPath, "rename-v1");
        using var observation = new FileWatcherObservation();
        using var watcher = new FileWatcher(_tempDir, "*.json");
        long sequence = observation.Sequence;

        File.Move(oldPath, newPath);

        observation.WaitForOutcomeAsync(
            () => observation.SawAfter(sequence, oldPath)
                && observation.SawAfter(sequence, newPath)
                && !File.Exists(oldPath)
                && File.ReadAllText(newPath) == "rename-v1",
            ObservationTimeout, newPath, "old absent/new rename-v1").GetAwaiter().GetResult();
    }

    [Fact]
    public void Dispose_ProducesNoPostDisposalNotification()
    {
        string path = Path.Combine(_tempDir, "after-dispose.json");
        using var observation = new FileWatcherObservation();
        var watcher = new FileWatcher(_tempDir, "*.json");
        watcher.Dispose();
        EventBus.Instance.ProcessFrame();

        File.WriteAllText(path, "after-dispose-v1");

        observation.AssertQuietAsync(path, TimeSpan.FromMilliseconds(150)).GetAwaiter().GetResult();
    }

    [Fact]
    public void Dispose_ConcurrentCalls_AreIdempotent()
    {
        var watcher = new FileWatcher(_tempDir, "*.json");
        Task first = Task.Run(watcher.Dispose);
        Task second = Task.Run(watcher.Dispose);

        Task.WaitAll(first, second);
        watcher.Dispose();
    }

    [Fact]
    public void Observation_AcceptsDuplicateAndReorderedNotifications()
    {
        using var observation = new FileWatcherObservation();
        string first = Path.Combine(_tempDir, "first.json");
        string final = Path.Combine(_tempDir, "final.json");
        long sequence = observation.Sequence;

        observation.InjectForTesting(final, first, final);

        Assert.True(observation.SawAfter(sequence, first));
        Assert.True(observation.SawAfter(sequence, final));
    }

    [Fact]
    public void Timeout_ReportsExpectedOutcomeObservationsBusAndPlatform()
    {
        using var observation = new FileWatcherObservation();
        string path = Path.Combine(_tempDir, "never.json");

        var error = Assert.Throws<TimeoutException>(() => observation.WaitForOutcomeAsync(
            () => false, TimeSpan.FromMilliseconds(20), path, "never-v1").GetAwaiter().GetResult());

        Assert.Contains("ExpectedPath=", error.Message);
        Assert.Contains("ExpectedMutation=never-v1", error.Message);
        Assert.Contains("Observed=", error.Message);
        Assert.Contains("EventBus=", error.Message);
        Assert.Contains("Elapsed=", error.Message);
        Assert.Contains("Platform=", error.Message);
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new FileWatcher(null!, "*.json"));
        Assert.Throws<ArgumentNullException>(() => new FileWatcher(_tempDir, null!));
    }
}
