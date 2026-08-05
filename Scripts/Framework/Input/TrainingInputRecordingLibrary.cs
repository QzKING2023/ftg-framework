#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FTG_Framework.Input;

public sealed class TrainingInputRecordingLibrary
{
    private readonly Dictionary<string, TrainingInputRecording> _recordings =
        new(StringComparer.Ordinal);
    private readonly TrainingInputRecording?[] _assigned = new TrainingInputRecording?[3];

    public IReadOnlyDictionary<string, TrainingInputRecording> Recordings =>
        new ReadOnlyDictionary<string, TrainingInputRecording>(_recordings);

    public bool TryAddOrReplace(TrainingInputRecording candidate, bool confirmReplace,
        TrainingInputRecording? activePlayback, out string error,
        Func<bool>? failBeforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            error = "[Input] Recording name is required.";
            return false;
        }
        bool replacing = _recordings.TryGetValue(candidate.Name, out TrainingInputRecording? previous);
        if (replacing && !confirmReplace)
        {
            error = $"[Input] Confirm replacement of recording '{candidate.Name}'.";
            return false;
        }
        if (replacing && ReferenceEquals(previous, activePlayback))
        {
            error = $"[Input] Stop playback of recording '{candidate.Name}' before replacing it.";
            return false;
        }
        if (failBeforeCommit?.Invoke() == true)
        {
            error = "[Input] Recording replacement failed before commit.";
            return false;
        }
        _recordings[candidate.Name] = candidate;
        if (replacing)
        {
            for (int player = 1; player <= 2; player++)
                if (ReferenceEquals(_assigned[player], previous)) _assigned[player] = candidate;
        }
        error = string.Empty;
        return true;
    }

    public bool TryDecodeAndAddOrReplace(ReadOnlySpan<byte> payload, bool confirmReplace,
        TrainingInputRecording? activePlayback, out string error)
    {
        try
        {
            TrainingInputRecording candidate = TrainingInputRecordingCodec.Decode(payload);
            return TryAddOrReplace(candidate, confirmReplace, activePlayback, out error);
        }
        catch (TrainingInputRecordingException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryAssign(int playerId, string name, out string error)
    {
        if (playerId is < 1 or > 2)
        {
            error = $"[Input] Dummy player must be 1 or 2, got {playerId}.";
            return false;
        }
        if (!_recordings.TryGetValue(name, out var recording))
        {
            error = $"[Input] Recording '{name}' was not found.";
            return false;
        }
        _assigned[playerId] = recording;
        error = string.Empty;
        return true;
    }

    public TrainingInputRecording? GetAssigned(int playerId) =>
        playerId is 1 or 2 ? _assigned[playerId] : null;

    /// <summary>Installs a fully validated library replacement during a restore
    /// commit. Assignments are resolved by name and must reference existing
    /// recordings; any mismatch is an internal invariant violation.</summary>
    internal void ReplaceAll(IReadOnlyList<TrainingInputRecording> recordings,
        string? p1Assignment, string? p2Assignment)
    {
        ArgumentNullException.ThrowIfNull(recordings);
        var byName = new Dictionary<string, TrainingInputRecording>(StringComparer.Ordinal);
        foreach (TrainingInputRecording recording in recordings)
        {
            if (string.IsNullOrWhiteSpace(recording.Name))
                throw new InvalidOperationException("[Input] Restored library contains an unnamed recording.");
            if (!byName.TryAdd(recording.Name, recording))
                throw new InvalidOperationException($"[Input] Restored library contains duplicate '{recording.Name}'.");
        }
        _recordings.Clear();
        foreach (TrainingInputRecording recording in recordings)
            _recordings[recording.Name] = recording;
        _assigned[1] = ResolveAssignment(p1Assignment, byName, 1);
        _assigned[2] = ResolveAssignment(p2Assignment, byName, 2);

        static TrainingInputRecording? ResolveAssignment(string? name,
            IReadOnlyDictionary<string, TrainingInputRecording> byName, int player)
        {
            if (name is null) return null;
            if (!byName.TryGetValue(name, out TrainingInputRecording? recording))
                throw new InvalidOperationException($"[Input] Restored P{player} assignment '{name}' has no library recording.");
            return recording;
        }
    }
}
