#nullable enable
using System;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-AC05/E4.3-F: errors are scoped per operation; a failure in one panel
/// never blocks other panels, and no panel reports success for an operation
/// that failed in another service.
/// </summary>
public sealed class ToolboxErrorRouterTests
{
    [Fact]
    public void ReportError_RecordsLastErrorAndRaisesEvent()
    {
        var router = new ToolboxErrorRouter();
        ToolboxError? observed = null;
        router.ErrorReported += error => observed = error;

        router.ReportError(ToolboxPanelKind.MoveAuthoring, "save", "disk full");

        Assert.NotNull(router.LastError(ToolboxPanelKind.MoveAuthoring));
        Assert.Equal(ToolboxPanelKind.MoveAuthoring, router.LastError(ToolboxPanelKind.MoveAuthoring)!.Source);
        Assert.Equal("save", router.LastError(ToolboxPanelKind.MoveAuthoring)!.Operation);
        Assert.Equal("disk full", router.LastError(ToolboxPanelKind.MoveAuthoring)!.Message);
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        Assert.Equal("disk full", observed!.Message);
    }

    [Fact]
    public void Errors_AreScopedPerSource()
    {
        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.MoveAuthoring, "save", "disk full");

        Assert.NotNull(router.LastError(ToolboxPanelKind.MoveAuthoring));
        Assert.Null(router.LastError(ToolboxPanelKind.RuntimeTuning));
        Assert.Null(router.LastError(ToolboxPanelKind.EventBusDebug));
        Assert.False(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
    }

    [Fact]
    public void ReportSuccess_DoesNotClearStickyFailure()
    {
        // Documented scoping semantics: failures are sticky per source; only an
        // explicit Clear clears them, so a recovered source cannot mask a
        // still-broken one.
        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.RuntimeTuning, "apply", "validation failed");

        router.ReportSuccess(ToolboxPanelKind.RuntimeTuning, "apply");

        Assert.True(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
    }

    [Fact]
    public void Clear_RemovesOnlyRequestedSource()
    {
        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.MoveAuthoring, "save", "disk full");
        router.ReportError(ToolboxPanelKind.RuntimeTuning, "apply", "validation failed");

        router.Clear(ToolboxPanelKind.MoveAuthoring);

        Assert.False(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
    }

    [Fact]
    public void ClearAll_RemovesAllSources()
    {
        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.MoveAuthoring, "save", "disk full");
        router.ReportError(ToolboxPanelKind.RuntimeTuning, "apply", "validation failed");

        router.ClearAll();

        Assert.False(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        Assert.False(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportError_RejectsNullOrWhitespaceOperation(string? operation)
    {
        var router = new ToolboxErrorRouter();

        Assert.ThrowsAny<ArgumentException>(() => router.ReportError(ToolboxPanelKind.MoveAuthoring, operation!, "boom"));
    }
}
