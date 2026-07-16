#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

public interface IInputHistory
{
    void RecordInput(int playerId, InputType type, int value);
    IReadOnlyList<InputEntry> GetDirectionalHistory(int playerId);
    IReadOnlyList<InputEntry> GetButtonHistory(int playerId);
    IReadOnlyList<InputEntry> GetHistory(int playerId, InputType type);
    int Capacity { get; }
}
