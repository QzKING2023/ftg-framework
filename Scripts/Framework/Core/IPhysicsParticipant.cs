#nullable enable

namespace FTG_Framework.Core;

public interface IPhysicsParticipant
{
    int PlayerId { get; }
    PhysicsParticipantSnapshot CapturePhysicsSnapshot();
    void ApplyPhysicsState(PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion) { }
    PhysicsMotionSnapshot CaptureMotionSnapshot()
    {
        var snapshot = CapturePhysicsSnapshot();
        return new PhysicsMotionSnapshot(0, 0, false, snapshot.WorldY);
    }
}

internal interface IRestorablePhysicsParticipant : IPhysicsParticipant
{
    void RestoreRuntimeSnapshot(PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion);
}
