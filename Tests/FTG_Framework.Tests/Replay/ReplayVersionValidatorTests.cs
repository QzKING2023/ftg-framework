#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class ReplayVersionValidatorTests
{
    [Fact]
    public void ValidateVersion_Match_Succeeds()
    {
        ReplayVersionValidator.ValidateVersion(ReplayVersionValidator.CurrentDataVersion);
    }

    [Fact]
    public void ValidateVersion_Mismatch_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateVersion(999));
        Assert.Contains("[Replay]", ex.Message);
        Assert.Contains("Version mismatch", ex.Message);
    }

    [Fact]
    public void ValidateVersion_Zero_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateVersion(0));
    }

    [Fact]
    public void ValidateFrameworkVersion_Match_Succeeds()
    {
        ReplayVersionValidator.ValidateFrameworkVersion("2.3.0", "2.3.1");
    }

    [Fact]
    public void ValidateFrameworkVersion_MajorMismatch_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateFrameworkVersion("1.3.0", "2.3.0"));
        Assert.Contains("[Replay]", ex.Message);
    }

    [Fact]
    public void ValidateFrameworkVersion_MinorMismatch_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateFrameworkVersion("2.4.0", "2.3.0"));
    }

    [Fact]
    public void ValidateFrameworkVersion_ShortFormat_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateFrameworkVersion("2", "2.3.0"));
    }

    [Fact]
    public void ValidateDeserializedEvent_Null_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateDeserializedEvent(null, "TestEvent"));
        Assert.Contains("[Replay]", ex.Message);
    }

    [Fact]
    public void ValidateDeserializedEvent_NullStringField_Throws()
    {
        // MoveFrameChangedEvent has a string MoveId field — construct one with null
        var evt = new MoveFrameChangedEvent(1, null!, 0, 0, MovePhase.Idle);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReplayVersionValidator.ValidateDeserializedEvent(evt, "MoveFrameChangedEvent"));
        Assert.Contains("[Replay]", ex.Message);
        Assert.Contains("MoveId", ex.Message);
    }

    [Fact]
    public void ValidateDeserializedEvent_Valid_Succeeds()
    {
        var evt = new HitConnectedEvent(1, 2, "5LP", 3, 30);
        ReplayVersionValidator.ValidateDeserializedEvent(evt, "HitConnectedEvent");
    }
}
