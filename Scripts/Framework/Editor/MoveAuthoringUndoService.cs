#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Data;

namespace FTG_Framework.Editor;

internal sealed class MoveAuthoringUndoService
{
    private readonly IEditorContext _context;
    private readonly MoveDatasetPersistence _persistence;
    private readonly string _documentIdentifier;

    internal MoveSaveResult? LastResult { get; private set; }

    internal MoveAuthoringUndoService(
        IEditorContext context, MoveDatasetPersistence persistence, string documentIdentifier)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _documentIdentifier = documentIdentifier;
    }

    internal void SaveUndoable(MoveAuthoringCandidate desired)
    {
        LastResult = null;
        IReadOnlyList<MoveAuthoringMove> before = MoveAuthoringCandidate
            .FromDocument(_persistence.Load(_documentIdentifier)).Moves.ToArray();
        IReadOnlyList<MoveAuthoringMove> after = desired.Moves.ToArray();
        LastResult = _persistence.Save(_documentIdentifier,
            desired with { Moves = after });
        if (LastResult.Status != MoveSaveStatus.Succeeded)
            return;

        MoveContentIdentity? undoExpected = LastResult.CurrentIdentity;
        MoveContentIdentity? redoExpected = null;

        void Do()
        {
            if (redoExpected is null)
            {
                LastResult = null;
                return;
            }
            LastResult = _persistence.Save(_documentIdentifier,
                desired with { Moves = after, SourceIdentity = redoExpected });
            if (LastResult.Status == MoveSaveStatus.Succeeded)
            {
                undoExpected = LastResult.CurrentIdentity;
                redoExpected = null;
            }
        }

        void Undo()
        {
            if (undoExpected is null)
            {
                LastResult = null;
                return;
            }
            LastResult = _persistence.Save(_documentIdentifier,
                desired with { Moves = before, SourceIdentity = undoExpected });
            if (LastResult.Status == MoveSaveStatus.Succeeded)
            {
                redoExpected = LastResult.CurrentIdentity;
                undoExpected = null;
            }
        }

        _context.CreateUndoAction(
            $"Save move dataset {_documentIdentifier}",
            Do,
            Undo,
            executeDo: false);
    }
}
