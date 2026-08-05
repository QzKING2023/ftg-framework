#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests.Engine.Combo;

[Collection(EventBusTestCollection.Name)]
public sealed class ComboStateTrackerSnapshotTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.None);

    public void Dispose() => _eventBusScope.Dispose();

    private static ComboStateTracker Tracker()
    {
        var dataStore = new DataStore([]);
        var tracker = new ComboStateTracker(dataStore);
        tracker.Initialize(dataStore);
        return tracker;
    }

    private static ComboRuntimeSnapshot ActiveTrack(int player, int frame) => new(
        new System.Collections.Generic.Dictionary<int, ComboTrackRuntimeSnapshot>
        {
            [player] = new ComboTrackRuntimeSnapshot(true, 3, "5LP", frame, 5, false, "5LP")
        });

    [Fact]
    public void Capture_Install_RoundTripsTrackStateExactly()
    {
        var tracker = Tracker();
        try
        {
            var captured = new ComboRuntimeSnapshot(
                new System.Collections.Generic.Dictionary<int, ComboTrackRuntimeSnapshot>
                {
                    [1] = new ComboTrackRuntimeSnapshot(true, 3, "5LP", 4, 5, false, "5LP"),
                    [2] = new ComboTrackRuntimeSnapshot(false, 0, string.Empty, -1, 0, true, string.Empty)
                });
            tracker.InstallComboState(captured);

            ComboRuntimeSnapshot snapshot = tracker.CaptureComboState();

            Assert.Equal(2, snapshot.Tracks.Count);
            Assert.True(tracker.IsActive(1));
            Assert.Equal(3, tracker.GetHitCount(1));
            Assert.Equal("5LP", tracker.GetCurrentMoveId(1));
            Assert.Equal(4, tracker.GetComboStartFrame(1));
            Assert.False(tracker.IsActive(2));
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_ValidSnapshot_ReturnsUnchanged()
    {
        var tracker = Tracker();
        try
        {
            ComboRuntimeSnapshot input = ActiveTrack(1, 4);
            var prepared = tracker.PrepareComboState(
                input, new SnapshotPrepareContext(1, 2, 10, SnapshotRestoreMode.Normal));

            Assert.Equal(input.Tracks[1], prepared.Tracks[1]);
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_ReplayHandoff_RebasesLiveDomainStartFrame()
    {
        var tracker = Tracker();
        try
        {
            // The handoff capture runs in the live frame domain while the snapshot
            // frame is the replay domain; the start is rebased, not rejected.
            ComboRuntimeSnapshot input = ActiveTrack(1, 1000);
            var prepared = tracker.PrepareComboState(
                input, new SnapshotPrepareContext(1, 2, 299, SnapshotRestoreMode.ReplayHandoff));

            Assert.Equal(300, prepared.Tracks[1].StartFrame);
            Assert.Equal(3, prepared.Tracks[1].HitCount);
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_NormalMode_LiveDomainStartFrame_StillRejects()
    {
        var tracker = Tracker();
        try
        {
            ComboRuntimeSnapshot input = ActiveTrack(1, 1000);
            Assert.Throws<SnapshotPrepareException>(() => tracker.PrepareComboState(
                input, new SnapshotPrepareContext(1, 2, 299, SnapshotRestoreMode.Normal)));
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_InvalidPlayerKey_Rejects()
    {
        var tracker = Tracker();
        try
        {
            var snapshot = new ComboRuntimeSnapshot(
                new System.Collections.Generic.Dictionary<int, ComboTrackRuntimeSnapshot>
                {
                    [3] = new ComboTrackRuntimeSnapshot(true, 1, "5LP", 0, 1, false, "5LP")
                });
            var ex = Assert.Throws<SnapshotPrepareException>(() => tracker.PrepareComboState(
                snapshot, new SnapshotPrepareContext(1, 2, 10, SnapshotRestoreMode.Normal)));
            Assert.Equal(SnapshotParticipantCatalog.Combo, ex.FaultPoint);
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_ActiveTrackWithInconsistentStartFrame_Rejects()
    {
        var tracker = Tracker();
        try
        {
            var snapshot = ActiveTrack(1, 99);
            var ex = Assert.Throws<SnapshotPrepareException>(() => tracker.PrepareComboState(
                snapshot, new SnapshotPrepareContext(1, 2, 10, SnapshotRestoreMode.Normal)));
            Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Prepare_ActiveTrackWithoutMoveIdentity_Rejects()
    {
        var tracker = Tracker();
        try
        {
            var snapshot = new ComboRuntimeSnapshot(
                new System.Collections.Generic.Dictionary<int, ComboTrackRuntimeSnapshot>
                {
                    [1] = new ComboTrackRuntimeSnapshot(true, 1, string.Empty, 0, 1, false, string.Empty)
                });
            Assert.Throws<SnapshotPrepareException>(() => tracker.PrepareComboState(
                snapshot, new SnapshotPrepareContext(1, 2, 10, SnapshotRestoreMode.Normal)));
        }
        finally { tracker.Shutdown(); }
    }

    [Fact]
    public void Install_ReplacesTracksWithoutResurrectingSubscriptions()
    {
        var tracker = Tracker();
        try
        {
            tracker.InstallComboState(ActiveTrack(1, 0));
            tracker.InstallComboState(new ComboRuntimeSnapshot(
                new System.Collections.Generic.Dictionary<int, ComboTrackRuntimeSnapshot>()));

            Assert.False(tracker.IsActive(1));
            Assert.Equal(0, tracker.GetHitCount(1));
            Assert.Equal(4, EventBus.Instance.GetSubscriberCount<HitConnectedEvent>() +
                             EventBus.Instance.GetSubscriberCount<FrameAdvancedEvent>() +
                             EventBus.Instance.GetSubscriberCount<MoveBlockedEvent>() +
                             EventBus.Instance.GetSubscriberCount<MoveFrameChangedEvent>());
        }
        finally { tracker.Shutdown(); }
    }
}
