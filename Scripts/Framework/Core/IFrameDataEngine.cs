#nullable enable

namespace FTG_Framework.Core;

public interface IFrameDataEngine
{
    void StartMove(int playerId, string moveId);
    void RegisterHit(int attackerId, int defenderId, string moveId, bool isBlocked);
    void Update();
    MovePhase GetPhase(int playerId);
    int GetCurrentFrame(int playerId);
    string? GetCurrentMoveId(int playerId);
    void InterruptAndStart(int playerId, string moveId);
    bool RestoreFrame(int frameNumber);
    int EarliestSnapshotFrame { get; }
}
