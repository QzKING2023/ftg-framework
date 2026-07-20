#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

internal sealed class StubInputLeniency : IInputLeniency
{
    private readonly List<MoveInputConfig> _moves = new();

    public void AddMove(MoveInputConfig config) => _moves.Add(config);

    public void RegisterMove(MoveInputConfig config) => _moves.Add(config);

    public IReadOnlyList<MoveInputConfig> GetRegisteredMoves() => _moves.AsReadOnly();

    public List<MatchResult> TryMatchResult { get; set; } = new();

    public int LastFromFrame { get; private set; }
    public int LastToFrame { get; private set; }

    public IReadOnlyList<MatchResult> TryMatch(int playerId) =>
        TryMatchResult.AsReadOnly();

    public IReadOnlyList<MatchResult> TryMatch(int playerId, int fromFrame, int toFrame)
    {
        LastFromFrame = fromFrame;
        LastToFrame = toFrame;
        return TryMatchResult.AsReadOnly();
    }

    public IDataStore DataStore => null!;
    public void Initialize(IDataStore dataStore) { }
    public void Shutdown() { }
}
