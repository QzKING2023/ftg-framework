#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Input;

internal sealed class InputHistory : IModule, IInputHistory
{
    private readonly CircularBuffer<InputEntry>[] _directionalTracks;
    private readonly CircularBuffer<InputEntry>[] _buttonTracks;
    private readonly int _capacity;

    public InputHistory(int capacity = 600)
    {
        if (capacity <= 0)
            throw new ArgumentException($"[Input] Capacity must be > 0, got {capacity}");
        _capacity = capacity;
        _directionalTracks = new CircularBuffer<InputEntry>[2];
        _buttonTracks = new CircularBuffer<InputEntry>[2];
        for (int i = 0; i < 2; i++)
        {
            _directionalTracks[i] = new CircularBuffer<InputEntry>(capacity);
            _buttonTracks[i] = new CircularBuffer<InputEntry>(capacity);
        }
        EventBus.Instance.Subscribe<FrameRewoundEvent>(_OnFrameRewound);
    }

    public int Capacity => _capacity;

    public void Initialize(IDataStore dataStore)
    {
        GD.Print($"[Input] InputHistory initialized — capacity: {_capacity} frames per track.");
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<FrameRewoundEvent>(_OnFrameRewound);
    }

    // After a rewind, inputs recorded on the abandoned future branch must not keep
    // driving InputBuffer/leniency matching.
    private void _OnFrameRewound(FrameRewoundEvent e)
    {
        for (int i = 0; i < 2; i++)
        {
            _directionalTracks[i].RemoveTailWhile(entry => entry.Frame > e.FrameNumber);
            _buttonTracks[i].RemoveTailWhile(entry => entry.Frame > e.FrameNumber);
        }
    }

    public void RecordInput(int playerId, InputType type, int value)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return;
        }

        int index = playerId - 1;
        int frame = EventBus.Instance.CurrentFrame;
        var entry = new InputEntry(frame, type, value);

        var track = type == InputType.Directional
            ? _directionalTracks[index]
            : _buttonTracks[index];
        track.Add(entry);

        EventBus.Instance.Publish(new InputReceivedEvent(playerId, frame, (int)type, value));
    }

    public IReadOnlyList<InputEntry> GetDirectionalHistory(int playerId)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<InputEntry>();
        }
        return _directionalTracks[playerId - 1].Snapshot();
    }

    public IReadOnlyList<InputEntry> GetButtonHistory(int playerId)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<InputEntry>();
        }
        return _buttonTracks[playerId - 1].Snapshot();
    }

    public IReadOnlyList<InputEntry> GetHistory(int playerId, InputType type)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<InputEntry>();
        }
        return type == InputType.Directional
            ? _directionalTracks[playerId - 1].Snapshot()
            : _buttonTracks[playerId - 1].Snapshot();
    }

    internal InputRuntimeSnapshot CaptureRuntimeSnapshot(ChargeTracker chargeTracker)
    {
        ArgumentNullException.ThrowIfNull(chargeTracker);
        return chargeTracker.CaptureRuntimeSnapshot(
            _directionalTracks[0].Snapshot(), _buttonTracks[0].Snapshot(),
            _directionalTracks[1].Snapshot(), _buttonTracks[1].Snapshot());
    }

    internal InputRuntimeSnapshot PrepareRuntimeSnapshot(InputRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.P1Directions is null || snapshot.P1Buttons is null ||
            snapshot.P2Directions is null || snapshot.P2Buttons is null ||
            snapshot.ChargeStarts is null || snapshot.ChargeEnds is null || snapshot.WasCharging is null)
            throw new SnapshotPrepareException(SnapshotParticipantCatalog.Input, "Required input state is null.");
        if (snapshot.ChargeStarts.Length != 4 || snapshot.ChargeEnds.Length != 4 || snapshot.WasCharging.Length != 4)
            throw new SnapshotPrepareException(SnapshotParticipantCatalog.Input, "Charge state shape is invalid.");
        return new InputRuntimeSnapshot(
            (InputEntry[])snapshot.P1Directions.Clone(), (InputEntry[])snapshot.P1Buttons.Clone(),
            (InputEntry[])snapshot.P2Directions.Clone(), (InputEntry[])snapshot.P2Buttons.Clone(),
            (int[])snapshot.ChargeStarts.Clone(), (int[])snapshot.ChargeEnds.Clone(),
            (bool[])snapshot.WasCharging.Clone(), snapshot.LastUpdateFrame);
    }

    internal void InstallRuntimeSnapshot(InputRuntimeSnapshot snapshot, ChargeTracker chargeTracker)
    {
        _directionalTracks[0].Replace(snapshot.P1Directions);
        _buttonTracks[0].Replace(snapshot.P1Buttons);
        _directionalTracks[1].Replace(snapshot.P2Directions);
        _buttonTracks[1].Replace(snapshot.P2Buttons);
        chargeTracker.InstallRuntimeSnapshot(snapshot);
    }
}
