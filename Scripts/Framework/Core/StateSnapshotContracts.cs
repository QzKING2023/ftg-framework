#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FTG_Framework.Core;

public enum SnapshotRestoreMode : byte
{
    Normal = 1,
    ReplayBootstrap = 2,
    ReplayHandoff = 3
}

public enum SnapshotFaultPoint : byte
{
    ReserveEpoch = 1,
    DecodeComponent = 2,
    PrepareParticipant = 3,
    ValidateGraph = 4
}

public static class SnapshotParticipantCatalog
{
    public const string StateMachine = "state_machine";
    public const string FrameData = "frame_data";
    public const string PhysicsMotion = "physics_motion";
    public const string Input = "input";
    public const string Recording = "recording";
    public const string Combo = "combo";
    public const string TrainingInput = "training_input";

    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(new[]
    {
        FrameData, Input, PhysicsMotion, Recording, StateMachine
    });

    public static void ValidateRequired(IEnumerable<IStateSnapshotParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var actual = participants.Select(participant => participant.Discriminator)
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = Required.Where(id => !actual.Contains(id)).ToArray();
        if (missing.Length != 0)
            throw new ArgumentException($"[Snapshot] Required participants missing: {string.Join(", ", missing)}.", nameof(participants));
    }
}

public static class SnapshotFaultCatalog
{
    public static IReadOnlyDictionary<SnapshotFaultPoint, string> Locations { get; } =
        new Dictionary<SnapshotFaultPoint, string>
        {
            [SnapshotFaultPoint.ReserveEpoch] = "Before lifecycle epoch reservation",
            [SnapshotFaultPoint.DecodeComponent] = "Before component lookup/version decode",
            [SnapshotFaultPoint.PrepareParticipant] = "Before participant Prepare",
            [SnapshotFaultPoint.ValidateGraph] = "Before cross-component graph validation"
        };
}

public sealed record SnapshotComponent(string Discriminator, int CodecVersion, string Payload);

public sealed class StateSnapshot
{
    private readonly SnapshotComponent[] _components;

    public StateSnapshot(int schemaVersion, string frameworkVersion, ulong sourceEpoch, int frame,
        IReadOnlyList<SnapshotComponent> components)
    {
        if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(frameworkVersion);
        ArgumentNullException.ThrowIfNull(components);
        SchemaVersion = schemaVersion;
        FrameworkVersion = frameworkVersion;
        SourceEpoch = sourceEpoch;
        if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
        Frame = frame;
        _components = components.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SnapshotComponent component in _components)
        {
            if (string.IsNullOrWhiteSpace(component.Discriminator) || !ids.Add(component.Discriminator))
                throw new ArgumentException("[Snapshot] Component discriminators must be non-empty and unique.", nameof(components));
            if (component.CodecVersion < 1 || component.Payload is null)
                throw new ArgumentException($"[Snapshot] Invalid component '{component.Discriminator}'.", nameof(components));
        }
    }

    public int SchemaVersion { get; }
    public string FrameworkVersion { get; }
    public ulong SourceEpoch { get; }
    public int Frame { get; }
    public IReadOnlyList<SnapshotComponent> Components => Array.AsReadOnly(_components);
}

public readonly record struct SnapshotPrepareContext(
    ulong SourceEpoch,
    ulong ReservedEpoch,
    int Frame,
    SnapshotRestoreMode Mode);

public interface IStateSnapshotParticipant
{
    string Discriminator { get; }
    int CodecVersion { get; }
    SnapshotComponent Capture(int frame, ulong epoch);
    IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context);
}

public interface IPreparedSnapshotComponent
{
    string Discriminator { get; }
}

internal interface IAtomicPreparedSnapshotComponent : IPreparedSnapshotComponent
{
    void ApplyReferenceSwap();
}

public static class PreparedSnapshotComponent
{
    public static IPreparedSnapshotComponent Create<TState>(
        string discriminator, SnapshotReference<TState> slot, TState replacement) where TState : class =>
        new AtomicPreparedSnapshotComponent<TState>(discriminator, slot, replacement);

    internal static IPreparedSnapshotComponent CreateOwnerSwap<TState>(
        string discriminator, TState replacement, Action<TState> install) where TState : class =>
        new AtomicOwnerPreparedSnapshotComponent<TState>(discriminator, replacement, install);

    private sealed class AtomicPreparedSnapshotComponent<TState>(
        string discriminator, SnapshotReference<TState> slot, TState replacement)
        : IAtomicPreparedSnapshotComponent where TState : class
    {
        public string Discriminator { get; } = discriminator;
        public void ApplyReferenceSwap() => slot.Value = replacement;
    }

    private sealed class AtomicOwnerPreparedSnapshotComponent<TState>(
        string discriminator, TState replacement, Action<TState> install)
        : IAtomicPreparedSnapshotComponent where TState : class
    {
        public string Discriminator { get; } = discriminator;
        public void ApplyReferenceSwap() => install(replacement);
    }
}

public sealed class SnapshotPrepareException : Exception
{
    public SnapshotPrepareException(string faultPoint, string message, Exception? inner = null)
        : base($"[Snapshot] Prepare failed at '{faultPoint}': {message}", inner) => FaultPoint = faultPoint;

    public string FaultPoint { get; }
}
