#nullable enable

namespace FTG_Framework.Core;

public interface IFrameDataEngine
{
    void StartMove(int playerId, string moveId);
    void Update();
    MovePhase GetPhase(int playerId);
    int GetCurrentFrame(int playerId);
    string? GetCurrentMoveId(int playerId);
    bool RestoreFrame(int frameNumber);
    int EarliestSnapshotFrame { get; }
}
