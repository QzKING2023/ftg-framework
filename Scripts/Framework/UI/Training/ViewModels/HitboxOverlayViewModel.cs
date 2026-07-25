#nullable enable
using System.Collections.Generic;
using Godot;

namespace FTG_Framework.UI.Training.ViewModels;

// Box storage for HitboxOverlay. Each Set replaces that category's boxes — the
// data provider repopulates every frame, so accumulation would duplicate stale geometry.
public sealed class HitboxOverlayViewModel
{
    public readonly record struct Box(Rect2 Rect, string Category);

    private readonly List<Box> _boxes = new();

    public IReadOnlyList<Box> Boxes => _boxes;
    public int BoxCount => _boxes.Count;
    public bool Enabled { get; set; }

    public void SetHitboxes(IReadOnlyList<Rect2> hitboxes)
    {
        _boxes.RemoveAll(b => b.Category == "Hit");
        foreach (var r in hitboxes)
            _boxes.Add(new Box(r, "Hit"));
    }

    public void SetHurtboxes(IReadOnlyList<Rect2> hurtboxes)
    {
        _boxes.RemoveAll(b => b.Category == "Hurt");
        foreach (var r in hurtboxes)
            _boxes.Add(new Box(r, "Hurt"));
    }

    public void Clear() => _boxes.Clear();
}
