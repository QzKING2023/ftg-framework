#nullable enable

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class ControlsLegendViewModel
{
    public string GameplayText =>
        "Gameplay\nP1: A/D move, S crouch, Space jump, U 5LP\n" +
        "P2: Left/Right move, Down crouch, Up jump, N 5LP";
    public string BlockText =>
        "Block: hold Back before contact. Back is opposite the opponent-facing direction.\n" +
        "First run: move into range -> real 5LP hit -> recover -> hold P2 Back -> real 5LP block.";
    public string DiagnosticsText =>
        "Diagnostics (TEST ONLY)\nP pause | ] step | [ restore | O boxes | 1/2 input logs | H hitstun | B blockstun";
}
