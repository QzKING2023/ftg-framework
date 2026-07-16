#nullable enable

namespace FTG_Framework.Core;

public readonly record struct MatchResult(string MoveId, ButtonValue RequiredButton, int MatchedAtFrame);
