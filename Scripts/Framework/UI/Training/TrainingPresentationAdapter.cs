#nullable enable
using System;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class TrainingPresentationAdapter : Node
{
    public Camera2D? WorldCamera { get; set; }
    public TestMatchPresentation? WorldPresentation { get; set; }
    public Control? TrainingUiRoot { get; set; }
    public Control? LeftDiagnosticsRegion { get; set; }
    public Control? LeftDiagnosticsDrawer { get; set; }
    public Button? DiagnosticsToggle { get; set; }
    public Button? DiagnosticsClose { get; set; }
    public Control? TopRightTuningRegion { get; set; }
    public Control? BottomRightPlaybackRegion { get; set; }
    public double UiScale { get; set; } = 1;

    public WorldPresentationLayout CurrentWorldLayout { get; private set; }
    public TrainingUiLayout CurrentUiLayout { get; private set; }
    public int LayoutRevision { get; private set; }
    private bool _drawerOpen;

    public override void _Ready()
    {
        GetViewport().SizeChanged += ApplyLayout;
        if (DiagnosticsToggle is not null) DiagnosticsToggle.Pressed += ToggleDiagnosticsDrawer;
        if (DiagnosticsClose is not null) DiagnosticsClose.Pressed += ToggleDiagnosticsDrawer;
        ApplyLayout();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= ApplyLayout;
        if (DiagnosticsToggle is not null) DiagnosticsToggle.Pressed -= ToggleDiagnosticsDrawer;
        if (DiagnosticsClose is not null) DiagnosticsClose.Pressed -= ToggleDiagnosticsDrawer;
    }

    public void ApplyLayout()
    {
        Vector2I size = GetWindow().Size;
        if (size.X < TrainingPresentationLayout.MinimumWidth ||
            size.Y < TrainingPresentationLayout.MinimumHeight)
            return;
        try
        {
            CurrentWorldLayout = TrainingPresentationLayout.CalculateWorld(size.X, size.Y);
            CurrentUiLayout = TrainingPresentationLayout.CalculateUi(size.X, size.Y, UiScale);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            GD.PushWarning(ex.Message);
            return;
        }

        if (WorldCamera is not null)
        {
            WorldCamera.Position = new Vector2(
                (float)CurrentWorldLayout.CameraCenterX,
                (float)CurrentWorldLayout.CameraCenterY);
            float zoom = (float)CurrentWorldLayout.Scale;
            WorldCamera.Zoom = new Vector2(zoom, zoom);
        }
        if (WorldPresentation is not null)
            WorldPresentation.VisibleWorldRect = CurrentWorldLayout.VisibleWorld;

        if (TrainingUiRoot is not null)
        {
            TrainingUiRoot.Position = Vector2.Zero;
        }
        ApplyRect(LeftDiagnosticsRegion, CurrentUiLayout.LeftDiagnostics);
        ApplyRect(LeftDiagnosticsDrawer, CurrentUiLayout.LeftDiagnosticsDrawer);
        ApplyRect(TopRightTuningRegion, CurrentUiLayout.TopRightTuning);
        ApplyRect(BottomRightPlaybackRegion, CurrentUiLayout.BottomRightPlayback);
        ApplyDiagnosticsVisibility();
        LayoutRevision++;
    }

    internal void ToggleDiagnosticsDrawer()
    {
        if (!CurrentUiLayout.LeftCollapsed) return;
        _drawerOpen = !_drawerOpen;
        ApplyDiagnosticsVisibility();
    }

    private void ApplyDiagnosticsVisibility()
    {
        bool collapsed = CurrentUiLayout.LeftCollapsed;
        if (LeftDiagnosticsRegion is not null) LeftDiagnosticsRegion.Visible = collapsed;
        if (DiagnosticsToggle is not null) DiagnosticsToggle.Visible = collapsed;
        if (DiagnosticsClose is not null) DiagnosticsClose.Visible = collapsed;
        if (LeftDiagnosticsDrawer is not null)
            LeftDiagnosticsDrawer.Visible = !collapsed || _drawerOpen;
    }

    private static void ApplyRect(Control? control, LayoutRect rect)
    {
        if (control is null) return;
        control.Position = new Vector2((float)rect.X, (float)rect.Y);
        control.Size = new Vector2((float)rect.Width, (float)rect.Height);
    }
}
