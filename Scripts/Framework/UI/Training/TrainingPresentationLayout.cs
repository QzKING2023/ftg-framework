#nullable enable
using System;

namespace FTG_Framework.UI.Training;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Intersects(LayoutRect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

public readonly record struct WorldPresentationLayout(
    double Scale,
    double CameraCenterX,
    double CameraCenterY,
    LayoutRect DesignRegion,
    LayoutRect VisibleWorld)
{
    public double ScaleX => Scale;
    public double ScaleY => Scale;
}

public readonly record struct TrainingUiLayout(
    double UiScale,
    double SafeMargin,
    double Gap,
    bool LeftCollapsed,
    LayoutRect LeftDiagnostics,
    LayoutRect LeftDiagnosticsDrawer,
    LayoutRect TopRightTuning,
    LayoutRect BottomRightPlayback);

public static class TrainingPresentationLayout
{
    public const double DesignWidth = 1152;
    public const double DesignHeight = 648;
    public const double MinimumWidth = 960;
    public const double MinimumHeight = 540;

    public static WorldPresentationLayout CalculateWorld(double width, double height)
    {
        ValidateFinitePositive(width, height);
        double scale = Math.Min(width / DesignWidth, height / DesignHeight);
        double designWidth = DesignWidth * scale;
        double designHeight = DesignHeight * scale;
        var designRegion = new LayoutRect(
            (width - designWidth) / 2,
            (height - designHeight) / 2,
            designWidth,
            designHeight);
        double visibleWidth = width / scale;
        double visibleHeight = height / scale;
        var visibleWorld = new LayoutRect(
            (DesignWidth - visibleWidth) / 2,
            (DesignHeight - visibleHeight) / 2,
            visibleWidth,
            visibleHeight);
        return new WorldPresentationLayout(
            scale, DesignWidth / 2, DesignHeight / 2, designRegion, visibleWorld);
    }

    public static TrainingUiLayout CalculateUi(double width, double height, double uiScale)
    {
        ValidateFinitePositive(width, height);
        if (width < MinimumWidth || height < MinimumHeight)
            throw new ArgumentOutOfRangeException(nameof(width),
                $"[TrainingLayout] Viewport must be at least {MinimumWidth}x{MinimumHeight}.");
        if (!double.IsFinite(uiScale) || uiScale is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(uiScale),
                "[TrainingLayout] UI scale must be finite and between 1.0 and 2.0.");

        double margin = 12 * uiScale;
        double gap = 8 * uiScale;
        double usableWidth = width - 2 * margin;
        double usableHeight = height - 2 * margin;
        double preferredRight = 390 * uiScale;
        double preferredLeft = 360 * uiScale;
        double collapsedLeft = 32 * uiScale;
        bool collapse = preferredLeft + gap + preferredRight > usableWidth;
        double leftWidth = collapse ? collapsedLeft : preferredLeft;
        double rightWidth = Math.Min(preferredRight, usableWidth - leftWidth - gap);
        if (rightWidth <= 0 || usableHeight <= gap)
            throw new ArgumentOutOfRangeException(nameof(width),
                "[TrainingLayout] Viewport cannot contain the collapsed training UI contract.");

        double rightX = width - margin - rightWidth;
        double panelHeight = (usableHeight - gap) / 2;
        var left = new LayoutRect(margin, margin, leftWidth, usableHeight);
        var drawer = new LayoutRect(
            margin,
            margin,
            collapse ? Math.Max(collapsedLeft, rightX - gap - margin) : leftWidth,
            usableHeight);
        var tuning = new LayoutRect(rightX, margin, rightWidth, panelHeight);
        var playback = new LayoutRect(rightX, margin + panelHeight + gap, rightWidth, panelHeight);
        return new TrainingUiLayout(uiScale, margin, gap, collapse, left, drawer, tuning, playback);
    }

    private static void ValidateFinitePositive(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width),
                "[TrainingLayout] Viewport dimensions must be finite and positive.");
    }
}
