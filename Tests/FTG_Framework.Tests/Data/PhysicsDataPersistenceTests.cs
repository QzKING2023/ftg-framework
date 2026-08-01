#nullable enable
using System;
using System.IO;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class PhysicsDataPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-data-tx-{Guid.NewGuid():N}");

    public PhysicsDataPersistenceTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AtomicWrite_PreCommitFailure_PreservesPriorBytes()
    {
        string path = Path.Combine(_directory, "profiles.json");
        File.WriteAllText(path, "old");

        Assert.Throws<IOException>(() =>
            PhysicsDataPersistence.WriteAtomically(path, "new", () => throw new IOException("injected")));

        Assert.Equal("old", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Migration_RequiresCompleteRegisteredPathAndPersistsCurrentSchema()
    {
        string path = Path.Combine(_directory, "profiles.json");
        File.WriteAllText(path, "{\"schema_version\":0,\"value\":1}");
        var migrations = new PhysicsDataMigrationRegistry(currentVersion: 1);
        Assert.Throws<FormatException>(() => migrations.MigrateFile(path, declaredSourceVersion: 0));

        migrations.Register(0, json => json.Replace("\"schema_version\":0", "\"schema_version\":1", StringComparison.Ordinal));
        migrations.MigrateFile(path, declaredSourceVersion: 0);

        Assert.Contains("\"schema_version\":1", File.ReadAllText(path), StringComparison.Ordinal);
    }
}
