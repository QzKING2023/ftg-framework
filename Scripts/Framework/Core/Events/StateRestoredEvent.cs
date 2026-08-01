#nullable enable

namespace FTG_Framework.Core.Events;

/// <summary>Observe-only notification emitted after a successful normal state restore.</summary>
public readonly record struct StateRestoredEvent(int Frame, ulong SourceEpoch);
