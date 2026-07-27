#nullable enable
namespace FTG_Framework.Core;

/// <summary>
/// Contract for objects managed by <see cref="PoolCore{T}"/> and <see cref="Pool{T}"/>.
/// <c>Reset()</c> is called on every <c>Acquire</c> to restore the object
/// to a clean state before it is returned to the caller.
/// </summary>
/// <remarks>
/// Implementations must:
/// <list type="bullet">
/// <item>Disconnect any dynamically-connected signals.</item>
/// <item>Stop and clear active <c>Tween</c> instances.</item>
/// <item>Clear residual velocity, forces, and collision state (if a physics body).</item>
/// <item>Restore visual defaults: position, scale, modulation, visibility.</item>
/// </list>
/// <c>Reset()</c> must be safe to call on a freshly-instantiated instance that has
/// never been used — it should be idempotent.
/// </remarks>
public interface IPoolable
{
    void Reset();
}
