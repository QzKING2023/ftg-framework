#nullable enable
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class EventTypeRegistryTests
{
    [Fact]
    public void AllTypes_HasAll18EventTypes()
    {
        Assert.Equal(18, EventTypeRegistry.AllTypes.Count);
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
}
