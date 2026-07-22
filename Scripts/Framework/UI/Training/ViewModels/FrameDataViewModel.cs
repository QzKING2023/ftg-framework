#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class FrameDataViewModel
{
    public string InfoText { get; private set; } = "Move: Idle";
    public string DurationsText { get; private set; } = "";
    public bool DurationsVisible { get; private set; }

    public void Update(IDataStore? dataStore, string? moveId, MovePhase phase, int currentFrame, int totalFrames)
    {
        if (phase == MovePhase.Idle)
        {
            InfoText = "Move: Idle";
            DurationsVisible = false;
            return;
        }

        var moveDef = dataStore?.GetMove(moveId ?? string.Empty);
        if (moveDef == null)
        {
            InfoText = $"Move: {moveId} (unknown) | Phase: {phase} | Frame: {currentFrame}/{totalFrames}";
            DurationsVisible = false;
            return;
        }

        int withinPhase = phase switch
        {
            MovePhase.Startup => currentFrame,
            MovePhase.Active => currentFrame - moveDef.Startup,
            MovePhase.Recovery => currentFrame - moveDef.Startup - moveDef.Active,
            _ => 0
        };

        int phaseTotal = phase switch
        {
            MovePhase.Startup => moveDef.Startup,
            MovePhase.Active => moveDef.Active,
            MovePhase.Recovery => moveDef.Recovery,
            _ => 0
        };

        InfoText = $"Move: {moveId} | Phase: {phase} | Frame: {withinPhase}/{phaseTotal}";
        DurationsText = $"Startup: {moveDef.Startup}f | Active: {moveDef.Active}f | Recovery: {moveDef.Recovery}f";
        DurationsVisible = true;
    }
}
