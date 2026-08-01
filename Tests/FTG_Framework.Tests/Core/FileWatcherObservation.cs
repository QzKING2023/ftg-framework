#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Tests.Core;

internal sealed class FileWatcherObservation : IDisposable
{
    private readonly ConcurrentQueue<(long Sequence, string Path)> _observations = new();
    private long _sequence;
    private bool _disposed;

    internal static StringComparer PathComparer { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public FileWatcherObservation() => EventBus.Instance.Subscribe<DataReloadedEvent>(OnReloaded);

    public long Sequence => Interlocked.Read(ref _sequence);
    public IReadOnlyList<(long Sequence, string Path)> Observations => _observations.ToArray();

    public static string Canonicalize(string path) => Path.GetFullPath(path);

    public bool SawAfter(long sequence, string path)
    {
        string canonical = Canonicalize(path);
        return _observations.Any(item => item.Sequence > sequence && PathComparer.Equals(item.Path, canonical));
    }

    public void InjectForTesting(params string[] paths)
    {
        foreach (string path in paths)
            Record(path);
    }

    public async Task WaitForOutcomeAsync(
        Func<bool> outcome,
        TimeSpan timeout,
        string expectedPath,
        string expectedMutation)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            EventBus.Instance.ProcessFrame();
            if (outcome())
                return;
            await DeadlineBackoffAsync(timeout - stopwatch.Elapsed).ConfigureAwait(false);
        }

        throw new TimeoutException(BuildTimeoutMessage(expectedPath, expectedMutation, stopwatch.Elapsed));
    }

    public async Task AssertQuietAsync(string path, TimeSpan quietWindow)
    {
        long sequence = Sequence;
        using var cancellation = new CancellationTokenSource(quietWindow);
        var unexpected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() => unexpected.TrySetCanceled(cancellation.Token));

        while (!cancellation.IsCancellationRequested)
        {
            EventBus.Instance.ProcessFrame();
            if (SawAfter(sequence, path))
                unexpected.TrySetResult();
            try
            {
                await unexpected.Task.WaitAsync(TimeSpan.FromMilliseconds(10), cancellation.Token).ConfigureAwait(false);
                throw new Xunit.Sdk.XunitException($"Unexpected notification for {Canonicalize(path)} within {quietWindow}.");
            }
            catch (TimeoutException) { }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
        }
    }

    public string BuildTimeoutMessage(string expectedPath, string expectedMutation, TimeSpan elapsed)
    {
        EventBusTestDiagnostic diagnostic = EventBus.Instance.GetTestDiagnostic();
        string observed = string.Join(", ", Observations.Select(item => $"{item.Sequence}:{item.Path}"));
        return $"[FileWatcher] Timeout; ExpectedPath={Canonicalize(expectedPath)}; ExpectedMutation={expectedMutation}; "
            + $"Observed=[{observed}]; EventBus={diagnostic}; Elapsed={elapsed}; Platform={RuntimeInformation.OSDescription}";
    }

    private static Task DeadlineBackoffAsync(TimeSpan remaining)
    {
        TimeSpan delay = remaining < TimeSpan.FromMilliseconds(10) ? remaining : TimeSpan.FromMilliseconds(10);
        return delay > TimeSpan.Zero ? Task.Delay(delay) : Task.CompletedTask;
    }

    private void OnReloaded(DataReloadedEvent evt) => Record(evt.FilePath);

    private void Record(string path)
    {
        long sequence = Interlocked.Increment(ref _sequence);
        _observations.Enqueue((sequence, Canonicalize(path)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EventBus.Instance.Unsubscribe<DataReloadedEvent>(OnReloaded);
    }
}
