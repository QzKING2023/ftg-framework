using Godot;

public partial class FrameRateManager : Node
{
    public override void _Ready()
    {
        Engine.MaxFps = 60;
    }
}
