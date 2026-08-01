## Deferred from: code review of v2-1-4-project-template-early (2026-07-31)

- Validate `Scaffold/framework-source-dirs.txt` entries as repository-relative paths and reject rooted or parent-traversing entries before constructing source and destination paths. The unsafe acceptance predates Story 1.4.

## Deferred from: code review of v2-1-7-physics-hot-reload (2026-07-31)

- Bind knockback completion to the exact Hitstun occupancy generation so an older trajectory cannot reset a later Hitstun entry. This behavior belongs to the pre-existing dirty Story 1.6 implementation.
- Guard knockback completion events with invalid player IDs before StateMachine query methods can throw. This event handling belongs to the pre-existing dirty Story 1.6 implementation.

## Deferred from: code review of v2-prep-2-1-runtime-and-scaffold-boundary-hardening (2026-08-01)

- Define safe EventBus `int` frame-number overflow semantics (or migrate to a non-wrapping sequence type) so knockback ordering remains valid beyond `int.MaxValue`; the signed frame counter predates PREP-2.1.
