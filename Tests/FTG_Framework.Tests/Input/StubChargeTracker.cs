#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

internal sealed class StubChargeTracker : IChargeTracker
{
    public bool IsValidResult { get; set; } = true;
    public int DurationResult { get; set; }
    public int RetentionFrames => 5;

    public int LastCheckedPlayerId { get; private set; }
    public DirectionValue LastCheckedDirection { get; private set; }
    public int LastCheckedMinDuration { get; private set; }

    public void Update(int playerId, int currentFrame) { }

    public bool IsChargeValid(int playerId, DirectionValue chargeDirection, int minDuration, int currentFrame)
    {
        LastCheckedPlayerId = playerId;
        LastCheckedDirection = chargeDirection;
        LastCheckedMinDuration = minDuration;
        return IsValidResult;
    }

    public int GetChargeDuration(int playerId, DirectionValue chargeDirection, int currentFrame) =>
        DurationResult;

    public IDataStore DataStore => null!;
    public void Initialize(IDataStore dataStore) { }
    public void Shutdown() { }
}
