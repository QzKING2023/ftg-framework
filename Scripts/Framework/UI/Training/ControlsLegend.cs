#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class ControlsLegend : Control
{
    private readonly ControlsLegendViewModel _viewModel = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 140);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer
        {
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(content);
        foreach (string text in new[] { _viewModel.GameplayText, _viewModel.BlockText, _viewModel.DiagnosticsText })
        {
            content.AddChild(new Label
            {
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            });
        }
    }
}
