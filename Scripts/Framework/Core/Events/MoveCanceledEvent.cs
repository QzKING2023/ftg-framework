#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct MoveCanceledEvent(int PlayerId, string FromMove, string ToMove, string WindowCategory);
