#nullable enable
using System.Text.Json.Serialization;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Core.Replay;

[JsonSerializable(typeof(CancelWindowEnteredEvent))]
[JsonSerializable(typeof(CancelWindowExitedEvent))]
[JsonSerializable(typeof(CharacterSelectedEvent))]
[JsonSerializable(typeof(ChargeStateChangedEvent))]
[JsonSerializable(typeof(ComboEndedEvent))]
[JsonSerializable(typeof(ComboStartedEvent))]
[JsonSerializable(typeof(FrameAdvancedEvent))]
[JsonSerializable(typeof(FrameRewoundEvent))]
[JsonSerializable(typeof(HitConnectedEvent))]
[JsonSerializable(typeof(InputBufferExpiredEvent))]
[JsonSerializable(typeof(InputReceivedEvent))]
[JsonSerializable(typeof(MatchInitializedEvent))]
[JsonSerializable(typeof(MoveBlockedEvent))]
[JsonSerializable(typeof(MoveCanceledEvent))]
[JsonSerializable(typeof(MoveFrameChangedEvent))]
[JsonSerializable(typeof(ReplayEndedEvent))]
[JsonSerializable(typeof(ReplayPausedEvent))]
[JsonSerializable(typeof(ReplayStartedEvent))]
[JsonSerializable(typeof(ReplayFile))]
[JsonSerializable(typeof(ReplayEntry))]
internal partial class ReplayEventJsonContext : JsonSerializerContext
{
}
