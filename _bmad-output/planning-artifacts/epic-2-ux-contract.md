# Epic 2 Lean UX Contract

Status: Approved  
Date: 2026-08-01

## Scope Decision

UJ-6 is corrected for V2: the workflow supports record, deterministic training playback, save, and restore. Replay frame-step and seeking are out of V2 scope. No acceptance criterion may imply otherwise.

## Shared Interaction Rules

- Godot docks/Controls are thin adapters over pure-C# ViewModels/services.
- Validation is inline and summarized; errors identify the field, rejected value, and recovery action without destroying form state.
- Save/write conflicts never overwrite unseen changes. Show expected/current versions and offer reload or explicit reapply through a newly validated candidate.
- Destructive overwrite, recording replacement, and state-load replacement require a confirmation naming the target. UndoRedo is used for editor mutations where supported.
- Long operations expose progress or a busy state, remain cancellable before commit, and distinguish validation, conflict, I/O, and incompatible-version failure.
- Keyboard focus order follows visual order; every action is keyboard-operable. Runtime training controls also expose controller bindings through the project's input map.
- Status and errors are not color-only. Text remains readable at editor/runtime scaling from 100%-200%; docks define a usable minimum width and scroll instead of clipping.
- Plugin/scene exit clears subscriptions and transient selection. Re-entry reconstructs state from authoritative services.

## Story Flows

- Story 2.1: select/create move → edit canonical fields → validate → save/resolve conflict → undo/redo → runtime-load confirmation.
- Story 2.2: select committed move/profile → edit staged values → apply for next initiation → persist/resolve conflict → observe reload/restart equivalence.
- Story 2.3: observer-only combo count/damage; reset state is visible and deterministic across hit, block, combo end, match/replay/restore, and scene re-entry.
- Story 2.4: choose source/dummy and duration → record → stop/name → play once/loop → stop. Full EventBus Replay disables training playback with a clear ownership message.
- Story 2.5: choose slot/file → save atomically; choose compatible snapshot → inspect metadata → confirm load → show success at restored frame or actionable validation/version error.

## Required Usability Evidence

Each Godot evidence scenario records keyboard navigation, controller operation where applicable, error/conflict recovery, focus restoration, scale/minimum-size behavior, scene/plugin exit/re-entry, and rendered state matching the pure ViewModel. Product Owner sign-off is required before a product story becomes ready-for-dev.
