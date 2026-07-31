#nullable enable

namespace FTG_Framework.Core;

public interface IFrameDataEngine
{
    void StartMove(int playerId, string moveId);
    void Update();
    MovePhase GetPhase(int playerId);
    int GetCurrentFrame(int playerId);
    string? GetCurrentMoveId(int playerId);
    EvaluatedMoveFrame GetLastEvaluatedFrame(int playerId);
    void InterruptAndStart(int playerId, string moveId);
    bool RestoreFrame(int frameNumber);
    int EarliestSnapshotFrame { get; }

    /// <summary>
    /// Returns the snapshot at the given frame for replay recording.
    /// Returns null if no snapshot exists for that frame.
    /// </summary>
    FrameStateSnapshot? TryGetSnapshot(int frameNumber);

    /// <summary>
    /// Restores internal state from a replay snapshot without publishing
    /// FrameRewoundEvent or calling RewindFrameCounter. Used for replay start,
    /// not UI rewind.
    /// </summary>
    void RestoreFromReplaySnapshot(FrameStateSnapshot snapshot);
}
