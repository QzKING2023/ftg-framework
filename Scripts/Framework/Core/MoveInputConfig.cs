#nullable enable
using System;

namespace FTG_Framework.Core;

public sealed class MoveInputConfig
{
    public string MoveId { get; init; } = string.Empty;
    public DirectionValue[][] AcceptedSequences { get; init; } = Array.Empty<DirectionValue[]>();
    public ButtonValue RequiredButton { get; init; }
    public DirectionValue? ChargeDirection { get; init; }
    public int MinChargeDuration { get; init; }
    public MoveCategory Category { get; init; } = MoveCategory.Normal;
}
