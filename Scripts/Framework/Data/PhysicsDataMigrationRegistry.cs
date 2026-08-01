#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FTG_Framework.Data;

internal sealed class PhysicsDataMigrationRegistry
{
    private readonly int _currentVersion;
    private readonly Dictionary<int, Func<string, string>> _steps = new();

    public PhysicsDataMigrationRegistry(int currentVersion)
    {
        if (currentVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(currentVersion));
        _currentVersion = currentVersion;
    }

    public void Register(int sourceVersion, Func<string, string> migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        if (sourceVersion < 0 || sourceVersion >= _currentVersion || !_steps.TryAdd(sourceVersion, migration))
            throw new InvalidOperationException($"[Data] Invalid or duplicate migration source version {sourceVersion}.");
    }

    public void MigrateFile(string path, int declaredSourceVersion)
    {
        string original = File.ReadAllText(path);
        int actual = ReadSchemaVersion(original);
        if (actual != declaredSourceVersion)
            throw new FormatException($"[Data] Declared source version {declaredSourceVersion} does not match document version {actual}.");
        if (actual >= _currentVersion)
            throw new FormatException($"[Data] Schema version {actual} is not an older supported source.");

        string migrated = original;
        for (int version = actual; version < _currentVersion; version++)
        {
            if (!_steps.TryGetValue(version, out var step))
                throw new FormatException($"[Data] Missing migration step {version} -> {version + 1}.");
            migrated = step(migrated);
            if (ReadSchemaVersion(migrated) != version + 1)
                throw new FormatException($"[Data] Migration step {version} did not produce schema_version {version + 1}.");
        }

        PhysicsDataPersistence.WriteAtomically(path, migrated);
    }

    private static int ReadSchemaVersion(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schema_version", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int version))
                throw new FormatException("[Data] Required integer schema_version is missing.");
            return version;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Failed to parse migration document: {ex.Message}", ex);
        }
    }
}
