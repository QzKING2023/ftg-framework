#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class DataModelTests
{
    [Fact]
    public void ReplayEntry_Constructor_Valid()
    {
        var entry = new ReplayEntry(5, "FrameAdvancedEvent", """{"FrameNumber":0}""");
        Assert.Equal(5, entry.Frame);
        Assert.Equal("FrameAdvancedEvent", entry.EventType);
        Assert.Equal("""{"FrameNumber":0}""", entry.Payload);
    }

    [Fact]
    public void ReplayEntry_NegativeFrame_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayEntry(-1, "Test", "{}"));
    }

    [Fact]
    public void ReplayEntry_EmptyEventType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayEntry(0, "", "{}"));
    }

    [Fact]
    public void ReplayEntry_NullPayload_CoercesToEmpty()
    {
        var entry = new ReplayEntry(0, "Test", null!);
        Assert.Equal(string.Empty, entry.Payload);
    }

    [Fact]
    public void ReplayFile_Constructor_Valid()
    {
        var entries = new List<ReplayEntry>
        {
            new(0, "FrameAdvancedEvent", """{"FrameNumber":0}"""),
            new(1, "InputReceivedEvent", """{"PlayerId":1}"""),
        };
        var file = new ReplayFile("2.3.0", 1, 5, entries);
        Assert.Equal("2.3.0", file.FrameworkVersion);
        Assert.Equal(1, file.DataVersion);
        Assert.Equal(5, file.FrameCount);
        Assert.Equal(2, file.EventCount);
        Assert.Equal(2, file.Entries.Count);
    }

    [Fact]
    public void ReplayFile_EmptyFrameworkVersion_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayFile("", 1, 0, new List<ReplayEntry>()));
    }

    [Fact]
    public void ReplayFile_DataVersionZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayFile("2.3.0", 0, 0, new List<ReplayEntry>()));
    }

    [Fact]
    public void ReplayFile_NegativeFrameCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayFile("2.3.0", 1, -1, new List<ReplayEntry>()));
    }

    [Fact]
    public void ReplayFile_NullEntries_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReplayFile("2.3.0", 1, 0, null!));
    }
}
