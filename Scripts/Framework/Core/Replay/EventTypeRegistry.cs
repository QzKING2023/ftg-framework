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

    public static string GetName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _typeToName.TryGetValue(type, out var name) ? name : type.Name;
    }
}
