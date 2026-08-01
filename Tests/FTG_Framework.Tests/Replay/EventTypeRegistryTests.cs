#nullable enable
using System;
using System.Linq;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class EventTypeRegistryTests
{
    [Fact]
    public void AllTypes_HasUniqueStablePolicyAndPhaseForEveryType()
    {
        Assert.NotEmpty(EventTypeRegistry.AllTypes);
        Assert.Equal(EventTypeRegistry.AllTypes.Count, EventTypeRegistry.AllTypes.Distinct().Count());
        foreach (var type in EventTypeRegistry.AllTypes)
        {
            Assert.InRange(EventTypeRegistry.GetPhase(type), 1, 7);
            Assert.True(Enum.IsDefined(EventTypeRegistry.GetPolicy(type)));
        }
    }

    [Fact]
    public void Resolve_AllKnownTypes_ByName()
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
    public void GetName_ReturnsCorrectName()
    {
        var type = typeof(FTG_Framework.Core.Events.HitConnectedEvent);
        Assert.Equal("HitConnectedEvent", EventTypeRegistry.GetName(type));
    }

    [Fact]
    public void GetName_UnregisteredType_FallsBackToTypeName()
    {
        var type = typeof(string);
        Assert.Equal("String", EventTypeRegistry.GetName(type));
    }

    [Fact]
    public void Catalog_IsExhaustiveOverRecordableEventBusTypes()
    {
        Type[] nonRecordable =
        [
            typeof(FTG_Framework.Core.Events.DataReloadedEvent),
            typeof(FTG_Framework.Core.Events.StateRestoredEvent)
        ];
        Type[] expected = FTG_Framework.Core.EventBus.Instance.GetKnownEventTypes()
            .Except(nonRecordable).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        Type[] actual = EventTypeRegistry.AllTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }
}
