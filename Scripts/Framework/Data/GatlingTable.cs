using System.Collections.Generic;

namespace FTG_Framework.Data;

public sealed class GatlingTable
{
    public string CharacterId { get; init; } = string.Empty;
    public IReadOnlyList<GatlingEntry> Entries { get; init; } = new List<GatlingEntry>();
}
