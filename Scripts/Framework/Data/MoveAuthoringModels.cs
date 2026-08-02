#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace FTG_Framework.Data;

public sealed record MoveContentIdentity(string Sha256)
{
    internal static MoveContentIdentity FromBytes(ReadOnlySpan<byte> bytes) =>
        new(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}

public sealed record MoveValidationError(
    string FieldPath, string RejectedValue, string Message, string RecoveryAction);

public sealed record MoveValidationResult(IReadOnlyList<MoveValidationError> Errors)
{
    public bool Success => Errors.Count == 0;
    public string? FirstInvalidField => Errors.Count == 0 ? null : Errors[0].FieldPath;
    public static MoveValidationResult Valid { get; } = new(Array.Empty<MoveValidationError>());
}

public sealed class MoveDatasetFormatException : FormatException
{
    public string FieldPath { get; }
    public string RejectedValue { get; }
    public string RecoveryAction { get; }

    public MoveDatasetFormatException(string fieldPath, string rejectedValue, string message, string recoveryAction,
        Exception? inner = null) : base($"[Data] {fieldPath}: {message}", inner)
    {
        FieldPath = fieldPath;
        RejectedValue = rejectedValue;
        RecoveryAction = recoveryAction;
    }

    public MoveValidationError ToError() => new(FieldPath, RejectedValue, Message, RecoveryAction);
}

public sealed record MoveAuthoringMove(
    string MoveId,
    int Startup,
    int Active,
    int Recovery,
    int HitAdvantage,
    int BlockAdvantage,
    int Damage,
    bool ChainRepeatable,
    string KnockbackProfileId,
    string? MoveName,
    IReadOnlyList<CancelWindow> CancelWindows,
    IReadOnlyList<CollisionFrameDefinition> CollisionFrames)
{
    internal static MoveAuthoringMove FromRuntime(MoveDefinition move) => new(
        move.MoveId, move.Startup, move.Active, move.Recovery, move.HitAdvantage,
        move.BlockAdvantage, move.Damage, move.ChainRepeatable,
        move.KnockbackProfileId ?? string.Empty, move.MoveName,
        Freeze(move.CancelWindows), Freeze(move.CollisionFrames));

    internal MoveDefinition ToRuntime() => new()
    {
        MoveId = MoveId,
        Startup = Startup,
        Active = Active,
        Recovery = Recovery,
        HitAdvantage = HitAdvantage,
        BlockAdvantage = BlockAdvantage,
        Damage = Damage,
        ChainRepeatable = ChainRepeatable,
        KnockbackProfileId = KnockbackProfileId,
        MoveName = MoveName,
        CancelWindows = Freeze(CancelWindows),
        CollisionFrames = Freeze(CollisionFrames),
    };

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

public sealed record MoveDatasetDocument(
    int SchemaVersion, IReadOnlyList<MoveDefinition> Moves, MoveContentIdentity ContentIdentity);

public sealed record MoveAuthoringCandidate(
    int SchemaVersion, IReadOnlyList<MoveAuthoringMove> Moves, MoveContentIdentity SourceIdentity)
{
    public static MoveAuthoringCandidate FromDocument(MoveDatasetDocument document) => new(
        document.SchemaVersion,
        new ReadOnlyCollection<MoveAuthoringMove>(document.Moves.Select(MoveAuthoringMove.FromRuntime).ToArray()),
        document.ContentIdentity);

    public MoveAuthoringCandidate EditMove(string moveId, Func<MoveAuthoringMove, MoveAuthoringMove> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        bool found = false;
        var changed = Moves.Select(move =>
        {
            if (!string.Equals(move.MoveId, moveId, StringComparison.Ordinal)) return move;
            found = true;
            return edit(move);
        }).ToArray();
        if (!found)
            throw new KeyNotFoundException($"[Data] Move '{moveId}' was not found.");
        return this with { Moves = new ReadOnlyCollection<MoveAuthoringMove>(changed) };
    }

    internal MoveDatasetDocument ToDocument() => new(
        SchemaVersion,
        new ReadOnlyCollection<MoveDefinition>(Moves.Select(move => move.ToRuntime()).ToArray()),
        SourceIdentity);
}

public sealed class MoveAuthoringViewModel
{
    private readonly MoveDatasetPersistence? _persistence;
    private readonly string? _documentIdentifier;
    private MoveAuthoringCandidate _loadedCandidate;
    public MoveAuthoringCandidate CurrentCandidate { get; private set; }
    public MoveValidationResult LastResult { get; private set; } = MoveValidationResult.Valid;
    public string SummaryStatus => LastResult.Success ? "Ready to save" : $"{LastResult.Errors.Count} validation error(s)";

    public MoveAuthoringViewModel(MoveDatasetDocument document)
    {
        CurrentCandidate = MoveAuthoringCandidate.FromDocument(document);
        _loadedCandidate = CurrentCandidate;
    }

    internal MoveAuthoringViewModel(
        MoveDatasetDocument document, MoveDatasetPersistence persistence, string documentIdentifier)
    {
        CurrentCandidate = MoveAuthoringCandidate.FromDocument(document);
        _loadedCandidate = CurrentCandidate;
        _persistence = persistence;
        _documentIdentifier = documentIdentifier;
    }

    public void Edit(string moveId, Func<MoveAuthoringMove, MoveAuthoringMove> edit) =>
        CurrentCandidate = CurrentCandidate.EditMove(moveId, edit);

    public void Add(MoveAuthoringMove move)
    {
        ArgumentNullException.ThrowIfNull(move);
        CurrentCandidate = CurrentCandidate with
        {
            Moves = new ReadOnlyCollection<MoveAuthoringMove>(CurrentCandidate.Moves.Append(move).ToArray())
        };
    }

    public MoveValidationResult Validate()
    {
        LastResult = MoveDatasetCodec.Validate(CurrentCandidate);
        return LastResult;
    }

    public void Reload(MoveDatasetDocument document)
    {
        CurrentCandidate = MoveAuthoringCandidate.FromDocument(document);
        _loadedCandidate = CurrentCandidate;
        LastResult = MoveValidationResult.Valid;
    }

    internal MoveSaveResult Save()
    {
        if (_persistence is null || _documentIdentifier is null)
            throw new InvalidOperationException("[Data] This ViewModel has no persistence session.");
        var result = _persistence.Save(_documentIdentifier, CurrentCandidate);
        if (result.Status == MoveSaveStatus.Succeeded)
            Reload(_persistence.Load(_documentIdentifier));
        else if (result.Errors is { Count: > 0 })
            LastResult = new MoveValidationResult(result.Errors);
        return result;
    }

    internal void ReloadCommitted()
    {
        if (_persistence is null || _documentIdentifier is null)
            throw new InvalidOperationException("[Data] This ViewModel has no persistence session.");
        Reload(_persistence.Load(_documentIdentifier));
    }

    internal void ReapplyCommitted()
    {
        if (_persistence is null || _documentIdentifier is null)
            throw new InvalidOperationException("[Data] This ViewModel has no persistence session.");
        ReapplyAfterConflict(_persistence.Load(_documentIdentifier));
    }

    internal void ReapplyAfterConflict(MoveDatasetDocument current)
    {
        MoveValidationResult editedValidation = MoveDatasetCodec.Validate(CurrentCandidate);
        if (!editedValidation.Success)
        {
            LastResult = editedValidation;
            return;
        }

        var bases = _loadedCandidate.Moves
            .Select((move, index) => (move, index))
            .ToDictionary(item => item.move.MoveId, item => item, StringComparer.Ordinal);
        var rebased = MoveAuthoringCandidate.FromDocument(current);
        foreach (var move in rebased.Moves.ToArray())
            if (bases.TryGetValue(move.MoveId, out var basis) && basis.index < CurrentCandidate.Moves.Count)
            {
                MoveAuthoringMove edited = CurrentCandidate.Moves[basis.index];
                rebased = rebased.EditMove(move.MoveId,
                    latest => ApplyChangedFields(basis.move, edited, latest));
            }

        foreach (MoveAuthoringMove added in CurrentCandidate.Moves.Skip(_loadedCandidate.Moves.Count))
            rebased = rebased with
            {
                Moves = new ReadOnlyCollection<MoveAuthoringMove>(rebased.Moves.Append(added).ToArray())
            };
        CurrentCandidate = rebased;
        _loadedCandidate = MoveAuthoringCandidate.FromDocument(current);
        LastResult = Validate();
    }

    private static MoveAuthoringMove ApplyChangedFields(
        MoveAuthoringMove original, MoveAuthoringMove edited, MoveAuthoringMove latest) => latest with
    {
        MoveId = edited.MoveId != original.MoveId ? edited.MoveId : latest.MoveId,
        MoveName = edited.MoveName != original.MoveName ? edited.MoveName : latest.MoveName,
        Startup = edited.Startup != original.Startup ? edited.Startup : latest.Startup,
        Active = edited.Active != original.Active ? edited.Active : latest.Active,
        Recovery = edited.Recovery != original.Recovery ? edited.Recovery : latest.Recovery,
        HitAdvantage = edited.HitAdvantage != original.HitAdvantage ? edited.HitAdvantage : latest.HitAdvantage,
        BlockAdvantage = edited.BlockAdvantage != original.BlockAdvantage ? edited.BlockAdvantage : latest.BlockAdvantage,
        Damage = edited.Damage != original.Damage ? edited.Damage : latest.Damage,
        ChainRepeatable = edited.ChainRepeatable != original.ChainRepeatable ? edited.ChainRepeatable : latest.ChainRepeatable,
        KnockbackProfileId = edited.KnockbackProfileId != original.KnockbackProfileId ? edited.KnockbackProfileId : latest.KnockbackProfileId,
        CancelWindows = !edited.CancelWindows.SequenceEqual(original.CancelWindows) ? edited.CancelWindows : latest.CancelWindows,
        CollisionFrames = !edited.CollisionFrames.SequenceEqual(original.CollisionFrames) ? edited.CollisionFrames : latest.CollisionFrames,
    };
}

internal sealed class MoveDefinitionSemanticComparer : IEqualityComparer<MoveDefinition>
{
    public static MoveDefinitionSemanticComparer Instance { get; } = new();
    public bool Equals(MoveDefinition? x, MoveDefinition? y) =>
        x is not null && y is not null &&
        MoveDatasetCodec.SerializeMovesForComparison(new[] { x })
            .SequenceEqual(MoveDatasetCodec.SerializeMovesForComparison(new[] { y }));
    public int GetHashCode(MoveDefinition obj) => obj.MoveId.GetHashCode(StringComparison.Ordinal);
}
