#nullable enable
using System;
using FTG_Framework.UI.Training;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

public sealed class TrainingPresentationLayoutTests
{
    [Theory]
    [InlineData(960, 540, 0.8333333333, 1152, 648)]
    [InlineData(1152, 648, 1, 1152, 648)]
    [InlineData(1280, 720, 1.1111111111, 1152, 648)]
    [InlineData(1920, 1080, 1.6666666667, 1152, 648)]
    [InlineData(2560, 1080, 1.6666666667, 1536, 648)]
    public void CalculateWorld_UsesUniformCenteredDesignRegionAndExpandsSurplus(
        float width, float height, double scale, float visibleWidth, float visibleHeight)
    {
        WorldPresentationLayout result = TrainingPresentationLayout.CalculateWorld(width, height);

        Assert.Equal(scale, result.Scale, 5);
        Assert.Equal(576, result.CameraCenterX);
        Assert.Equal(324, result.CameraCenterY);
        Assert.Equal(visibleWidth, result.VisibleWorld.Width, 3);
        Assert.Equal(visibleHeight, result.VisibleWorld.Height, 3);
        Assert.Equal(result.Scale, result.ScaleX);
        Assert.Equal(result.Scale, result.ScaleY);
        Assert.True(result.DesignRegion.Left >= 0);
        Assert.True(result.DesignRegion.Top >= 0);
        Assert.True(result.DesignRegion.Right <= width + 0.01);
        Assert.True(result.DesignRegion.Bottom <= height + 0.01);
    }

    [Theory]
    [InlineData(0, 648)]
    [InlineData(-1, 648)]
    [InlineData(1152, 0)]
    [InlineData(float.NaN, 648)]
    [InlineData(1152, float.PositiveInfinity)]
    public void CalculateWorld_RejectsInvalidViewport(float width, float height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainingPresentationLayout.CalculateWorld(width, height));

    public static TheoryData<float, float, float> RequiredUiMatrix => new()
    {
        { 960, 540, 1 }, { 960, 540, 1.5f }, { 960, 540, 2 },
        { 1152, 648, 1 }, { 1152, 648, 1.5f }, { 1152, 648, 2 },
        { 1280, 720, 1 }, { 1280, 720, 1.5f }, { 1280, 720, 2 },
        { 1920, 1080, 1 }, { 1920, 1080, 1.5f }, { 1920, 1080, 2 },
        { 2560, 1080, 1 }, { 2560, 1080, 1.5f }, { 2560, 1080, 2 }
    };

    [Theory]
    [MemberData(nameof(RequiredUiMatrix))]
    public void CalculateUi_ContainsSeparatedRegionsAtRequiredScales(
        float width, float height, float uiScale)
    {
        TrainingUiLayout result = TrainingPresentationLayout.CalculateUi(width, height, uiScale);

        Assert.True(result.LeftDiagnostics.Left >= result.SafeMargin - 0.01);
        Assert.True(result.TopRightTuning.Right <= width - result.SafeMargin + 0.01);
        Assert.True(result.BottomRightPlayback.Right <= width - result.SafeMargin + 0.01);
        Assert.True(result.TopRightTuning.Bottom + result.Gap <= result.BottomRightPlayback.Top + 0.01);
        Assert.False(result.LeftDiagnostics.Intersects(result.TopRightTuning));
        Assert.False(result.LeftDiagnostics.Intersects(result.BottomRightPlayback));
        Assert.False(result.LeftDiagnosticsDrawer.Intersects(result.TopRightTuning));
        Assert.False(result.LeftDiagnosticsDrawer.Intersects(result.BottomRightPlayback));
        Assert.Equal(12 * uiScale, result.SafeMargin, 3);
        Assert.Equal(8 * uiScale, result.Gap, 3);
        Assert.True(result.LeftDiagnosticsDrawer.Left >= result.SafeMargin - 0.01);
        Assert.True(result.LeftDiagnosticsDrawer.Right + result.Gap <= result.TopRightTuning.Left + 0.01);
        Assert.True(result.LeftDiagnostics.Width > 0);
        Assert.True(result.LeftDiagnosticsDrawer.Width >= result.LeftDiagnostics.Width);
        Assert.True(result.TopRightTuning.Height > 0);
        Assert.True(result.BottomRightPlayback.Height > 0);
        foreach (LayoutRect rect in new[]
                 {
                     result.LeftDiagnostics, result.LeftDiagnosticsDrawer,
                     result.TopRightTuning, result.BottomRightPlayback
                 })
        {
            Assert.True(rect.Left >= result.SafeMargin - 0.01);
            Assert.True(rect.Top >= result.SafeMargin - 0.01);
            Assert.True(rect.Right <= width - result.SafeMargin + 0.01);
            Assert.True(rect.Bottom <= height - result.SafeMargin + 0.01);
        }
    }

    [Fact]
    public void CalculateUi_MinimumViewportAtTwoHundredPercentCollapsesLeftRail()
    {
        TrainingUiLayout result = TrainingPresentationLayout.CalculateUi(960, 540, 2);

        Assert.True(result.LeftCollapsed);
        Assert.Equal(64, result.LeftDiagnostics.Width, 3);
        Assert.Equal(24, result.SafeMargin, 3);
        Assert.Equal(16, result.Gap, 3);
        Assert.Equal(780, result.TopRightTuning.Width, 3);
        Assert.Equal(116, result.LeftDiagnosticsDrawer.Width, 3);
    }

    [Theory]
    [InlineData(960, 540, 0)]
    [InlineData(960, 540, 2.1)]
    [InlineData(959, 540, 1)]
    [InlineData(960, 539, 1)]
    public void CalculateUi_RejectsUnsupportedInputs(float width, float height, float uiScale) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainingPresentationLayout.CalculateUi(width, height, uiScale));
}
