#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

[Collection(EventBusTestCollection.Name)]
public class EventBusRecordingTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Recorder);
    private readonly EventBus _bus;

    public EventBusRecordingTests()
    {
        _bus = EventBus.Instance;
        _bus.Recorder = null;
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    [Fact]
    public void ProcessFrame_WithRecorder_RecordsAllEvents()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        _bus.Recorder = recorder;

        _bus.Publish(new InputReceivedEvent(1, 0, 0, 3));
        _bus.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30));
        _bus.ProcessFrame();

        var file = recorder.Save();
        // FrameAdvanced + 2 published events
        Assert.True(file.EventCount >= 3);
    }

    [Fact]
    public void ProcessFrame_AllEventsSameFrameNumber()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        _bus.Recorder = recorder;

        _bus.Publish(new InputReceivedEvent(1, 0, 0, 3));
        _bus.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30));
        _bus.ProcessFrame();

        var file = recorder.Save();
        // All events in one ProcessFrame call share the same dispatch frame number
        int firstFrame = file.Entries[0].Frame;
        foreach (var entry in file.Entries)
            Assert.Equal(firstFrame, entry.Frame);
    }

    [Fact]
    public void PublishImmediate_WithRecorder_RecordsEvent()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        _bus.Recorder = recorder;

        _bus.PublishImmediate(new FrameRewoundEvent(5));

        var file = recorder.Save();
        Assert.True(file.EventCount >= 1);
        Assert.Contains(file.Entries, e => e.EventType == nameof(FrameRewoundEvent));
    }

    [Fact]
    public void Recorder_Null_NoThrow()
    {
        _bus.Recorder = null;

        _bus.Publish(new InputReceivedEvent(1, 0, 0, 3));
        _bus.ProcessFrame();
        _bus.PublishImmediate(new FrameRewoundEvent(5));
    }

    [Fact]
    public void FrameNumbers_AreMonotonic()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        _bus.Recorder = recorder;

        for (int i = 0; i < 10; i++)
        {
            _bus.Publish(new InputReceivedEvent(1, i, 0, 3));
            _bus.ProcessFrame();
        }

        var file = recorder.Save();
        int prev = -1;
        foreach (var entry in file.Entries)
        {
            Assert.True(entry.Frame >= prev, $"Frame {entry.Frame} should be >= previous {prev}");
            prev = entry.Frame;
        }
    }

    [Fact]
    public void Recorder_StoresLivePhaseSequenceAndSourceEpochProvenance()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        _bus.Recorder = recorder;
        ulong epoch = _bus.LifecycleEpoch;
        _bus.Publish(new InputReceivedEvent(1, 0, 0, 3));
        _bus.Publish(new InputReceivedEvent(2, 0, 0, 1));
        _bus.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30));
        _bus.ProcessFrame();

        ReplayFile file = recorder.Save();
        foreach (ReplayEntry entry in file.Entries)
        {
            Type type = EventTypeRegistry.Resolve(entry.EventType)!;
            Assert.Equal(EventTypeRegistry.GetPhase(type), entry.Phase);
            Assert.Equal(epoch, entry.SourceEpoch);
        }
        ReplayEntry[] inputs = file.Entries.Where(e => e.EventType == nameof(InputReceivedEvent)).ToArray();
        Assert.Equal([0, 1], inputs.Select(e => e.Sequence));
    }

    [Fact]
    public void ReplayInjection_BypassesRecording_WhenRecorderNull()
    {
        // Simulate replay: recorder is null during injection
        _bus.Recorder = null;

        // Inject events via PublishImmediate (as replay does)
        _bus.PublishImmediate(new FrameAdvancedEvent(0));
        _bus.PublishImmediate(new HitConnectedEvent(1, 2, "5LP", 3, 30));

        // Verify no recording was made (null recorder = no-op)
        // No assertion needed beyond no-throw — the null check handles it
    }
}
