#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct ComboEndedEvent(int PlayerId, int TotalHits, string FinalMove);
