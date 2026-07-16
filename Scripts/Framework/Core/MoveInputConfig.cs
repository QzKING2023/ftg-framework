#nullable enable
using System;

namespace FTG_Framework.Core;

public sealed class MoveInputConfig
{
    public string MoveId { get; init; } = string.Empty;
    public DirectionValue[][] AcceptedSequences { get; init; } = Array.Empty<DirectionValue[]>();
    public ButtonValue RequiredButton { get; init; }
}
