#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

internal sealed record KnockbackTupleRuntimeSnapshot(
    ulong Epoch,
    ulong Generation,
    FTG_Framework.Core.Events.KnockbackAppliedEvent Last,
    bool Terminal);

internal sealed record KnockbackOccupancyRuntimeSnapshot(ulong Epoch, ulong Generation);

internal sealed record StateMachineRuntimeSnapshot(
    Dictionary<int, List<CharacterState>> Stacks,
    Dictionary<int, FTG_Framework.Data.PhysicsResponseProfile> EffectiveProfiles,
    Dictionary<int, GenerationEpochSnapshot> GenerationHighWater,
    Dictionary<int, ReactionRecoverySnapshot> ReactionRecovery,
    Dictionary<int, KnockbackTupleRuntimeSnapshot>? KnockbackTuples = null,
    Dictionary<int, KnockbackOccupancyRuntimeSnapshot>? HitstunOccupancy = null);

internal readonly record struct ReactionRecoverySnapshot(CharacterState ExpectedState, int RemainingFrames);

internal readonly record struct GenerationEpochSnapshot(ulong Epoch, ulong Generation);

internal sealed record FrameDataRuntimeSnapshot(FrameStateSnapshot State);

internal sealed record InputRuntimeSnapshot(
    InputEntry[] P1Directions, InputEntry[] P1Buttons,
    InputEntry[] P2Directions, InputEntry[] P2Buttons,
    int[] ChargeStarts, int[] ChargeEnds, bool[] WasCharging, int LastUpdateFrame);

internal sealed record PhysicsRuntimeSnapshot(
    Dictionary<int, ulong> GenerationHighWater,
    Dictionary<int, PhysicsParticipantSnapshot> Participants,
    Dictionary<int, PhysicsMotionSnapshot> Motions,
    HashSet<PhysicsConsumedContact> ConsumedContacts,
    Dictionary<int, PhysicsTrajectorySnapshot> Trajectories);

internal readonly record struct PhysicsTrajectorySnapshot(
    ulong GenerationId,
    float PositionX,
    float PositionY,
    float VelocityX,
    float VelocityY,
    float GroundY,
    bool Airborne,
    FTG_Framework.Data.KnockbackProfile Profile,
    int ContactFrame,
    bool Completed);

internal sealed record RecordingRuntimeSnapshot(bool IsRecording);

internal sealed record ComboTrackRuntimeSnapshot(
    bool Active,
    int HitCount,
    string CurrentMoveId,
    int StartFrame,
    int PendingAdvantage,
    bool AttackerIdle,
    string LastObservedMoveId);

internal sealed record ComboRuntimeSnapshot(Dictionary<int, ComboTrackRuntimeSnapshot> Tracks);

internal sealed record TrainingInputRecordingRuntimeSnapshot(string Name, string CodecPayloadBase64);

internal sealed record TrainingInputPlaybackSessionRuntimeSnapshot(
    string RecordingName,
    int DummyPlayer,
    int StartFrame,
    int EntryIndex,
    bool Loop,
    bool PendingLoopReset,
    bool PendingStop,
    ulong Epoch);

internal sealed record TrainingInputRuntimeSnapshot(
    TrainingInputRecordingRuntimeSnapshot[] Library,
    string? P1Assignment,
    string? P2Assignment,
    string? SelectedRecording,
    TrainingInputPlaybackSessionRuntimeSnapshot? Session);
