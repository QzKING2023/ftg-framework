#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public class CancelWindowTrackerTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private static CancelWindow MakeWindow(int startFrame, int endFrame, string targetCategory = "special") => new()
    {
        StartFrame = startFrame,
        EndFrame = endFrame,
        TargetCategory = targetCategory
    };

    private static MoveDefinition MakeMove(string moveId, params CancelWindow[] windows) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
        CancelWindows = new List<CancelWindow>(windows)
    };

    private static MoveDefinition MakeMoveWithNoWindows(string moveId) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7
    };

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    // --- 3.2: Single window [3,7] ---

    [Fact]
    public void SingleWindow_EnteredFiresExactlyOnce_ExitedFiresAtEndPlusOne()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 7, "special"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            // Frames 0-2: window not yet open
            for (int f = 0; f <= 2; f++)
                tracker.EvaluateFrame(1, "test", f);

            // Frame 3: window opens
            tracker.EvaluateFrame(1, "test", 3);

            // Frames 4-7: window stays open, no duplicate events
            for (int f = 4; f <= 7; f++)
                tracker.EvaluateFrame(1, "test", f);

            // Frame 8: window closes
            tracker.EvaluateFrame(1, "test", 8);
        });

        var enter = Assert.Single(entered);
        Assert.Equal(1, enter.PlayerId);
        Assert.Equal("test", enter.MoveId);
        Assert.Equal("special", enter.Category);
        Assert.Equal(3, enter.StartFrame);
        Assert.Equal(7, enter.EndFrame);

        var exit = Assert.Single(exited);
        Assert.Equal(1, exit.PlayerId);
        Assert.Equal("test", exit.MoveId);
        Assert.Equal("special", exit.Category);
    }

    [Fact]
    public void SingleWindow_NoDuplicateEnteredOnSubsequentFrames()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 7));
        tracker.TrackMove(1, move);

        var entered = EventBusTestHelper.Collect<CancelWindowEnteredEvent>(() =>
        {
            tracker.EvaluateFrame(1, "test", 3);
            tracker.EvaluateFrame(1, "test", 5);
            tracker.EvaluateFrame(1, "test", 7);
        });

        Assert.Single(entered);
    }

    // --- 3.3: Two windows different categories ---

    [Fact]
    public void TwoWindows_DifferentCategories_EventsAtExactBoundaryFrames()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test",
            MakeWindow(3, 5, "special"),
            MakeWindow(6, 8, "super"));
        tracker.TrackMove(1, move);

        // Per-frame flush attributes each event to the frame that produced it.
        // Within one frame, phase-3 dispatch delivers Entered before Exited (AD-4).
        var log = new List<(int frame, string kind, string category)>();
        for (int f = 0; f <= 9; f++)
        {
            int frame = f;
            var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
                () => tracker.EvaluateFrame(1, "test", frame));
            foreach (var e in entered) log.Add((frame, "entered", e.Category));
            foreach (var e in exited) log.Add((frame, "exited", e.Category));
        }

        Assert.Equal(new[]
        {
            (3, "entered", "special"),
            (6, "entered", "super"),
            (6, "exited", "special"),
            (9, "exited", "super"),
        }, log);
    }

    // --- 3.4: Overlapping windows ---

    [Fact]
    public void OverlappingWindows_BothOpenSimultaneously_EachClosesAtOwnBoundary()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test",
            MakeWindow(8, 12, "special"),
            MakeWindow(10, 14, "super"));
        tracker.TrackMove(1, move);

        var log = new List<(int frame, string kind, string category)>();
        for (int f = 0; f <= 15; f++)
        {
            int frame = f;
            var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
                () => tracker.EvaluateFrame(1, "test", frame));
            foreach (var e in entered) log.Add((frame, "entered", e.Category));
            foreach (var e in exited) log.Add((frame, "exited", e.Category));
        }

        Assert.Equal(new[]
        {
            (8, "entered", "special"),
            (10, "entered", "super"),
            (13, "exited", "special"),
            (15, "exited", "super"),
        }, log);
    }

    [Fact]
    public void OverlappingWindows_Frames10to12_BothOpen()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test",
            MakeWindow(8, 12, "special"),
            MakeWindow(10, 14, "super"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 9; f++)
                tracker.EvaluateFrame(1, "test", f);
        });

        Assert.Single(entered);
        Assert.Empty(exited);

        var entered2 = EventBusTestHelper.Collect<CancelWindowEnteredEvent>(() =>
        {
            tracker.EvaluateFrame(1, "test", 10);
        });

        var secondOpen = Assert.Single(entered2);
        Assert.Equal("super", secondOpen.Category);
    }

    // --- 3.5: Empty cancel_windows ---

    [Fact]
    public void EmptyCancelWindows_ZeroEventsAcrossFullEvaluation()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMoveWithNoWindows("test");
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 14; f++)
                tracker.EvaluateFrame(1, "test", f);
        });

        Assert.Empty(entered);
        Assert.Empty(exited);
    }

    // --- 3.6: Window starting at frame 0 ---

    [Fact]
    public void WindowStartingAtZero_EnteredOnFirstEvaluatedFrame()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(0, 5));
        tracker.TrackMove(1, move);

        var entered = EventBusTestHelper.Collect<CancelWindowEnteredEvent>(() =>
        {
            tracker.EvaluateFrame(1, "test", 0);
        });

        var enter = Assert.Single(entered);
        Assert.Equal(0, enter.StartFrame);
        Assert.Equal(5, enter.EndFrame);
    }

    // --- 3.7: Window end beyond move duration ---

    [Fact]
    public void WindowEndBeyondMoveDuration_CloseAllPublishesExited_TrackerCleanForNextMove()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 20, "special"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            // Evaluate through the move (15 frames total: startup 5 + active 3 + recovery 7)
            for (int f = 0; f <= 14; f++)
                tracker.EvaluateFrame(1, "test", f);

            // Window is still open (20 > 14), CloseAll handles cleanup
            tracker.CloseAll(1, "test");
        });

        Assert.Single(entered);
        var close = Assert.Single(exited);
        Assert.Equal("special", close.Category);
        Assert.Equal("test", close.MoveId);

        // Tracker is clean for the next move — no stale events after CloseAll
        tracker.TrackMove(1, MakeMove("next", MakeWindow(0, 2, "super")));
        var (entered2, exited2) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 2; f++)
                tracker.EvaluateFrame(1, "next", f);
        });

        var next = Assert.Single(entered2);
        Assert.Equal("super", next.Category);
        Assert.Equal("next", next.MoveId);
        Assert.Empty(exited2);
    }

    [Fact]
    public void WindowEndBeyondMoveDuration_NoBoundaryExitedDuringMove()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 20, "special"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 14; f++)
                tracker.EvaluateFrame(1, "test", f);
        });

        Assert.Single(entered);
        Assert.Empty(exited); // No boundary exit because 20 > 14
    }

    // --- 3.8: TrackMove replaces previous state ---

    [Fact]
    public void TrackMove_NewMoveMidState_PublishesExitedForOpenWindows()
    {
        var tracker = new CancelWindowTracker();
        var firstMove = MakeMove("first", MakeWindow(3, 7, "special"));
        tracker.TrackMove(1, firstMove);
        tracker.EvaluateFrame(1, "first", 3); // opens the window; Entered left queued

        // Replacing the move mid-state must close the old window — every
        // Entered pairs with exactly one Exited, even on interruption.
        var secondMove = MakeMove("second", MakeWindow(5, 10, "super"));
        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
            () => tracker.TrackMove(1, secondMove));

        Assert.Empty(entered);
        var close = Assert.Single(exited);
        Assert.Equal("first", close.MoveId);
        Assert.Equal("special", close.Category);

        // The new move evaluates cleanly — only its own window fires
        var (entered2, exited2) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 11; f++)
                tracker.EvaluateFrame(1, "second", f);
        });

        var open = Assert.Single(entered2);
        Assert.Equal("super", open.Category);
        Assert.Equal(5, open.StartFrame);
        var boundaryClose = Assert.Single(exited2);
        Assert.Equal("second", boundaryClose.MoveId);
    }

    [Fact]
    public void TrackMove_NewMoveClearsOpenWindows_NoStaleEventsDuringNextEvaluation()
    {
        var tracker = new CancelWindowTracker();
        var firstMove = MakeMove("first", MakeWindow(3, 7, "special"));
        tracker.TrackMove(1, firstMove);

        var entered1 = EventBusTestHelper.Collect<CancelWindowEnteredEvent>(
            () => tracker.EvaluateFrame(1, "first", 3));
        Assert.Single(entered1);

        // TrackMove closes the old window immediately; the pairing Exited carries the old move id
        var exitedFromReplace = EventBusTestHelper.Collect<CancelWindowExitedEvent>(
            () => tracker.TrackMove(1, MakeMove("second", MakeWindow(0, 5, "super"))));
        var close = Assert.Single(exitedFromReplace);
        Assert.Equal("first", close.MoveId);

        // Evaluating the new move produces only its own events — nothing stale
        var (entered2, exited2) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
            () => tracker.EvaluateFrame(1, "second", 0));

        var open = Assert.Single(entered2);
        Assert.Equal("super", open.Category);
        Assert.Empty(exited2);
    }

    [Fact]
    public void TrackMove_NullMove_Throws()
    {
        var tracker = new CancelWindowTracker();
        Assert.Throws<ArgumentNullException>(() => tracker.TrackMove(1, null!));
    }

    // --- 3.9: Window with StartFrame beyond move duration ---

    [Fact]
    public void WindowStartBeyondMoveDuration_NeverOpens_ZeroEvents()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(20, 25, "special"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            for (int f = 0; f <= 14; f++)
                tracker.EvaluateFrame(1, "test", f);
        });

        Assert.Empty(entered);
        Assert.Empty(exited);
    }

    // --- CloseAll with no open windows is a no-op ---

    [Fact]
    public void CloseAll_WithNoOpenWindows_PublishesNothing()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 7));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            tracker.CloseAll(1, "test");
        });

        Assert.Empty(entered);
        Assert.Empty(exited);
    }

    // --- CloseAll with multiple open windows ---

    [Fact]
    public void CloseAll_MultipleOpenWindows_PublishesExitedForEachInDeterministicOrder()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test",
            MakeWindow(3, 10, "special"),
            MakeWindow(5, 10, "super"));
        tracker.TrackMove(1, move);

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            tracker.EvaluateFrame(1, "test", 5); // both windows now open
            tracker.CloseAll(1, "test");
        });

        Assert.Equal(2, entered.Count);
        // CloseAll publishes in window-index order; LIFO same-type dispatch reverses
        // the subscriber-visible sequence — deterministic every run.
        Assert.Equal(new[] { "super", "special" }, exited.ConvertAll(e => e.Category));
        Assert.All(exited, e => Assert.Equal("test", e.MoveId));
    }

    // --- ResyncFrame (rewind restore path) ---

    [Fact]
    public void ResyncFrame_WindowOpenAcrossRestore_EmitsNothing()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 7, "special"));
        tracker.TrackMove(1, move);
        tracker.EvaluateFrame(1, "test", 5); // window open
        EventBusTestHelper.Drain();

        // Restoring to a frame inside the same window must not emit a spurious
        // Exited+Entered pair — the window never closed on this branch.
        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
            () => tracker.ResyncFrame(1, move, 5));

        Assert.Empty(entered);
        Assert.Empty(exited);
    }

    [Fact]
    public void ResyncFrame_WindowClosedOnAbandonedBranch_ReopensWithEntered()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(3, 7, "special"));
        tracker.TrackMove(1, move);
        tracker.EvaluateFrame(1, "test", 3);
        tracker.EvaluateFrame(1, "test", 8); // closed on the abandoned future
        EventBusTestHelper.Drain();

        // Restored frame is inside the window, but the tracker closed it on the
        // abandoned branch — subscribers saw Exited, so they must see Entered.
        var entered = EventBusTestHelper.Collect<CancelWindowEnteredEvent>(
            () => tracker.ResyncFrame(1, move, 5));

        var open = Assert.Single(entered);
        Assert.Equal("special", open.Category);
        Assert.Equal("test", open.MoveId);
    }

    [Fact]
    public void ResyncFrame_WindowOpenedOnAbandonedBranch_ClosesWithExited()
    {
        var tracker = new CancelWindowTracker();
        var move = MakeMove("test", MakeWindow(6, 8, "super"));
        tracker.TrackMove(1, move);
        tracker.EvaluateFrame(1, "test", 6); // opened on the abandoned future
        EventBusTestHelper.Drain();

        // Restored frame is before the window — subscribers saw Entered, so
        // they must see Exited.
        var exited = EventBusTestHelper.Collect<CancelWindowExitedEvent>(
            () => tracker.ResyncFrame(1, move, 3));

        var close = Assert.Single(exited);
        Assert.Equal("super", close.Category);
    }

    [Fact]
    public void ResyncFrame_DifferentMoveOnAbandonedBranch_ClosesOldAndOpensNew()
    {
        var tracker = new CancelWindowTracker();
        var first = MakeMove("first", MakeWindow(0, 9, "special"));
        var second = MakeMove("second", MakeWindow(2, 4, "super"));
        tracker.TrackMove(1, first);
        tracker.EvaluateFrame(1, "first", 2); // first's window open on abandoned branch
        EventBusTestHelper.Drain();

        var (entered, exited) = EventBusTestHelper.Collect<CancelWindowEnteredEvent, CancelWindowExitedEvent>(
            () => tracker.ResyncFrame(1, second, 3));

        var close = Assert.Single(exited);
        Assert.Equal("first", close.MoveId);
        var open = Assert.Single(entered);
        Assert.Equal("second", open.MoveId);
        Assert.Equal("super", open.Category);
    }

    [Fact]
    public void ResyncFrame_NullMove_Throws()
    {
        var tracker = new CancelWindowTracker();
        Assert.Throws<ArgumentNullException>(() => tracker.ResyncFrame(1, null!, 0));
    }
}
