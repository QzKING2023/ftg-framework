#nullable enable

namespace FTG_Framework.Core;

public interface IPhysicsParticipant
{
    int PlayerId { get; }
    PhysicsParticipantSnapshot CapturePhysicsSnapshot();
    PhysicsMotionSnapshot CaptureMotionSnapshot()
    {
        var snapshot = CapturePhysicsSnapshot();
        return new PhysicsMotionSnapshot(0, 0, false, snapshot.WorldY);
    }
}
