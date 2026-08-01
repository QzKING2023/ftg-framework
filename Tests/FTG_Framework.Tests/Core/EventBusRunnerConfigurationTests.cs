#nullable enable
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace FTG_Framework.Tests.Core;

public sealed class EventBusRunnerConfigurationTests
{
    [Fact]
    public void Runner_EnablesCollectionParallelism()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");
        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(path));

        Assert.True(config.RootElement.GetProperty("diagnosticMessages").GetBoolean());
        Assert.True(config.RootElement.GetProperty("parallelizeTestCollections").GetBoolean());
        Assert.Equal(0, config.RootElement.GetProperty("maxParallelThreads").GetInt32());
    }

    [Fact]
    public void EventBusCollection_IsNonParallel_AndPureTestsRemainUnassigned()
    {
        var definition = typeof(EventBusTestCollection).GetCustomAttribute<CollectionDefinitionAttribute>();
        Assert.NotNull(definition);
        Assert.True(definition!.DisableParallelization);
        Assert.Null(typeof(SmokeTest).GetCustomAttribute<CollectionAttribute>());
    }

    [Fact]
    public void MaintainedEventBusInventory_IsAssignedToSingletonCollection()
    {
        string[] inventory = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "eventbus-test-inventory.txt"));
        Assembly assembly = typeof(EventBusRunnerConfigurationTests).Assembly;

        foreach (string typeName in inventory)
        {
            Type? candidate = assembly.GetType(typeName);
            Assert.True(candidate is not null, $"Inventory type not found: {typeName}");
            Type type = candidate!;
            var collection = type.GetCustomAttribute<CollectionAttribute>();
            Assert.NotNull(collection);
            CustomAttributeData data = Assert.Single(type.CustomAttributes, item => item.AttributeType == typeof(CollectionAttribute));
            Assert.Equal(EventBusTestCollection.Name, data.ConstructorArguments[0].Value);
            if (type != typeof(Replay.ReplayRecorderTests) && type != typeof(EventBusTestIsolationTests))
                Assert.True(type.GetField("_eventBusScope", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                    $"Per-test EventBus scope missing: {typeName}");
        }

        string[] assigned = assembly.GetTypes()
            .Where(type => type.CustomAttributes.Any(attribute =>
                attribute.AttributeType == typeof(CollectionAttribute)
                && Equals(attribute.ConstructorArguments[0].Value, EventBusTestCollection.Name)))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(inventory.OrderBy(name => name, StringComparer.Ordinal), assigned);
    }
}
