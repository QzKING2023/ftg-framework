#nullable enable
using System.Text.Json.Serialization;

namespace FTG_Framework.Core.Events;

public readonly record struct HitConnectedEvent
{
    public int AttackerId { get; init; }
    public int DefenderId { get; init; }
    public string MoveId { get; init; }
    public int HitAdvantage { get; init; }
    public int Damage { get; init; }
    [JsonPropertyName("ContactFrame")]
    public int? SerializedContactFrame { get; init; }
    [JsonIgnore]
    public int ContactFrame => SerializedContactFrame ?? -1;
    [JsonPropertyName("HitboxId")]
    public string? SerializedHitboxId { get; init; }
    [JsonIgnore]
    public string HitboxId => SerializedHitboxId ?? string.Empty;

    public HitConnectedEvent(
        int AttackerId, int DefenderId, string MoveId, int HitAdvantage, int Damage,
        int ContactFrame = -1, string? HitboxId = null)
    {
        this.AttackerId = AttackerId;
        this.DefenderId = DefenderId;
        this.MoveId = MoveId;
        this.HitAdvantage = HitAdvantage;
        this.Damage = Damage;
        SerializedContactFrame = ContactFrame < 0 ? null : ContactFrame;
        SerializedHitboxId = string.IsNullOrEmpty(HitboxId) ? null : HitboxId;
    }
}
