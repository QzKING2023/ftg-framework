#nullable enable
using System;
using System.Linq;
using System.Text;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Engine.Physics;
using FTG_Framework.Engine.StateMachine;
using FTG_Framework.Input;

namespace FTG_Framework.Core.Balance;

/// <summary>
/// Canonical per-frame state hash for balance trials (S4.1-AC03/AC06). One line
/// per completed frame covering FrameData, StateMachine stacks, Physics
/// trajectories and generation high-water, Combo tracks, and the recent
/// directional input history of the trial's target player. The hash is the
/// determinism contract: repeated identical trials must produce byte-identical
/// trails; first-divergence diagnostics parse the leading frame key and the
/// pipe-delimited component prefixes (fd1/fd2/sm/ph/hw/cb/in).
/// </summary>
internal static class BalanceFrameHash
{
    internal static string Compute(
        int frame,
        int targetPlayer,
        FrameDataEngine frameData,
        StateMachine stateMachine,
        PhysicsEngine physics,
        ComboStateTracker combo,
        InputHistory inputHistory,
        ChargeTracker chargeTracker)
    {
        ArgumentNullException.ThrowIfNull(frameData);
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(combo);
        ArgumentNullException.ThrowIfNull(inputHistory);
        ArgumentNullException.ThrowIfNull(chargeTracker);
        if (targetPlayer is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(targetPlayer));

        var sb = new StringBuilder();
        sb.Append(frame).Append('|');
        var fd = frameData.CaptureRuntimeSnapshot(frame);
        sb.Append("fd1:").Append(fd.State.P1MoveId).Append('/').Append(fd.State.P1CurrentFrame).Append('/').Append(fd.State.P1Phase).Append('|');
        sb.Append("fd2:").Append(fd.State.P2MoveId).Append('/').Append(fd.State.P2CurrentFrame).Append('/').Append(fd.State.P2Phase).Append('|');
        var sm = stateMachine.CaptureRuntimeSnapshot();
        foreach (var kv in sm.Stacks.OrderBy(k => k.Key))
            sb.Append("sm:").Append(kv.Key).Append(':').Append(string.Join(',', kv.Value)).Append('|');
        var ph = physics.CaptureRuntimeSnapshot();
        foreach (var kv in ph.Trajectories.OrderBy(k => k.Key))
            sb.Append("ph:").Append(kv.Key).Append(':').Append(kv.Value.GenerationId).Append(',').Append(kv.Value.PositionX).Append(',').Append(kv.Value.PositionY).Append(',').Append(kv.Value.Completed).Append('|');
        foreach (var kv in ph.GenerationHighWater.OrderBy(k => k.Key))
            sb.Append("hw:").Append(kv.Key).Append('=').Append(kv.Value).Append('|');
        var comboState = combo.CaptureComboState();
        foreach (var kv in comboState.Tracks.OrderBy(k => k.Key))
            sb.Append("cb:").Append(kv.Key).Append(':').Append(kv.Value.Active).Append(',').Append(kv.Value.HitCount).Append(',').Append(kv.Value.CurrentMoveId).Append(',').Append(kv.Value.StartFrame).Append('|');
        // Only the target player's directional input is governed by the trial's
        // recording; hashing the other player's live input would break
        // determinism for target-player-2 trials (S4.1-AC06).
        var input = inputHistory.CaptureRuntimeSnapshot(chargeTracker);
        var directions = targetPlayer == 1 ? input.P1Directions : input.P2Directions;
        sb.Append("in:");
        foreach (var entry in directions.Skip(Math.Max(0, directions.Length - 8)))
            sb.Append(entry.Frame).Append(':').Append((int)entry.Value).Append(',');
        sb.Append(';');
        return sb.ToString();
    }
}
