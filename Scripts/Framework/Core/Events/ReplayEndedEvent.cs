#nullable enable
namespace FTG_Framework.Core.Events;

public readonly record struct ReplayEndedEvent(int TotalFramesPlayed);
