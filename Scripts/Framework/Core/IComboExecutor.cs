#nullable enable

namespace FTG_Framework.Core;

public interface IComboExecutor
{
    bool CanCancel(string characterId, string fromMoveId, string toMoveId, string cancelCategory);
    bool TryCancel(int playerId, string candidateMoveId);
    void CaptureTableForWindow(string characterId);
    void ReleaseTableForWindow(string characterId);
}
