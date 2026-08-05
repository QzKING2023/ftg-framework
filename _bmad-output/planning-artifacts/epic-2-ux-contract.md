# Epic 2 Lean UX Contract

This is the Epic 2 specialization of the [project V2 UX contract](./ux-v2-lean-contract.md) and inherits every AD-21 interaction requirement.

Status: Approved  
Date: 2026-08-04

## Scope Decision

UJ-6 distinguishes V1 FR-11 training frame controls, FR-26 canonical-input training playback, and FR-28 authoritative EventBus Replay. Replay supports real-time playback and pause/resume; seeking, rewind, and replay-envelope single-frame stepping are out of scope. No acceptance criterion may imply otherwise.

## Shared Interaction Rules

- Godot docks/Controls are thin adapters over pure-C# ViewModels/services.
- Validation is inline and summarized; errors identify the field, rejected value, and recovery action without destroying form state.
- Save/write conflicts never overwrite unseen changes. Show expected/current versions and offer reload or explicit reapply through a newly validated candidate.
- Destructive overwrite, recording replacement, and state-load replacement require a confirmation naming the target. UndoRedo is used for editor mutations where supported.
- Long operations expose progress or a busy state, remain cancellable before commit, and distinguish validation, conflict, I/O, and incompatible-version failure.
- Keyboard focus order follows visual order; every action is keyboard-operable. Runtime training controls also expose controller bindings through the project's input map.
- Status and errors are not color-only. Text remains readable at editor/runtime scaling from 100%-200%; docks define a usable minimum width and scroll instead of clipping.
- Plugin/scene exit clears subscriptions and transient selection. Re-entry reconstructs state from authoritative services.
- Story 2.2 runtime tuning uses the shared top-right region; Story 2.4 recording/playback uses the shared bottom-right region. Neither panel assigns an absolute window position.
- Layout recomputation preserves staged tuning edits, recording selection, playback state, and focus. It must not trigger persistence, recording, playback, or simulation mutations.
- Left-side training guidance and diagnostics use wrapping plus bounded scrolling/collapse. A vertically clipped or unreachable message fails usability acceptance.
- Story 2.4 primary actions use the project InputMap actions and display their currently resolved bindings.

## Story Flows

- Story 2.1: select/create move → edit canonical fields → validate → save/resolve conflict → undo/redo → runtime-load confirmation.
- Story 2.2: select committed move/profile → edit staged values → apply for next initiation → persist/resolve conflict → observe reload/restart equivalence.
- Story 2.3: observer-only combo count/damage; reset state is visible and deterministic across hit, block, combo end, match/replay/restore, and scene re-entry.
- Story 2.4: choose source/dummy → toggle `Start Recording` by panel or shortcut → toggle `Stop Recording` with stop-derived duration → name or explicitly confirm atomic same-name overwrite → select/assign → play once/loop by panel or shortcut → stop playback and release ownership. The next loop iteration begins on the immediately following schedulable frame with no additional wait. The panel exposes one normal recording toggle, remains in the bottom-right responsive region through resize/fullscreen changes, and preserves the prior recording on overwrite cancellation/failure. Full EventBus Replay disables both panel and shortcut training actions with a clear ownership message.
- Story 2.5: choose slot/file → save atomically; choose compatible snapshot → inspect metadata → confirm load → show success at restored frame or actionable validation/version error.

## Required Usability Evidence

Each Godot evidence scenario records keyboard navigation, resolved shortcut operation, controller operation where applicable, error/conflict recovery, focus restoration, scale/minimum-size behavior, window resize and fullscreen transitions, overlay non-overlap, left-side text readability, scene/plugin exit/re-entry, and rendered state matching the pure ViewModel. Product Owner sign-off is required before a product story becomes ready-for-dev.
