#nullable enable

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class AdvantageViewModel
{
    private int _advantage;

    public int TrackedPlayer { get; }
    public bool ShowWhenZero { get; }

    public int Advantage => _advantage;
    public string DisplayText { get; private set; } = "0";
    public bool IsVisible { get; private set; } = true;

    public AdvantageViewModel(int trackedPlayer = 1, bool showWhenZero = true)
    {
        TrackedPlayer = trackedPlayer;
        ShowWhenZero = showWhenZero;
        UpdateDisplay();
    }

    public void OnFrameAdvanced()
    {
        if (_advantage > 0)
            _advantage--;
        else if (_advantage < 0)
            _advantage++;
        UpdateDisplay();
    }

    public void OnHitConnected(int attackerId, int defenderId, int hitAdvantage)
    {
        if (attackerId == TrackedPlayer)
            _advantage = hitAdvantage;
        else if (defenderId == TrackedPlayer)
            _advantage = -hitAdvantage;
        else
            return;
        UpdateDisplay();
    }

    public void OnMoveBlocked(int attackerId, int defenderId, int blockAdvantage)
    {
        if (attackerId == TrackedPlayer)
            _advantage = blockAdvantage;
        else if (defenderId == TrackedPlayer)
            _advantage = -blockAdvantage;
        else
            return;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        IsVisible = _advantage != 0 || ShowWhenZero;
        DisplayText = IsVisible
            ? (_advantage > 0 ? $"+{_advantage}" : $"{_advantage}")
            : "0";
    }
}
