#nullable enable
using Xunit;

namespace FTG_Framework.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EventBusTestCollection
{
    public const string Name = "EventBus singleton";
}
