#nullable enable
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Physics;

internal readonly record struct WorldCollisionBox(
    string BoxId, float CenterX, float CenterY, float Width, float Height);

internal static class CollisionDetector
{
    internal static WorldCollisionBox ToWorld(
        CollisionBoxDefinition box, float originX, float originY, bool facingRight)
    {
        float centerX = originX + (facingRight ? box.X : -box.X);
        return new WorldCollisionBox(box.BoxId, centerX, originY + box.Y, box.Width, box.Height);
    }

    internal static bool Overlaps(in WorldCollisionBox a, in WorldCollisionBox b)
    {
        if (a.Width <= 0 || a.Height <= 0 || b.Width <= 0 || b.Height <= 0)
            return false;
        float aLeft = a.CenterX - a.Width * 0.5f;
        float aRight = a.CenterX + a.Width * 0.5f;
        float aTop = a.CenterY - a.Height * 0.5f;
        float aBottom = a.CenterY + a.Height * 0.5f;
        float bLeft = b.CenterX - b.Width * 0.5f;
        float bRight = b.CenterX + b.Width * 0.5f;
        float bTop = b.CenterY - b.Height * 0.5f;
        float bBottom = b.CenterY + b.Height * 0.5f;
        return aLeft < bRight && aRight > bLeft && aTop < bBottom && aBottom > bTop;
    }
}
