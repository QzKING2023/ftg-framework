#nullable enable
using System.Text.Json.Serialization;

namespace FTG_Framework.Core.Events;

public readonly record struct MoveBlockedEvent
{
    public int AttackerId { get; init; }
    public int DefenderId { get; init; }
    public string MoveId { get; init; }
    public int BlockAdvantage { get; init; }
    public int Damage { get; init; }
    [JsonPropertyName("ContactFrame")]
    public int? SerializedContactFrame { get; init; }
    [JsonIgnore]
    public int ContactFrame => SerializedContactFrame ?? -1;

    public MoveBlockedEvent(
        int AttackerId, int DefenderId, string MoveId, int BlockAdvantage, int Damage,
        int ContactFrame = -1)
    {
        this.AttackerId = AttackerId;
        this.DefenderId = DefenderId;
        this.MoveId = MoveId;
        this.BlockAdvantage = BlockAdvantage;
        this.Damage = Damage;
        SerializedContactFrame = ContactFrame < 0 ? null : ContactFrame;
    }
}
