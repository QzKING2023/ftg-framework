#nullable enable
namespace FTG_Framework.Core.Events;

public readonly record struct ReplayStartedEvent(int TotalFrames, int DataVersion);
