using System.Collections.Generic;

namespace FTG_Framework.Data;

public sealed class GatlingEntry
{
    public string SourceMove { get; init; } = string.Empty;
    public IReadOnlyList<string> TargetMoves { get; init; } = new List<string>();
    public string CancelCategory { get; init; } = string.Empty;
}
