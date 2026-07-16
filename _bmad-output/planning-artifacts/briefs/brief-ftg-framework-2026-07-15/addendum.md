# Addendum: FTG Framework

Supplementary material captured during Product Brief discovery. These details are not part of the brief itself but are preserved for downstream consumption (PRD, Architecture, Epics/Stories).

## Combo System — UNI Reference

The user referenced Under Night In-Birth's combo system as an ideal model: normal attacks can cancel into each other freely, but the same move cannot be used twice in the same chain. The exception is the "A" series (light attack chain cancels). This goes beyond basic Gatling table registration and will need to be addressed in the combo API design during architecture.

## Input System — Architectural Constraints

- **Dual-timeline input storage**: Button input history and directional input history must be maintained independently, without one interfering with the other. This is a critical architectural invariant.
- **Per-move granularity**: Input leniency rules are specified per special move, not globally. 623-type DPs accept 636 and 323; 236236 supers accept 23626; but basic 236/214 quarter-circles remain strict. The configuration model must support this granularity.
- **Buffer timing**: The ~6f input buffer at 60fps equates to approximately 0.1 seconds — chosen because this duration is within the range where muscle memory can form reliably. Charge retention (~5f) follows a similar principle: brief enough to not feel "sticky," long enough to enable flexible combo routing.

## Training Mode Feature Set (from SF6 reference)

The following training mode features were identified as the gold standard during discovery:
- Frame data panel (startup, active, recovery, hit/block advantage per move)
- Frame advantage display (real-time +/- frames after hit/block)
- Historical input log (scrollable, with frame-level timestamps)
- Frame-by-frame playback (pause, advance, rewind)
- Hitbox/hurtbox overlay rendering

## Module Priority

V1 delivers three modules in order: Input System → Frame Data Engine + Training Mode Visualization → Combo System API. Post-V1 modules: Character State Machine tools, Hitbox System, Replay System, Object Pool.
