#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace FTG_Framework.Data;

public sealed record DataContentIdentity(string Sha256)
{
    internal static DataContentIdentity FromBytes(ReadOnlySpan<byte> bytes) =>
        new(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}

internal enum PhysicsDocumentKind { Knockback, Response }

internal sealed record MoveDatasetBaseline(
    IReadOnlyList<MoveDefinition> Moves,
    ulong DatasetVersion,
    MoveContentIdentity ContentIdentity);

internal sealed record PhysicsDatasetBaseline(
    IReadOnlyList<KnockbackProfile> KnockbackProfiles,
    IReadOnlyList<PhysicsResponseProfile> ResponseProfiles,
    ulong DatasetVersion,
    DataContentIdentity KnockbackIdentity,
    DataContentIdentity ResponseIdentity);

public enum RuntimeTuningSelectionKind { Move, KnockbackProfile, PhysicsResponseProfile }

public sealed record RuntimeTuningSelection(
    RuntimeTuningSelectionKind Kind, string DocumentIdentifier, string ItemId);

public sealed record RuntimeTuningBaseline(
    RuntimeTuningSelection Selection,
    IReadOnlyDictionary<string, string> Fields,
    string ContentIdentity,
    ulong DatasetVersion);

public sealed record RuntimeTuningCommitRequest(
    RuntimeTuningSelection Selection,
    IReadOnlyDictionary<string, string> Fields,
    string ExpectedIdentity,
    ulong ExpectedDatasetVersion,
    ulong SessionToken);

public sealed record RuntimeTuningValidationError(
    string FieldPath, string RejectedValue, string Message, string RecoveryAction);

public sealed record RuntimeTuningValidationResult(IReadOnlyList<RuntimeTuningValidationError> Errors)
{
    public bool Success => Errors.Count == 0;
    public string? FirstInvalidField => Errors.Count == 0 ? null : Errors[0].FieldPath;
    public static RuntimeTuningValidationResult Valid { get; } = new(Array.Empty<RuntimeTuningValidationError>());
}

public enum RuntimeTuningCommitStatus
{
    Succeeded,
    ValidationFailed,
    Conflict,
    IoFailure,
    Cancelled,
    IncompatibleVersion,
    CommittedWithDiagnostic
}

public sealed record RuntimeTuningCommitResult(
    RuntimeTuningCommitStatus Status,
    string? ExpectedIdentity = null,
    string? CurrentIdentity = null,
    string? Diagnostic = null,
    IReadOnlyList<RuntimeTuningValidationError>? Errors = null)
{
    public bool Committed => Status is RuntimeTuningCommitStatus.Succeeded or RuntimeTuningCommitStatus.CommittedWithDiagnostic;
}

public interface IRuntimeTuningService
{
    RuntimeTuningBaseline Load(RuntimeTuningSelection selection);
    RuntimeTuningValidationResult Validate(RuntimeTuningCommitRequest request);
    RuntimeTuningCommitResult Commit(RuntimeTuningCommitRequest request);
}

public sealed class RuntimeTuningSessionAuthority
{
    private readonly object _sync = new();
    private ulong _token = 1;
    public ulong Capture() { lock (_sync) return _token; }
    public bool IsCurrent(ulong token) { lock (_sync) return token == _token; }
    public ulong Invalidate()
    {
        lock (_sync)
        {
            _token = checked(_token + 1);
            return _token;
        }
    }
}

internal static class RuntimeTuningFields
{
    internal static IReadOnlyDictionary<string, string> Freeze(IDictionary<string, string> fields) =>
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(fields, StringComparer.Ordinal));
}
