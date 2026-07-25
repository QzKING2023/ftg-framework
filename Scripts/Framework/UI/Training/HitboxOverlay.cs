#nullable enable
using Godot;
using System.Collections.Generic;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class HitboxOverlay : Control
{
    [Export] public Color OverlayColor { get; set; } = new(0, 1, 0, 0.4f);
    [Export] public Color HurtboxColor { get; set; } = new(1, 0, 0, 0.4f);
    [Export] public float LineWidth { get; set; } = 2f;

    [Export]
    public bool Enabled
    {
        get => _vm.Enabled;
        set
        {
            _vm.Enabled = value;
            QueueRedraw();
        }
    }

    private readonly HitboxOverlayViewModel _vm = new();

    internal int BoxCount => _vm.BoxCount;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public void SetHitboxes(IReadOnlyList<Rect2> hitboxes)
    {
        _vm.SetHitboxes(hitboxes);
        QueueRedraw();
    }

    public void SetHurtboxes(IReadOnlyList<Rect2> hurtboxes)
    {
        _vm.SetHurtboxes(hurtboxes);
        QueueRedraw();
    }

    public void Clear()
    {
        _vm.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_vm.Enabled)
            return;

        foreach (var box in _vm.Boxes)
        {
            var color = box.Category == "Hit" ? OverlayColor : HurtboxColor;
            DrawRect(box.Rect, color, filled: false, LineWidth);
        }
    }
}
