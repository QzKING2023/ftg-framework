#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

// Frame simulation model: EventBus.ProcessFrame() auto-injects one FrameAdvancedEvent
// (Phase 1) before dispatching hit/block events (Phase 3). One ProcessFrame() call ==
// one frame. Never publish FrameAdvancedEvent manually — that creates phantom ticks.
public class AdvantageDisplayTests : IDisposable
{
    private readonly List<AdvantageDisplay> _panels = new();

    private AdvantageDisplay CreatePanel(
        int trackedPlayer = 1,
        bool showWhenZero = true)
    {
        EventBusTestHelper.Drain();
        var panel = new AdvantageDisplay
        {
            TrackedPlayer = trackedPlayer,
            ShowWhenZero = showWhenZero
        };
        panel._Ready();
        _panels.Add(panel);
        return panel;
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
            panel._ExitTree();
        EventBusTestHelper.Drain();
    }

    private static void PublishAndProcess<T>(T evt) where T : struct
    {
        EventBus.Instance.Publish(evt);
        EventBus.Instance.ProcessFrame();
    }

    private static void AdvanceFrames(int count)
    {
        for (int i = 0; i < count; i++)
            EventBus.Instance.ProcessFrame();
    }

    [Fact]
    public void HitConnected_TrackedPlayerIsAttacker_SetsPositiveAdvantage()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));

        Assert.Equal(4, panel.Advantage);
        Assert.Equal("+4", panel.DisplayText);
        Assert.True(panel.IsLabelVisible);
    }

    [Fact]
    public void HitConnected_TrackedPlayerIsDefender_SetsNegativeAdvantage()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(2, 1, "5LP", 4));

        Assert.Equal(-4, panel.Advantage);
        Assert.Equal("-4", panel.DisplayText);
    }

    [Fact]
    public void MoveBlocked_TrackedPlayerIsAttacker_SetsNegativeAdvantage()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new MoveBlockedEvent(1, 2, "5LP", -5));

        Assert.Equal(-5, panel.Advantage);
        Assert.Equal("-5", panel.DisplayText);
    }

    [Fact]
    public void MoveBlocked_TrackedPlayerIsDefender_SetsPositiveAdvantage()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new MoveBlockedEvent(2, 1, "5LP", -5));

        Assert.Equal(5, panel.Advantage);
        Assert.Equal("+5", panel.DisplayText);
    }

    [Fact]
    public void HitConnected_ZeroAdvantage_DisplaysZero()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 0));

        Assert.Equal(0, panel.Advantage);
        Assert.Equal("0", panel.DisplayText);
        Assert.True(panel.IsLabelVisible);
    }

    [Fact]
    public void FrameAdvanced_DecrementsPositiveAdvantage_StepsToZero()
    {
        var panel = CreatePanel(1);

        // Hit lands: Phase 1 ticks 0→0, Phase 3 sets +4 (shown for the rest of that frame)
        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));
        Assert.Equal(4, panel.Advantage);

        // Each subsequent frame decrements by exactly 1
        for (int expected = 3; expected >= 0; expected--)
        {
            AdvanceFrames(1);
            Assert.Equal(expected, panel.Advantage);
        }

        // Extra frame should not overshoot past zero
        AdvanceFrames(1);
        Assert.Equal(0, panel.Advantage);
        Assert.Equal("0", panel.DisplayText);
    }

    [Fact]
    public void FrameAdvanced_IncrementsNegativeAdvantage_StepsToZero()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new MoveBlockedEvent(1, 2, "5LP", -5));
        Assert.Equal(-5, panel.Advantage);

        for (int expected = -4; expected <= 0; expected++)
        {
            AdvanceFrames(1);
            Assert.Equal(expected, panel.Advantage);
        }

        // Extra frame should not overshoot past zero
        AdvanceFrames(1);
        Assert.Equal(0, panel.Advantage);
    }

    [Fact]
    public void HitConnected_MidCountdown_ResetsAdvantage()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));

        // Advance 2 frames: +4 → +2
        AdvanceFrames(2);
        Assert.Equal(2, panel.Advantage);

        // New hit lands: Phase 1 ticks +2 → +1, Phase 3 overwrites with +6
        PublishAndProcess(new HitConnectedEvent(1, 2, "5HP", 6));
        Assert.Equal(6, panel.Advantage);
        Assert.Equal("+6", panel.DisplayText);
    }

    [Fact]
    public void FrameAdvanced_AdvantageIsZero_DoesNothing()
    {
        var panel = CreatePanel(1);

        Assert.Equal(0, panel.Advantage);

        AdvanceFrames(1);

        Assert.Equal(0, panel.Advantage);
    }

    [Fact]
    public void ShowWhenZero_False_HidesLabelAndBackgroundWhenZero()
    {
        var panel = CreatePanel(1, showWhenZero: false);

        Assert.Equal(0, panel.Advantage);
        Assert.False(panel.IsLabelVisible);
        Assert.False(panel.IsBackgroundVisible);

        // Non-zero advantage — whole panel becomes visible
        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 3));
        Assert.Equal(3, panel.Advantage);
        Assert.True(panel.IsLabelVisible);
        Assert.True(panel.IsBackgroundVisible);

        // Count down to 0 — whole panel hides
        AdvanceFrames(3);
        Assert.Equal(0, panel.Advantage);
        Assert.False(panel.IsLabelVisible);
        Assert.False(panel.IsBackgroundVisible);
    }

    [Fact]
    public void ShowWhenZero_True_ShowsLabelWhenZero()
    {
        var panel = CreatePanel(1, showWhenZero: true);

        Assert.Equal(0, panel.Advantage);
        Assert.True(panel.IsLabelVisible);
        Assert.True(panel.IsBackgroundVisible);
        Assert.Equal("0", panel.DisplayText);
    }

    [Fact]
    public void TrackedPlayer_Two_UsesP2Perspective()
    {
        var panel = CreatePanel(2);

        // P2 is the defender — advantage is negated
        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));
        Assert.Equal(-4, panel.Advantage);
        Assert.Equal("-4", panel.DisplayText);

        // P2 is the attacker — advantage is taken as-is.
        // Same frame: Phase 1 ticks -4 → -3, Phase 3 overwrites with +4
        PublishAndProcess(new HitConnectedEvent(2, 1, "5LP", 4));
        Assert.Equal(4, panel.Advantage);
        Assert.Equal("+4", panel.DisplayText);
    }

    [Fact]
    public void TwoPanels_TrackingP1AndP2_PerspectivesAreMirrored()
    {
        var p1Panel = CreatePanel(1);
        var p2Panel = CreatePanel(2);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));

        Assert.Equal(4, p1Panel.Advantage);
        Assert.Equal(-4, p2Panel.Advantage);

        PublishAndProcess(new MoveBlockedEvent(1, 2, "5LP", -5));

        Assert.Equal(-5, p1Panel.Advantage);
        Assert.Equal(5, p2Panel.Advantage);
    }

    [Fact]
    public void ExitTree_Unsubscribes_DisplayNotUpdatedByEvents()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));
        Assert.Equal(4, panel.Advantage);

        panel._ExitTree();
        EventBusTestHelper.Drain();

        // Publish events — panel should not react
        PublishAndProcess(new HitConnectedEvent(1, 2, "5HP", 6));
        AdvanceFrames(1);

        Assert.Equal(4, panel.Advantage);
    }

    [Fact]
    public void HitConnected_NeitherAttackerNorDefender_AdvantageUnchanged()
    {
        var panel = CreatePanel(1);

        PublishAndProcess(new HitConnectedEvent(1, 2, "5LP", 4));
        Assert.Equal(4, panel.Advantage);

        // Same frame: Phase 1 ticks 4 → 3; the unrelated (2,3) hit must be ignored in Phase 3
        PublishAndProcess(new HitConnectedEvent(2, 3, "5HP", 10));

        Assert.Equal(3, panel.Advantage);
    }
}
