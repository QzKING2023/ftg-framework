#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Core;

public sealed class StateSnapshotCoordinator
{
    public const int CurrentSchemaVersion = 1;
    private readonly EventBus _eventBus;
    private readonly IStateSnapshotParticipant[] _participants;
    private readonly Action<SnapshotFaultPoint, string>? _faultInjector;
    private readonly Action<IReadOnlyDictionary<string, SnapshotComponent>, SnapshotPrepareContext>? _graphValidator;
    private readonly string _frameworkVersion;

    public StateSnapshotCoordinator(
        EventBus eventBus,
        IEnumerable<IStateSnapshotParticipant> participants,
        Action<SnapshotFaultPoint, string>? faultInjector = null,
        Action<IReadOnlyDictionary<string, SnapshotComponent>, SnapshotPrepareContext>? graphValidator = null,
        string frameworkVersion = "2.3.0")
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(participants);
        _eventBus = eventBus;
        _participants = participants.OrderBy(p => p.Discriminator, StringComparer.Ordinal).ToArray();
        _faultInjector = faultInjector;
        _graphValidator = graphValidator;
        _frameworkVersion = string.IsNullOrWhiteSpace(frameworkVersion)
            ? throw new ArgumentException("Framework version is required.", nameof(frameworkVersion))
            : frameworkVersion;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in _participants)
        {
            if (string.IsNullOrWhiteSpace(participant.Discriminator) || !ids.Add(participant.Discriminator))
                throw new ArgumentException("[Snapshot] Participant discriminators must be non-empty and unique.", nameof(participants));
            if (participant.CodecVersion < 1)
                throw new ArgumentException("[Snapshot] Participant codec versions must be positive.", nameof(participants));
        }
    }

    public IReadOnlyList<string> ParticipantOrder =>
        Array.AsReadOnly(_participants.Select(participant => participant.Discriminator).ToArray());

    public static StateSnapshotCoordinator CreateRuntime(
        EventBus eventBus,
        IEnumerable<IStateSnapshotParticipant> participants,
        Action<SnapshotFaultPoint, string>? faultInjector = null,
        Action<IReadOnlyDictionary<string, SnapshotComponent>, SnapshotPrepareContext>? graphValidator = null)
    {
        IStateSnapshotParticipant[] materialized = participants?.ToArray()
            ?? throw new ArgumentNullException(nameof(participants));
        SnapshotParticipantCatalog.ValidateRequired(materialized);
        return new StateSnapshotCoordinator(eventBus, materialized, faultInjector, graphValidator);
    }

    public StateSnapshot Capture(int frame, string frameworkVersion)
    {
        _eventBus.BeginSnapshotQuiescence();
        try
        {
            SnapshotComponent[] components = _participants
                .Select(participant => participant.Capture(frame, _eventBus.LifecycleEpoch))
                .ToArray();
            return new StateSnapshot(CurrentSchemaVersion, frameworkVersion,
                _eventBus.LifecycleEpoch, frame, components);
        }
        finally { _eventBus.EndSnapshotQuiescence(discardQuarantined: false); }
    }

    public void Restore(StateSnapshot snapshot, SnapshotRestoreMode mode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != CurrentSchemaVersion)
            throw new SnapshotPrepareException("container.schema_version", $"Unsupported version {snapshot.SchemaVersion}.");
        if (!string.Equals(snapshot.FrameworkVersion, _frameworkVersion, StringComparison.Ordinal))
            throw new SnapshotPrepareException("container.framework_version",
                $"Snapshot framework '{snapshot.FrameworkVersion}' is incompatible with '{_frameworkVersion}'.");

        _eventBus.BeginSnapshotQuiescence();
        bool committed = false;
        try
        {
            _faultInjector?.Invoke(SnapshotFaultPoint.ReserveEpoch, "event_bus");
            ulong reservedEpoch = _eventBus.ReserveLifecycleEpoch();
            var byId = snapshot.Components.ToDictionary(c => c.Discriminator, StringComparer.Ordinal);
            var participantByDiscriminator = _participants.ToDictionary(
                participant => participant.Discriminator, StringComparer.Ordinal);
            string[] unknown = byId.Keys.Where(id => !participantByDiscriminator.ContainsKey(id)).ToArray();
            if (unknown.Length != 0)
                throw new SnapshotPrepareException("component_catalog",
                    $"Snapshot contains unknown components: {string.Join(", ", unknown)}.");
            var prepared = new List<IPreparedSnapshotComponent>(_participants.Length);
            foreach (IStateSnapshotParticipant participant in _participants)
            {
                _faultInjector?.Invoke(SnapshotFaultPoint.DecodeComponent, participant.Discriminator);
                if (!byId.TryGetValue(participant.Discriminator, out SnapshotComponent? component))
                {
                    // Optional participants (combo, training_input) may be absent
                    // in snapshots recorded by older coordinators, e.g. replay
                    // files from before their introduction.
                    if (SnapshotParticipantCatalog.Required.Contains(participant.Discriminator))
                        throw new SnapshotPrepareException(participant.Discriminator, "Required component is missing.");
                    continue;
                }
                if (component.CodecVersion != participant.CodecVersion)
                    throw new SnapshotPrepareException(participant.Discriminator,
                        $"Unsupported codec version {component.CodecVersion}.");
                _faultInjector?.Invoke(SnapshotFaultPoint.PrepareParticipant, participant.Discriminator);
                prepared.Add(participant.Prepare(component,
                    new SnapshotPrepareContext(snapshot.SourceEpoch, reservedEpoch, snapshot.Frame, mode)));
            }
            _faultInjector?.Invoke(SnapshotFaultPoint.ValidateGraph, "cross_component_graph");
            var prepareContext = new SnapshotPrepareContext(snapshot.SourceEpoch, reservedEpoch, snapshot.Frame, mode);
            _graphValidator?.Invoke(byId, prepareContext);

            IAtomicPreparedSnapshotComponent[] atomic = prepared
                .Select(replacement => replacement as IAtomicPreparedSnapshotComponent
                    ?? throw new SnapshotPrepareException(replacement.Discriminator,
                        "Participant did not return a Core-owned atomic prepared replacement."))
                .ToArray();
            int nextFrame = _eventBus.PrepareRestoreCommit(reservedEpoch, snapshot.Frame);

            foreach (IAtomicPreparedSnapshotComponent replacement in atomic)
                replacement.ApplyReferenceSwap();
            _eventBus.CommitReservedEpoch(reservedEpoch, nextFrame);
            if (mode == SnapshotRestoreMode.Normal)
                _eventBus.PublishStateRestored(new StateRestoredEvent(snapshot.Frame, snapshot.SourceEpoch));
            committed = true;
        }
        catch (SnapshotPrepareException)
        {
            _eventBus.CancelReservedEpoch();
            throw;
        }
        catch (Exception ex)
        {
            _eventBus.CancelReservedEpoch();
            throw new SnapshotPrepareException("participant", ex.Message, ex);
        }
        finally { _eventBus.EndSnapshotQuiescence(discardQuarantined: committed); }
    }
}
