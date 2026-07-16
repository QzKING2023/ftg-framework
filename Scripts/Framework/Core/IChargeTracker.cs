#nullable enable

namespace FTG_Framework.Core;

public interface IChargeTracker
{
    int RetentionFrames { get; }
    void Update(int playerId, int currentFrame);
    bool IsChargeValid(int playerId, DirectionValue chargeDirection, int minDuration, int currentFrame);
    int GetChargeDuration(int playerId, DirectionValue chargeDirection, int currentFrame);
}