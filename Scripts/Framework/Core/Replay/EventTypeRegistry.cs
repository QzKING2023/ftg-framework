#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// String-based event type registry for replay serialization.
/// Maps event type names to CLR types and back.
/// </summary>
public static class EventTypeRegistry
{
    public enum ReplayPolicy : byte { Authoritative = 1, ObserveOnly = 2 }

    public static readonly IReadOnlyList<Type> AllTypes = new[]
    {
        typeof(CancelWindowEnteredEvent),
        typeof(CancelWindowExitedEvent),
        typeof(CharacterSelectedEvent),
        typeof(ChargeStateChangedEvent),
        typeof(ComboEndedEvent),
        typeof(ComboStartedEvent),
        typeof(FrameAdvancedEvent),
        typeof(FrameRewoundEvent),
        typeof(HitConnectedEvent),
        typeof(InputBufferExpiredEvent),
        typeof(InputReceivedEvent),
        typeof(KnockbackAppliedEvent),
        typeof(MatchInitializedEvent),
        typeof(MoveBlockedEvent),
        typeof(MoveCanceledEvent),
        typeof(MoveFrameChangedEvent),
        typeof(MoveStartedEvent),
        typeof(StateChangedEvent),
        typeof(StateStackChangedEvent),
        typeof(SceneChangingEvent),
        typeof(SceneChangedEvent),
        typeof(ReplayEndedEvent),
        typeof(ReplayPausedEvent),
        typeof(ReplayStartedEvent),
    };

    private static readonly Dictionary<string, Type> _nameToType = new();
    private static readonly Dictionary<Type, string> _typeToName = new();

    static EventTypeRegistry()
    {
        foreach (var type in AllTypes)
        {
            _nameToType[type.Name] = type;
            _typeToName[type] = type.Name;
        }
    }

    public static Type? Resolve(string eventTypeName) =>
        _nameToType.TryGetValue(eventTypeName, out var type) ? type : null;

    public static bool IsRegistered(Type type) =>
        type is not null && _typeToName.ContainsKey(type);

    public static string GetName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _typeToName.TryGetValue(type, out var name) ? name : type.Name;
    }

    public static int GetPhase(Type type) => type.Name switch
    {
        nameof(FrameAdvancedEvent) or nameof(FrameRewoundEvent) => 1,
        nameof(InputReceivedEvent) or nameof(InputBufferExpiredEvent) or nameof(ChargeStateChangedEvent) => 2,
        nameof(MoveStartedEvent) or nameof(MoveFrameChangedEvent) or nameof(CancelWindowEnteredEvent) or nameof(CancelWindowExitedEvent) => 3,
        nameof(HitConnectedEvent) or nameof(MoveBlockedEvent) or nameof(KnockbackAppliedEvent) => 4,
        nameof(StateChangedEvent) or nameof(StateStackChangedEvent) => 5,
        nameof(ComboStartedEvent) or nameof(MoveCanceledEvent) or nameof(ComboEndedEvent) => 6,
        _ => 7
    };

    public static ReplayPolicy GetPolicy(Type type) => type == typeof(StateChangedEvent)
        || type == typeof(StateStackChangedEvent)
        || type == typeof(CancelWindowEnteredEvent)
        || type == typeof(CancelWindowExitedEvent)
        ? ReplayPolicy.ObserveOnly
        : ReplayPolicy.Authoritative;
}
