#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public class ChargeTrackerTests
{
    // --- State transitions ---

    [Fact]
    public void StartingCharge_MarksCharging()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);

        tracker.Update(1, 0);

        Assert.True(tracker.IsChargeValid(1, DirectionValue.Down, 1, 0));
        Assert.Equal(1, tracker.GetChargeDuration(1, DirectionValue.Down, 0));
    }

    [Fact]
    public void ChargeAfterRelease_RetentionValidAtBoundary()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        // Frame 0: hold down
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);

        // Frame 1-4: release
        for (int f = 1; f <= 4; f++)
            tracker.Update(1, f);

        // Frame 5: 5 - 0 <= 5, still valid at boundary
        tracker.Update(1, 5);
        Assert.True(tracker.IsChargeValid(1, DirectionValue.Down, 1, 5));
    }

    [Fact]
    public void ChargeAfterRelease_RetentionExpired()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history, retentionFrames: 3);

        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);

        for (int f = 1; f <= 5; f++)
            tracker.Update(1, f);

        Assert.False(tracker.IsChargeValid(1, DirectionValue.Down, 1, 5));
    }

    [Fact]
    public void ChargeRetention_ExpiresAfterWindow()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history, retentionFrames: 3);

        // Hold down on frame 0, release after
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);
        // Frame 1-9: no input (released)
        for (int f = 1; f <= 9; f++)
            tracker.Update(1, f);

        Assert.False(tracker.IsChargeValid(1, DirectionValue.Down, 1, 9));
    }

    [Fact]
    public void RePressInRetentionWindow_RestoresCharge()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history, retentionFrames: 5);

        // Frame 0: hold back, charge for 5 frames
        for (int f = 0; f < 5; f++)
        {
            history.AddDirectionalEntry(1, f, DirectionValue.Back);
            tracker.Update(1, f);
        }

        // Frame 5-6: release
        tracker.Update(1, 5);
        tracker.Update(1, 6);

        // Frame 7: re-press back within retention window
        history.AddDirectionalEntry(1, 7, DirectionValue.Back);
        tracker.Update(1, 7);

        // Charge should be valid with accumulated duration
        Assert.True(tracker.IsChargeValid(1, DirectionValue.Back, 5, 7));
    }

    [Fact]
    public void RePressAfterRetentionExpiry_StartsNewCharge()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history, retentionFrames: 2);

        // Frame 0: hold back
        history.AddDirectionalEntry(1, 0, DirectionValue.Back);
        tracker.Update(1, 0);
        // Frame 1-5: release (exceeds retention=2)
        for (int f = 1; f <= 5; f++)
            tracker.Update(1, f);

        // Frame 6: re-press (retention expired)
        history.AddDirectionalEntry(1, 6, DirectionValue.Back);
        tracker.Update(1, 6);

        Assert.Equal(1, tracker.GetChargeDuration(1, DirectionValue.Back, 6));
    }

    // --- Charge validity & duration ---

    [Fact]
    public void IsChargeValid_FalseWhenInsufficientDuration()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);

        Assert.False(tracker.IsChargeValid(1, DirectionValue.Down, minDuration: 10, currentFrame: 0));
    }

    [Fact]
    public void IsChargeValid_TrueWhenSufficientDuration()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        for (int f = 0; f < 30; f++)
        {
            history.AddDirectionalEntry(1, f, DirectionValue.Back);
            tracker.Update(1, f);
        }

        Assert.True(tracker.IsChargeValid(1, DirectionValue.Back, minDuration: 30, currentFrame: 29));
    }

    [Fact]
    public void IsChargeValid_FalseForNeverCharged()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        Assert.False(tracker.IsChargeValid(1, DirectionValue.Back, minDuration: 1, currentFrame: 0));
    }

    [Fact]
    public void GetChargeDuration_ZeroForNeverCharged()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        Assert.Equal(0, tracker.GetChargeDuration(1, DirectionValue.Back, 0));
    }

    // --- Dual player independence ---

    [Fact]
    public void ChargeStates_IndependentPerPlayer()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);

        Assert.True(tracker.IsChargeValid(1, DirectionValue.Down, minDuration: 1, currentFrame: 0));
        Assert.Equal(0, tracker.GetChargeDuration(2, DirectionValue.Down, 0));
    }

    [Fact]
    public void ChargeStates_IndependentPerDirection()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history);

        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        tracker.Update(1, 0);

        Assert.True(tracker.IsChargeValid(1, DirectionValue.Down, minDuration: 1, currentFrame: 0));
        Assert.Equal(0, tracker.GetChargeDuration(1, DirectionValue.Back, 0));
    }

    // --- RetentionFrames property ---

    [Fact]
    public void RetentionFrames_ReturnsConfiguredValue()
    {
        var history = new StubInputHistory();
        var tracker = new ChargeTracker(history, retentionFrames: 7);
        Assert.Equal(7, tracker.RetentionFrames);
    }
}
