#nullable enable
using System;
using System.Text.Json;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

[Collection(EventBusTestCollection.Name)]
public class ReplayRecorderTests
{
    [Fact]
    public void Record_SingleEventPerFrame_HundredFrames()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        for (int frame = 0; frame < 100; frame++)
            recorder.Record(frame, new FrameAdvancedEvent(frame));

        Assert.Equal(100, recorder.EventCount);
        Assert.Equal(99, recorder.MaxFrameNumber);
    }

    [Fact]
    public void Record_FiveEventTypesIntermixed_DispatchOrderPreserved()
    {
        var recorder = new ReplayRecorder { IsRecording = true };

        recorder.Record(0, new FrameAdvancedEvent(0));
        recorder.Record(0, new InputReceivedEvent(1, 0, 0, 3));
        recorder.Record(1, new FrameAdvancedEvent(1));
        recorder.Record(1, new MoveFrameChangedEvent(1, "5LP", 1, 10, MovePhase.Startup));
        recorder.Record(1, new HitConnectedEvent(1, 2, "5LP", 3, 30));

        var file = recorder.Save();
        Assert.Equal(5, file.EventCount);
        Assert.Equal("FrameAdvancedEvent", file.Entries[0].EventType);
        Assert.Equal("InputReceivedEvent", file.Entries[1].EventType);
        Assert.Equal("FrameAdvancedEvent", file.Entries[2].EventType);
        Assert.Equal("MoveFrameChangedEvent", file.Entries[3].EventType);
        Assert.Equal("HitConnectedEvent", file.Entries[4].EventType);
    }

    [Fact]
    public void Record_NegativeFrame_Throws()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.Record(-1, new FrameAdvancedEvent(0)));
    }

    [Fact]
    public void Save_ReturnsDeepCopiedSnapshot()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        recorder.Record(0, new FrameAdvancedEvent(0));
        recorder.Record(1, new InputReceivedEvent(1, 1, 0, 3));

        var file = recorder.Save();
        Assert.Equal(2, file.EventCount);

        // Modify recorder after Save — file must be unaffected
        recorder.Record(2, new HitConnectedEvent(1, 2, "5LP", 3, 30));
        Assert.Equal(2, file.EventCount);
        Assert.Equal(3, recorder.EventCount);
    }

    [Fact]
    public void IsRecording_False_RecordIsNoop()
    {
        var recorder = new ReplayRecorder { IsRecording = false };
        recorder.Record(0, new FrameAdvancedEvent(0));

        Assert.Equal(0, recorder.EventCount);
    }

    [Fact]
    public void Save_JsonRoundTrip_ViaReplayEventJsonContext()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        recorder.Record(0, new FrameAdvancedEvent(0));
        recorder.Record(1, new HitConnectedEvent(1, 2, "5HP", 3, 80));

        var file = recorder.Save();

        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = ReplayEventJsonContext.Default,
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(file, options);
        Assert.Contains("FrameAdvancedEvent", json);
        Assert.Contains("HitConnectedEvent", json);

        var restored = JsonSerializer.Deserialize<ReplayFile>(json, options);
        Assert.NotNull(restored);
        Assert.Equal(file.FrameworkVersion, restored.FrameworkVersion);
        Assert.Equal(file.DataVersion, restored.DataVersion);
        Assert.Equal(file.FrameCount, restored.FrameCount);
        Assert.Equal(file.EventCount, restored.EventCount);
    }

    [Fact]
    public void Record_ThreadSafe_MultipleThreadsNoCorruption()
    {
        var recorder = new ReplayRecorder { IsRecording = true };
        const int eventsPerThread = 100;

        var threads = new System.Threading.Thread[4];
        for (int t = 0; t < threads.Length; t++)
        {
            int threadId = t;
            threads[t] = new System.Threading.Thread(() =>
            {
                for (int i = 0; i < eventsPerThread; i++)
                {
                    recorder.Record(threadId * eventsPerThread + i,
                        new FrameAdvancedEvent(threadId * eventsPerThread + i));
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        Assert.Equal(eventsPerThread * threads.Length, recorder.EventCount);
    }

    [Fact]
    public void Save_PublishesFrameworkLog()
    {
        string? logMessage = null;
        FrameworkLog.Info = msg => logMessage = msg;

        try
        {
            var recorder = new ReplayRecorder { IsRecording = true };
            recorder.Record(0, new FrameAdvancedEvent(0));
            recorder.Save();

            Assert.NotNull(logMessage);
            Assert.Contains("[Replay]", logMessage);
            Assert.Contains("Saving recording", logMessage);
        }
        finally
        {
            FrameworkLog.Info = null;
        }
    }
}
