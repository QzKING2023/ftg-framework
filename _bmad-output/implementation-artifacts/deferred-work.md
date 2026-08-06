## Deferred from: code review of v2-1-4-project-template-early (2026-07-31)

- Validate `Scaffold/framework-source-dirs.txt` entries as repository-relative paths and reject rooted or parent-traversing entries before constructing source and destination paths. The unsafe acceptance predates Story 1.4.

## Deferred from: code review of v2-1-7-physics-hot-reload (2026-07-31)

- Bind knockback completion to the exact Hitstun occupancy generation so an older trajectory cannot reset a later Hitstun entry. This behavior belongs to the pre-existing dirty Story 1.6 implementation.
- Guard knockback completion events with invalid player IDs before StateMachine query methods can throw. This event handling belongs to the pre-existing dirty Story 1.6 implementation.

## Deferred from: code review of v2-prep-2-1-runtime-and-scaffold-boundary-hardening (2026-08-01)

- Define safe EventBus `int` frame-number overflow semantics (or migrate to a non-wrapping sequence type) so knockback ordering remains valid beyond `int.MaxValue`; the signed frame counter predates PREP-2.1.
## Deferred from: code review of v2-prep-2-4-epic-2-planning-and-evidence-review (2026-08-01)

- PREP-2.3’s `backlog -> done` sprint transition appears in the PREP-2.4 baseline diff but predates PREP-2.4 and belongs to the active PREP-2.3 workstream. Verify it against PREP-2.3 closure evidence rather than reverting it from PREP-2.4. [_bmad-output/implementation-artifacts/sprint-status.yaml:223]

## Deferred from: code review of v2-2-1-c-godot-editor-flow (2026-08-02)

- `Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs:863-893`: `PackageAddon_ProducesValidZip` uses the first matching archive and deletes all `ftg-framework-*.zip` files. This is a real isolation/cleanup risk, but the behavior predates the reviewed slice-C diff and belongs in separate test-infrastructure work.

## Deferred from: code review of v2-2-4-e-manual-acceptance-correction (2026-08-04)

- Preserve the unrelated `project.godot` animation-library importer root-scale setting for its owning workstream rather than changing user-owned baseline state during Story 2.4-E review.
- Repair or remove the pre-existing untracked mojibake `workflow.md` in a documentation-cleanup workstream; it is not referenced by Story 2.4-E evidence.

## Deferred from: code review of v2-4-1-deterministic-balance-testbed (2026-08-05)

- The final window frame's Phase 3/4 metrics (damage, initiations, combo-end) are dropped because the trial completes at its FrameAdvanced before same-frame event dispatch. Practically unreachable (the window ends 120 frames after terminal); a fix ripples window-count semantics (HashedFrames +1, smoke/evidence refresh) — revisit when window semantics are reworked.
- No stall detection: a paused host that stops dispatching FrameAdvanced strands the trial in Running until external Cancel/Shutdown. Host-side policy question; revisit when Story 4.3 dock hosting defines trial lifecycle integration.
- AC02 "validate against both candidate datasets" letter not implemented: validation runs per trial against the live current dataset. The letter would require historical dataset access (versioned snapshots of committed data); the practical per-trial contract is covered by existing tests.
- AC13 letter: `TryStartPlayback` remains a fallible operation after the committed restore swap. It is fully pre-validated by Prepare; a post-swap failure is an invariant-violation detector that marks the trial Failed (never silent). The story documents this interpretation.

## Deferred from: code review of v2-4-2-state-scoped-save-replay (2026-08-06)

- Session-level replay suppression swallows all registered-type publications (Publish/PublishImmediate) for the whole playback session; unregistered types bypass. Latent (no current publisher produces a swallowed event); mandated by Task S4.2-C implementation note.
- Injected lifecycle event bumps the epoch mid-frame and purges same-frame sibling envelopes. Latent: the recorder never records lifecycle events today.
- LoadAndStartReplay lacks the _recorder.IsRecording guard; an active recording is orphaned (Recorder=null, IsRecording stays true). Pre-existing orphan risk.
- StateScopedReplaySmokeTest.cs.uid not copied by the README Copy-Item step — same convention as all prior smoke suites; E4.2-S evidence passed.
- Envelope recording uses _dispatchFrame, stale for PublishImmediate events fired between ProcessFrames; pre-existing, newly load-bearing under the byte-exact hash contract.
