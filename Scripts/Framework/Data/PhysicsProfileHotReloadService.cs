#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Data;

internal sealed class PhysicsProfileHotReloadService : IModule
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _knockbackPath;
    private readonly string _responsePath;
    private readonly Func<IReadOnlyCollection<string>> _requiredResponseProfileIds;
    private IDataStore? _dataStore;
    private bool _initialized;

    public PhysicsProfileHotReloadService(
        string knockbackPath,
        string responsePath,
        Func<IReadOnlyCollection<string>> requiredResponseProfileIds)
    {
        ArgumentNullException.ThrowIfNull(knockbackPath);
        ArgumentNullException.ThrowIfNull(responsePath);
        ArgumentNullException.ThrowIfNull(requiredResponseProfileIds);
        _knockbackPath = Path.GetFullPath(knockbackPath);
        _responsePath = Path.GetFullPath(responsePath);
        _requiredResponseProfileIds = requiredResponseProfileIds;
    }

    public void Initialize(IDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        if (_initialized)
            return;
        _dataStore = dataStore;
        EventBus.Instance.Subscribe<DataReloadedEvent>(OnDataReloaded);
        _initialized = true;
    }

    public void Shutdown()
    {
        if (!_initialized)
            return;
        EventBus.Instance.Unsubscribe<DataReloadedEvent>(OnDataReloaded);
        _dataStore = null;
        _initialized = false;
    }

    private void OnDataReloaded(DataReloadedEvent evt)
    {
        if (_dataStore is null)
            return;

        string path;
        try
        {
            path = Path.GetFullPath(evt.FilePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            FrameworkLog.Error?.Invoke($"[Data] Cannot resolve reload path '{evt.FilePath}': {ex.Message}");
            return;
        }

        bool knockback = string.Equals(path, _knockbackPath, PathComparison);
        bool response = string.Equals(path, _responsePath, PathComparison);
        if (!knockback && !response)
            return;

        try
        {
            ulong expectedVersion = _dataStore is DataStore concrete
                ? concrete.PhysicsDatasetVersion
                : 0;
            string json = File.ReadAllText(path);
            if (knockback)
            {
                var candidates = PhysicsDataLoader.LoadKnockbackProfilesFromJson(json);
                PhysicsProfileReferenceValidator.ValidateKnockbackProfiles(_dataStore, candidates);
                if (_dataStore is DataStore store)
                {
                    if (!store.TryCommitKnockbackProfiles(candidates, expectedVersion))
                        throw new FormatException("[Data] Stale knockback-profile reload candidate.");
                }
                else
                    _dataStore.SetKnockbackProfiles(candidates);
            }
            else
            {
                var candidates = PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(json);
                PhysicsProfileReferenceValidator.ValidateResponseProfiles(
                    candidates, _requiredResponseProfileIds());
                if (_dataStore is DataStore store)
                {
                    if (!store.TryCommitPhysicsResponseProfiles(candidates, expectedVersion))
                        throw new FormatException("[Data] Stale physics-response reload candidate.");
                }
                else
                    _dataStore.SetPhysicsResponseProfiles(candidates);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            FrameworkLog.Info?.Invoke(
                $"[Data] Warning: watched profile file was removed: {path}. Retaining last known valid data.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or FormatException)
        {
            FrameworkLog.Error?.Invoke(
                $"[Data] Failed to reload physics profiles from '{path}': {ex.Message} Retaining last known valid data.");
        }
    }
}
