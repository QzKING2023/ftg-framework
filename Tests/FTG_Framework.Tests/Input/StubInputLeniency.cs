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

    public IReadOnlyList<MatchResult> TryMatch(int playerId) =>
        new List<MatchResult>().AsReadOnly();

    public IReadOnlyList<MatchResult> TryMatch(int playerId, int fromFrame, int toFrame) =>
        new List<MatchResult>().AsReadOnly();

    public IDataStore DataStore => null!;
    public void Initialize(IDataStore dataStore) { }
    public void Shutdown() { }
}
