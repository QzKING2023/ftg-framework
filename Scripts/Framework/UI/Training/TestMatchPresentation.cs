#nullable enable
using FTG_Framework.Characters;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class TestMatchPresentation : Node2D
{
    private CharacterController? _p1;
    private CharacterController? _p2;

    public void Bind(CharacterController p1, CharacterController p2)
    {
        _p1 = p1;
        _p2 = p2;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_p1 is null || _p2 is null) return;
        float groundY = Mathf.Max(_p1.GlobalPosition.Y, _p2.GlobalPosition.Y) + 25;
        DrawLine(new Vector2(0, groundY), new Vector2(GetViewportRect().Size.X, groundY), Colors.White, 3);
        DrawCharacter(_p1.GlobalPosition, true);
        DrawCharacter(_p2.GlobalPosition, false);
    }

    private void DrawCharacter(Vector2 position, bool p1)
    {
        if (p1)
        {
            DrawRect(new Rect2(position - new Vector2(18, 45), new Vector2(36, 70)),
                new Color(0.2f, 0.65f, 1f, 0.55f), true);
            DrawString(ThemeDB.FallbackFont, position + new Vector2(-10, -50), "P1");
        }
        else
        {
            Vector2[] diamond =
            [
                position + new Vector2(0, -45), position + new Vector2(22, -10),
                position + new Vector2(0, 25), position + new Vector2(-22, -10),
                position + new Vector2(0, -45)
            ];
            DrawPolyline(diamond, new Color(1f, 0.6f, 0.2f), 5);
            DrawString(ThemeDB.FallbackFont, position + new Vector2(-10, -50), "P2");
        }
    }
}
