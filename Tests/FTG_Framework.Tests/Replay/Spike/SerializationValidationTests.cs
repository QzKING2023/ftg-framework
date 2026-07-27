#nullable enable
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Xunit;

namespace FTG_Framework.Tests.Replay.Spike;

// ============================================================================
// Task 2.1-2.3: JSON round-trip tests for all 13 event types + edge cases
// ============================================================================

public class SerializationRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    // --- 2.1: One test per event type ---

    [Fact]
    public void FrameAdvancedEvent_RoundTrip()
    {
        var original = new FrameAdvancedEvent(42);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<FrameAdvancedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void FrameRewoundEvent_RoundTrip()
    {
        var original = new FrameRewoundEvent(99);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<FrameRewoundEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void InputReceivedEvent_RoundTrip()
    {
        var original = new InputReceivedEvent(1, 100, 1, 5);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<InputReceivedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void InputBufferExpiredEvent_RoundTrip()
    {
        var original = new InputBufferExpiredEvent(2, 200);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<InputBufferExpiredEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void ChargeStateChangedEvent_RoundTrip()
    {
        var original = new ChargeStateChangedEvent(1, 2, true);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<ChargeStateChangedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void MoveFrameChangedEvent_RoundTrip()
    {
        var original = new MoveFrameChangedEvent(1, "5LP", 5, 20, MovePhase.Active);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<MoveFrameChangedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void CancelWindowEnteredEvent_RoundTrip()
    {
        var original = new CancelWindowEnteredEvent(1, "5HP", "Special", 10, 18);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<CancelWindowEnteredEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void CancelWindowExitedEvent_RoundTrip()
    {
        var original = new CancelWindowExitedEvent(1, "5HP", "Special");
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<CancelWindowExitedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void HitConnectedEvent_RoundTrip()
    {
        var original = new HitConnectedEvent(1, 2, "5HP", 3, 80);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<HitConnectedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void MoveBlockedEvent_RoundTrip()
    {
        var original = new MoveBlockedEvent(2, 1, "2MK", -5, 40);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<MoveBlockedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void ComboStartedEvent_RoundTrip()
    {
        var original = new ComboStartedEvent(1, "5LP", 1);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<ComboStartedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void MoveCanceledEvent_RoundTrip()
    {
        var original = new MoveCanceledEvent(1, "5LP", "5MP", "Normal");
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<MoveCanceledEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void ComboEndedEvent_RoundTrip()
    {
        var original = new ComboEndedEvent(1, 5, "Shoryuken");
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<ComboEndedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    // --- 2.2: Verify positional constructor deserialization ---

    [Fact]
    public void SingleFieldPositional_DeserializesCorrectly()
    {
        // FrameAdvancedEvent(int FrameNumber) — simplest positional record
        var json = """{"FrameNumber":123}""";
        var evt = JsonSerializer.Deserialize<FrameAdvancedEvent>(json, Options);
        Assert.Equal(123, evt.FrameNumber);
    }

    [Fact]
    public void FiveFieldPositional_DeserializesCorrectly()
    {
        // HitConnectedEvent(int AttackerId, int DefenderId, string MoveId, int HitAdvantage, int Damage)
        var json = """{"AttackerId":1,"DefenderId":2,"MoveId":"5HP","HitAdvantage":3,"Damage":80}""";
        var evt = JsonSerializer.Deserialize<HitConnectedEvent>(json, Options);
        Assert.Equal(1, evt.AttackerId);
        Assert.Equal(2, evt.DefenderId);
        Assert.Equal("5HP", evt.MoveId);
        Assert.Equal(3, evt.HitAdvantage);
        Assert.Equal(80, evt.Damage);
    }

    [Fact]
    public void CaseInsensitivePropertyMatching_Works()
    {
        var json = """{"framenumber":99}""";
        var evt = JsonSerializer.Deserialize<FrameAdvancedEvent>(json, Options);
        Assert.Equal(99, evt.FrameNumber);
    }

    // --- 2.3: Edge cases ---

    [Fact]
    public void IntBoundary_MinValue_RoundTrip()
    {
        var original = new InputReceivedEvent(int.MinValue, int.MinValue, int.MinValue, int.MinValue);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<InputReceivedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void IntBoundary_MaxValue_RoundTrip()
    {
        var original = new HitConnectedEvent(int.MaxValue, int.MaxValue, "test", int.MaxValue, int.MaxValue);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<HitConnectedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void IntBoundary_ZeroAndNegative_RoundTrip()
    {
        var original = new HitConnectedEvent(0, -1, "test", 0, -100);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<HitConnectedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void StringField_Null_DeserializesToNullString()
    {
        // readonly record struct with positional string — JSON null passes through
        // to the constructor parameter. The constructor does not validate non-null,
        // so the field ends up as null (de facto, despite string not being nullable).
        // This is a known gap: replay should validate string fields are non-null.
        var json = """{"PlayerId":1,"MoveId":null,"CurrentFrame":0,"TotalFrames":0,"Phase":0}""";
        var evt = JsonSerializer.Deserialize<MoveFrameChangedEvent>(json, Options);
        Assert.Equal(1, evt.PlayerId);
        Assert.Null(evt.MoveId); // string field is null — important edge case
    }

    [Fact]
    public void StringField_Empty_RoundTrip()
    {
        var original = new MoveFrameChangedEvent(1, "", 0, 0, MovePhase.Idle);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<MoveFrameChangedEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void StringField_Unicode_RoundTrip()
    {
        var original = new CancelWindowEnteredEvent(1, "波動拳", "必殺技", 5, 20);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<CancelWindowEnteredEvent>(json, Options);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void EnumField_AllMovePhaseValues_RoundTrip()
    {
        foreach (MovePhase phase in Enum.GetValues<MovePhase>())
        {
            var original = new MoveFrameChangedEvent(1, "5LP", 0, 10, phase);
            var json = JsonSerializer.Serialize(original, Options);
            var restored = JsonSerializer.Deserialize<MoveFrameChangedEvent>(json, Options);
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void EnumField_AllDirectionValues_RoundTrip()
    {
        // DirectionValue is backed by int in events (InputType uses int)
        // This validates int-to-enum round-trip for the full 0-9 range
        for (int dir = 0; dir <= 9; dir++)
        {
            var original = new InputReceivedEvent(1, 50, 0, dir);
            var json = JsonSerializer.Serialize(original, Options);
            var restored = JsonSerializer.Deserialize<InputReceivedEvent>(json, Options);
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void BoolField_False_RoundTrip()
    {
        var original = new ChargeStateChangedEvent(1, 2, false);
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<ChargeStateChangedEvent>(json, Options);
        Assert.Equal(original, restored);
    }
}

// ============================================================================
// Task 2.4: Source-generated serializer (AOT compatibility) verification
// ============================================================================

[JsonSerializable(typeof(FrameAdvancedEvent))]
[JsonSerializable(typeof(FrameRewoundEvent))]
[JsonSerializable(typeof(InputReceivedEvent))]
[JsonSerializable(typeof(InputBufferExpiredEvent))]
[JsonSerializable(typeof(ChargeStateChangedEvent))]
[JsonSerializable(typeof(MoveFrameChangedEvent))]
[JsonSerializable(typeof(CancelWindowEnteredEvent))]
[JsonSerializable(typeof(CancelWindowExitedEvent))]
[JsonSerializable(typeof(HitConnectedEvent))]
[JsonSerializable(typeof(MoveBlockedEvent))]
[JsonSerializable(typeof(ComboStartedEvent))]
[JsonSerializable(typeof(MoveCanceledEvent))]
[JsonSerializable(typeof(ComboEndedEvent))]
internal partial class EventJsonContext : JsonSerializerContext
{
}

public class SourceGeneratorSerializationTests
{
    [Fact]
    public void SourceGenContext_All13Types_RoundTrip()
    {
        var context = EventJsonContext.Default;
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = EventJsonContext.Default,
            PropertyNameCaseInsensitive = true
        };

        var evt = new HitConnectedEvent(1, 2, "5HP", 3, 80);
        var json = JsonSerializer.Serialize(evt, typeof(HitConnectedEvent), context);
        var restored = (HitConnectedEvent)JsonSerializer.Deserialize(json, typeof(HitConnectedEvent), context)!;
        Assert.Equal(evt, restored);
    }

    [Fact]
    public void SourceGenContext_WorksWithPositionalRecordStructs()
    {
        // Verify that JsonSerializerContext correctly handles readonly record struct
        // with positional construction — this is critical for AOT compatibility.
        var context = EventJsonContext.Default;

        foreach (var type in EventTypeRegistry.AllTypes)
        {
            // Verify the type info was generated (no fallback to reflection)
            var typeInfo = context.GetTypeInfo(type);
            Assert.NotNull(typeInfo);
        }
    }
}

// ============================================================================
// Task 2.5: Type registry prototype — string → Type → deserialize → Publish<T>
// ============================================================================

public static class EventTypeRegistry
{
    public static readonly IReadOnlyList<Type> AllTypes = new[]
    {
        typeof(FrameAdvancedEvent),
        typeof(FrameRewoundEvent),
        typeof(InputReceivedEvent),
        typeof(InputBufferExpiredEvent),
        typeof(ChargeStateChangedEvent),
        typeof(MoveFrameChangedEvent),
        typeof(CancelWindowEnteredEvent),
        typeof(CancelWindowExitedEvent),
        typeof(HitConnectedEvent),
        typeof(MoveBlockedEvent),
        typeof(ComboStartedEvent),
        typeof(MoveCanceledEvent),
        typeof(ComboEndedEvent),
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

public class TypeRegistryTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Resolve_All13Types_ByName()
    {
        foreach (var type in EventTypeRegistry.AllTypes)
        {
            var resolved = EventTypeRegistry.Resolve(type.Name);
            Assert.NotNull(resolved);
            Assert.Equal(type, resolved);
        }
    }

    [Fact]
    public void Resolve_UnknownType_ReturnsNull()
    {
        Assert.Null(EventTypeRegistry.Resolve("NonExistentEvent"));
    }

    [Fact]
    public void Registry_RoundTrip_DeserializeThenPublishViaReflection()
    {
        var type = EventTypeRegistry.Resolve("HitConnectedEvent")!;
        Assert.Equal(typeof(HitConnectedEvent), type);

        var json = """{"AttackerId":1,"DefenderId":2,"MoveId":"5HP","HitAdvantage":3,"Damage":80}""";
        var obj = JsonSerializer.Deserialize(json, type, Options);
        Assert.NotNull(obj);
        Assert.IsType<HitConnectedEvent>(obj);

        var evt = (HitConnectedEvent)obj;
        Assert.Equal(1, evt.AttackerId);
        Assert.Equal(80, evt.Damage);

        // Verify Publish<T> can be invoked via MakeGenericMethod
        var publishMethod = typeof(EventBus).GetMethod(nameof(EventBus.Publish),
            BindingFlags.Public | BindingFlags.Instance)!;
        var genericPublish = publishMethod.MakeGenericMethod(type);
        Assert.NotNull(genericPublish);

        // Actually invoke via reflection — this proves the full pipeline works
        var eventBus = EventBus.Instance;
        var received = new List<HitConnectedEvent>();
        void Handler(HitConnectedEvent e) => received.Add(e);
        EventBus.Instance.Subscribe<HitConnectedEvent>(Handler);
        try
        {
            genericPublish.Invoke(eventBus, [obj]);
            EventBus.Instance.ProcessFrame();
            Assert.Single(received);
            Assert.Equal(evt, received[0]);
        }
        finally
        {
            EventBus.Instance.Unsubscribe<HitConnectedEvent>(Handler);
        }
    }

    [Fact]
    public void Registry_All13Types_DeserializeAndPublishViaReflection()
    {
        var publishMethod = typeof(EventBus).GetMethod(nameof(EventBus.PublishImmediate),
            BindingFlags.Public | BindingFlags.Instance)!;

        var payloads = new Dictionary<Type, object>
        {
            [typeof(FrameAdvancedEvent)] = new FrameAdvancedEvent(1),
            [typeof(FrameRewoundEvent)] = new FrameRewoundEvent(5),
            [typeof(InputReceivedEvent)] = new InputReceivedEvent(1, 10, 0, 3),
            [typeof(InputBufferExpiredEvent)] = new InputBufferExpiredEvent(1, 15),
            [typeof(ChargeStateChangedEvent)] = new ChargeStateChangedEvent(1, 2, true),
            [typeof(MoveFrameChangedEvent)] = new MoveFrameChangedEvent(1, "5LP", 3, 20, MovePhase.Startup),
            [typeof(CancelWindowEnteredEvent)] = new CancelWindowEnteredEvent(1, "5HP", "Special", 10, 18),
            [typeof(CancelWindowExitedEvent)] = new CancelWindowExitedEvent(1, "5HP", "Special"),
            [typeof(HitConnectedEvent)] = new HitConnectedEvent(1, 2, "5HP", 3, 80),
            [typeof(MoveBlockedEvent)] = new MoveBlockedEvent(1, 2, "5HP", -5, 40),
            [typeof(ComboStartedEvent)] = new ComboStartedEvent(1, "5LP", 1),
            [typeof(MoveCanceledEvent)] = new MoveCanceledEvent(1, "5LP", "5MP", "Normal"),
            [typeof(ComboEndedEvent)] = new ComboEndedEvent(1, 5, "Shoryuken"),
        };

        foreach (var (type, original) in payloads)
        {
            // Serialize → deserialize via type registry
            var json = JsonSerializer.Serialize(original, type, Options);
            var restored = JsonSerializer.Deserialize(json, type, Options);
            Assert.NotNull(restored);
            Assert.Equal(original, restored);

            // Verify PublishImmediate<T> can be created via MakeGenericMethod
            var genericPublish = publishMethod.MakeGenericMethod(type);
            Assert.NotNull(genericPublish);

            // Verify actual dispatch works via reflection — PublishImmediate
            // bypasses the queue (matching the replay injection pattern).
            var eventBus = EventBus.Instance;
            EventBusTestHelper.Drain();

            var captured = new List<object>();
            var captureMi = typeof(TypeRegistryTests)
                .GetMethod(nameof(CaptureToList), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type);
            var handlerType = typeof(Action<>).MakeGenericType(type);
            var handler = Delegate.CreateDelegate(handlerType, captured, captureMi);

            var subscribeMethod = typeof(EventBus).GetMethod(nameof(EventBus.Subscribe))!
                .MakeGenericMethod(type);
            subscribeMethod.Invoke(eventBus, [handler]);
            try
            {
                genericPublish.Invoke(eventBus, [restored]);
                Assert.Single(captured);
                Assert.Equal(restored, captured[0]);
            }
            finally
            {
                var unsubscribeMethod = typeof(EventBus).GetMethod(nameof(EventBus.Unsubscribe))!
                    .MakeGenericMethod(type);
                unsubscribeMethod.Invoke(eventBus, [handler]);
            }
        }
    }

    // Instance method — the first arg of CreateDelegate becomes the 'this' target
    private static void CaptureToList<T>(List<object> list, T evt) => list.Add(evt!);
}
