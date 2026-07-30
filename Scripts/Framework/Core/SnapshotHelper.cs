#nullable enable

namespace FTG_Framework.Core;

/// <summary>
/// AD-9 centralized snapshot helper. Every hot-reloadable data consumer calls
/// <c>Snapshot.Of(data)</c> on initiation so the action uses a stable version
/// for its full lifecycle, even if a <c>DataReloadedEvent</c> arrives mid-action.
///
/// For immutable types (record structs, init-only objects), this returns the
/// value/reference itself. The helper exists as a centralized audit point —
/// every snapshot code path is grep-able via "Snapshot.Of".
/// </summary>
public static class Snapshot
{
    public static T Of<T>(T data) => data;
}
