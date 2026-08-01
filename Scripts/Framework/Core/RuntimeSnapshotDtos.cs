#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

internal sealed record StateMachineRuntimeSnapshot(
    Dictionary<int, List<CharacterState>> Stacks,
    Dictionary<int, FTG_Framework.Data.PhysicsResponseProfile> EffectiveProfiles,
    Dictionary<int, GenerationEpochSnapshot> GenerationHighWater);

internal readonly record struct GenerationEpochSnapshot(ulong Epoch, ulong Generation);

internal sealed record FrameDataRuntimeSnapshot(FrameStateSnapshot State);

internal sealed record InputRuntimeSnapshot(
    InputEntry[] P1Directions, InputEntry[] P1Buttons,
    InputEntry[] P2Directions, InputEntry[] P2Buttons,
    int[] ChargeStarts, int[] ChargeEnds, bool[] WasCharging, int LastUpdateFrame);

internal sealed record PhysicsRuntimeSnapshot(
    Dictionary<int, ulong> GenerationHighWater,
    Dictionary<int, PhysicsParticipantSnapshot> Participants,
    Dictionary<int, PhysicsMotionSnapshot> Motions);

internal sealed record RecordingRuntimeSnapshot(bool IsRecording);
