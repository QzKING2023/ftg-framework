#nullable enable

namespace FTG_Framework.Core;

public interface IPhysicsEngine : IModule
{
    void Register(IPhysicsParticipant participant);
    void Unregister(IPhysicsParticipant participant);
    void Update();
    bool TryGetHitContext(
        int attackerId, int defenderId, string hitboxId, int contactFrame,
        out HitContext context);
}
