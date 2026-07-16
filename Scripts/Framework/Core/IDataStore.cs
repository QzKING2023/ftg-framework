#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

public interface IDataStore
{
    MoveDefinition? GetMove(string moveId);
    IReadOnlyList<MoveDefinition> GetAllMoves();
}
