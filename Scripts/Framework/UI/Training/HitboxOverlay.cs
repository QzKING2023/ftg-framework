#nullable enable
using Godot;
using System.Collections.Generic;

namespace FTG_Framework.UI.Training;

public partial class HitboxOverlay : Control
{
    [Export] public Color OverlayColor { get; set; } = new(0, 1, 0, 0.4f);
    [Export] public Color HurtboxColor { get; set; } = new(1, 0, 0, 0.4f);
    [Export] public float LineWidth { get; set; } = 2f;

    private bool _enabled;

    [Export]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            QueueRedraw();
        }
    }

    private readonly List<(Rect2 Rect, Color Color, string Label)> _boxes = new();

    internal int BoxCount => _boxes.Count;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    // Each Set replaces that category's boxes — the data provider repopulates every
    // frame, so accumulation would duplicate stale geometry.
    public void SetHitboxes(IReadOnlyList<Rect2> hitboxes)
    {
        _boxes.RemoveAll(b => b.Label == "Hit");
        foreach (var r in hitboxes)
            _boxes.Add((r, OverlayColor, "Hit"));
        QueueRedraw();
    }

    public void SetHurtboxes(IReadOnlyList<Rect2> hurtboxes)
    {
        _boxes.RemoveAll(b => b.Label == "Hurt");
        foreach (var r in hurtboxes)
            _boxes.Add((r, HurtboxColor, "Hurt"));
        QueueRedraw();
    }

    public void Clear()
    {
        _boxes.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Enabled)
            return;

        foreach (var (rect, color, _) in _boxes)
        {
            DrawRect(rect, color, filled: false, LineWidth);
        }
    }
}
