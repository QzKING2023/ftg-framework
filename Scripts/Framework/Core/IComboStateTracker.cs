#nullable enable
using System;

namespace FTG_Framework.Core;

public interface IComboStateTracker
{
    bool IsActive(int playerId);
    int GetHitCount(int playerId);
    string? GetCurrentMoveId(int playerId);
    int GetComboStartFrame(int playerId);
    void SetOnComboHit(Action<string, int>? callback);
}
