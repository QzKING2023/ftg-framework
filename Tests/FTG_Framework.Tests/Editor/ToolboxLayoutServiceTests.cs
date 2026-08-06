#nullable enable
using FTG_Framework.Data;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-AC08/E4.3-U: the workspace-layout value is a versioned UI preference.
/// Invalid or incompatible layout data falls back to the documented default
/// without affecting move/runtime data.
/// </summary>
public sealed class ToolboxLayoutServiceTests
{
    [Fact]
    public void DefaultLayout_HasAllPanelsVisibleInCanonicalOrder()
    {
        WorkspaceLayoutData layout = ToolboxLayoutService.DefaultLayout();

        Assert.Equal(ToolboxLayoutService.CurrentVersion, layout.Version);
        Assert.Collection(layout.Panels,
            entry => Assert.Equal(ToolboxPanelKind.MoveAuthoring, entry.Kind),
            entry => Assert.Equal(ToolboxPanelKind.EventBusDebug, entry.Kind),
            entry => Assert.Equal(ToolboxPanelKind.RuntimeTuning, entry.Kind));
        Assert.All(layout.Panels, entry => Assert.True(entry.Visible));
        Assert.Equal(1, layout.Panels[0].Order);
        Assert.Equal(2, layout.Panels[1].Order);
        Assert.Equal(3, layout.Panels[2].Order);
    }

    [Fact]
    public void SerializeParse_RoundTripsDefaultLayout()
    {
        WorkspaceLayoutData layout = ToolboxLayoutService.DefaultLayout();

        WorkspaceLayoutData parsed = ToolboxLayoutService.Parse(ToolboxLayoutService.Serialize(layout));

        Assert.True(ToolboxLayoutService.SameLayout(layout, parsed));
    }

    [Fact]
    public void SerializeParse_RoundTripsCustomLayout()
    {
        var layout = new WorkspaceLayoutData(1, new[]
        {
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.RuntimeTuning, Visible: false, Order: 1, SizeProportion: 0.40f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.MoveAuthoring, Visible: true, Order: 2, SizeProportion: 0.45f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.EventBusDebug, Visible: true, Order: 3, SizeProportion: 0.15f)
        });

        WorkspaceLayoutData parsed = ToolboxLayoutService.Parse(ToolboxLayoutService.Serialize(layout));

        Assert.True(ToolboxLayoutService.SameLayout(layout, parsed));
        Assert.False(ToolboxLayoutService.EntryFor(parsed, ToolboxPanelKind.RuntimeTuning).Visible);
    }

    [Fact]
    public void Serialize_OrdersEntriesByOrderField()
    {
        var layout = new WorkspaceLayoutData(1, new[]
        {
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.RuntimeTuning, Visible: true, Order: 3, SizeProportion: 0.25f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.MoveAuthoring, Visible: true, Order: 1, SizeProportion: 0.50f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.EventBusDebug, Visible: true, Order: 2, SizeProportion: 0.25f)
        });

        string serialized = ToolboxLayoutService.Serialize(layout);

        Assert.StartsWith("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25",
            serialized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus-prefix/v2;MoveAuthoring=1,1,0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,2")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=abc,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,0,0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,-1,0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,1.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,-0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,2,0.25;RuntimeTuning=1,3,0.25;Extra=1,4,0.10")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;MoveAuthoring=1,2,0.25;RuntimeTuning=1,3,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,2,0.25")]
    [InlineData("ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,1,0.25;RuntimeTuning=1,2,0.25")]
    public void Parse_InvalidOrIncompatibleValue_FallsBackToDefault(string? serialized)
    {
        WorkspaceLayoutData parsed = ToolboxLayoutService.Parse(serialized);

        Assert.True(ToolboxLayoutService.SameLayout(ToolboxLayoutService.DefaultLayout(), parsed));
    }

    [Fact]
    public void Parse_TotalSizeOutOfRange_FallsBackToDefault()
    {
        // 0.50 + 0.50 + 0.50 = 1.50 exceeds the documented 1.001 ceiling.
        string oversized = "ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.50;EventBusDebug=1,2,0.50;RuntimeTuning=1,3,0.50";
        // 0.00 + 0.00 + 0.00 = 0.00 is below the documented 0.001 floor.
        string undersized = "ftg-toolbox-layout/v1;MoveAuthoring=1,1,0.00;EventBusDebug=1,2,0.00;RuntimeTuning=1,3,0.00";

        Assert.True(ToolboxLayoutService.SameLayout(
            ToolboxLayoutService.DefaultLayout(), ToolboxLayoutService.Parse(oversized)));
        Assert.True(ToolboxLayoutService.SameLayout(
            ToolboxLayoutService.DefaultLayout(), ToolboxLayoutService.Parse(undersized)));
    }

    [Fact]
    public void Parse_InvalidLayoutDoesNotAffectMoveOrRuntimeData()
    {
        var store = new DataStore([new MoveDefinition { MoveId = "5A", Startup = 5, Active = 3, Recovery = 7 }],
            knockbackProfiles: [new KnockbackProfile { ProfileId = "light", Horizontal = 8.0f, Vertical = 3.0f }]);
        MoveDefinition? moveBefore = store.GetMove("5A");
        KnockbackProfile? profileBefore = store.GetKnockbackProfile("light");

        ToolboxLayoutService.Parse("corrupt;garbage");

        Assert.Same(moveBefore, store.GetMove("5A"));
        Assert.Same(profileBefore, store.GetKnockbackProfile("light"));
    }

    [Fact]
    public void EditorSettingsKey_IsVersioned_AndMatchesFormatPrefix()
    {
        // E4.3-E: the persistence boundary stores the versioned value under a
        // versioned key, so a future format bump can neither reuse nor collide
        // with the current key, and the key encodes the same version the parser
        // validates against.
        Assert.Contains("_v" + ToolboxLayoutService.CurrentVersion, ToolboxLayoutService.EditorSettingsKey);
        Assert.Contains("/v" + ToolboxLayoutService.CurrentVersion, ToolboxLayoutService.FormatPrefix);
    }

    [Fact]
    public void EntryFor_ReturnsRequestedKindEntry()
    {
        WorkspaceLayoutData layout = ToolboxLayoutService.DefaultLayout();

        Assert.Equal(ToolboxPanelKind.RuntimeTuning,
            ToolboxLayoutService.EntryFor(layout, ToolboxPanelKind.RuntimeTuning).Kind);
        Assert.Equal(0.25f,
            ToolboxLayoutService.EntryFor(layout, ToolboxPanelKind.RuntimeTuning).SizeProportion, precision: 5);
    }

    [Fact]
    public void SameLayout_DifferentSizesWithinTolerance_AreEqual()
    {
        var left = new WorkspaceLayoutData(1, new[]
        {
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.MoveAuthoring, true, 1, 0.50000f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.EventBusDebug, true, 2, 0.25000f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.RuntimeTuning, true, 3, 0.25000f)
        });
        var right = new WorkspaceLayoutData(1, new[]
        {
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.MoveAuthoring, true, 1, 0.50009f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.EventBusDebug, true, 2, 0.25000f),
            new ToolboxPanelLayoutEntry(ToolboxPanelKind.RuntimeTuning, true, 3, 0.24991f)
        });

        Assert.True(ToolboxLayoutService.SameLayout(left, right));
    }
}
