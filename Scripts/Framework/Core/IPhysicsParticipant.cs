#nullable enable

namespace FTG_Framework.Core;

public interface IPhysicsParticipant
{
    int PlayerId { get; }
    PhysicsParticipantSnapshot CapturePhysicsSnapshot();
}
