---
story_key: v2-prep-2-4-epic-2-planning-and-evidence-review
story_id: PREP-2.4
date_created: 2026-08-01
baseline_commit: cddba608b099b00e7a0f25cc5d0e635286ff00a4
---

# PREP-2.4: Epic 2 Planning and Evidence Review

Status: done

## Story

As a product owner coordinating Epic 2 readiness,
I want corrected Stories 2.1-2.5, consistent tracking, and an approved risk/evidence matrix,
so that implementation begins with shared scope, architecture, and proof expectations.

## Acceptance Criteria

1. **P2.4-AC01 — Corrected stories.**  
   **Given** the approved correction, canonical SPEC, and final Architecture Spine  
   **When** Stories 2.1 through 2.5 are reviewed  
   **Then** each Story preserves FR-23 through FR-27 user intent while incorporating its required lifecycle, ownership, immutability, concurrency, versioning, failure, and evidence boundaries  
   **And** no Story carries forward an acceptance criterion that conflicts with AD-12, AD-13, AD-15, AD-18, AD-19, or AD-20.

2. **P2.4-AC02 — Executable Story 2.4/2.5 dependency.**  
   **Given** Story 2.4 requires a recording round trip with Story 2.5  
   **When** Epic 2 Story order and dependencies are finalized  
   **Then** standalone recording acceptance is independently completable before the integration proof  
   **And** the integration proof is assigned only after the required snapshot/recording contract exists, with no dependency on a future unfinished Story.

3. **P2.4-AC03 — Bounded vertical slices.**  
   **Given** a Story exceeds one developer-agent context  
   **When** the planning review applies the sizing rule  
   **Then** it is divided into independently verifiable vertical slices with observable outcomes  
   **And** no slice consists solely of an internal technical layer.

4. **P2.4-AC04 — Cross-story risk review.**  
   **Given** the Epic 2 risk/evidence checklist is created  
   **When** each Story row is completed  
   **Then** it identifies applicable lifecycle epoch, ownership, generation, event order, immutability, invalid-input, failure-atomicity, concurrency, test-isolation, runtime/scaffold parity, and determinism risks  
   **And** every applicable risk maps to a specific acceptance criterion and evidence type.

5. **P2.4-AC05 — Evidence contract per story.**  
   **Given** evidence requirements are assigned to a Story  
   **When** its readiness row is reviewed  
   **Then** required unit, service/Data integration, EventBus integration, fault-injection, concurrency, lifecycle, Godot editor/runtime, scaffold, round-trip, and end-to-end evidence is explicitly marked required or not applicable with rationale  
   **And** the expected evidence location is recorded before the Story can enter `ready-for-dev`.

6. **P2.4-AC06 — Epic 1 tracking alignment.**  
   **Given** Story 1.3, its manual verification guide, V2 Epic 1 retrospective, and sprint status are compared  
   **When** PREP-2.4 aligns tracking  
   **Then** they consistently record the completed automated suite, generated-project/Godot evidence, manual checks, and Q1625 acceptance  
   **And** stale pending-verification wording is removed without altering historical evidence.

7. **P2.4-AC07 — Unambiguous Epic 2 start gate.**  
   **Given** `sprint-status.yaml` contains the Epic 2 start-gate comment  
   **When** tracking alignment is applied  
   **Then** it requires all four PREP entries, all nine `v2-1` actions, every Architecture Adoption Gate row, the completed risk/evidence checklist, and PO/Architect/QA approvals  
   **And** no weaker interpretation can move `v2-epic-2` from backlog.

8. **P2.4-AC08 — Independent role approval.**  
   **Given** revised Stories 2.1-2.5 and their checklist rows are complete  
   **When** Product Owner, Architect, and QA review them  
   **Then** each role records approval or actionable rejection with date and artifact reference  
   **And** PREP-2.4 cannot close while any required role rejection or unresolved blocker remains.

9. **P2.4-AC09 — Gate scope control.**  
   **Given** an implementation finding does not violate an approved architecture invariant or existing gate condition  
   **When** it is triaged during PREP-2.4  
   **Then** it enters the normal backlog without expanding the gate  
   **And** adding another gate package requires a newly approved course correction.

10. **P2.4-AC10 — Gate-safe closure.**  
    **Given** all PREP-2.4 criteria and role approvals are satisfied  
    **When** sprint tracking is updated  
    **Then** `v2-prep-2-4-epic-2-planning-and-evidence-review` and the corresponding planning/tracking retrospective actions may move to `done`  
    **And** Epic 2 may leave backlog only if PREP-2.1-2.3 and every remaining gate condition are also complete.

## Tasks / Subtasks

- [x] Establish the planning baseline and decision inventory (AC: 1-3, 9)
  - [x] Read the full V2 Epic 1 retrospective, PREP-2.1-2.3 story/evidence records, current Architecture Spine, sprint status, and the current implementations/contracts cited below.
  - [x] Review and revise the existing canonical Epic 2 package under `_bmad-output/planning-artifacts/epics/`; do not treat stale V1 `docs/architecture.md` planned-module tables as V2 requirements.
  - [x] Record missing product inputs as explicit blocking decisions with owner and affected story. Do not silently choose UX, file formats, damage rules, recording limits, or compatibility policy.
  - [x] Define Epic 2 entry/exit criteria and trace FR-23-27 to exactly one primary story while recording cross-story dependencies.

- [x] Apply the cross-story lifecycle and architecture risk checklist (AC: 4)
  - [x] Create a row for each story/risk pair covering ownership, lifecycle epoch, generation identity, AD-12 phase order, non-reentrant publication, immutable payloads, transactional atomicity, stale writers, snapshot Prepare/Commit, scene/plugin teardown, scaffold parity, and tracking isolation.
  - [x] For every applicable row, point to an acceptance criterion and verification row; for every non-applicable row, record a technical rationale.
  - [x] Escalate any proposal that bypasses established Core interfaces, duplicates Data validation/persistence, serializes Godot/live containers, or creates a third playback mode.

- [x] Rewrite and review Stories 2.1-2.5 (AC: 1-3, 9)
  - [x] Story 2.1: thin EditorPlugin adapter, Data authority, optimistic concurrency, undo/redo, lifecycle cleanup, editor and scaffold proof.
  - [x] Story 2.2: transactional runtime tuning/writeback, watcher-loop control, expected-version conflicts, atomic persistence and fault equivalence.
  - [x] Story 2.3: authoritative combo/damage ownership, observer-only ViewModel/UI, reset semantics and subscription lifecycle.
  - [x] Story 2.4: training recording/playback representation, accepted mutual-exclusion coordinator, lifecycle/snapshot participation and deterministic frame semantics.
  - [x] Story 2.5: snapshot coordinator composition, value-only versioned container, migration/rejection policy, failure-atomic persistence and exact restore semantics.
  - [x] Include explicit prerequisites so no story consumes a contract or prior story until its approval/evidence gate is accepted.

- [x] Define the verification and evidence contract (AC: 4, 5)
  - [x] Build one evidence matrix per story across unit, integration, full regression, stress/fault, Godot editor/runtime, generated scaffold, and end-to-end verification.
  - [x] Require checked-in evidence indexes/manifests with exact commands/scenarios, OS, .NET SDK, Godot version, commit/hash, pass/fail/skip counts, exit codes, duration/seed where relevant, reviewer, acceptance state, and SHA-256 inventory.
  - [x] Require real Godot editor proof for EditorPlugin behavior and real Godot runtime proof for UI/input/save-load paths; `dotnet test` substitutes only where an explicit limitation and owner-approved alternative are documented.
  - [x] Keep evidence generation outside ordinary tests; tests must not mutate tracked evidence directories.

- [x] Run approval and traceability review (AC: 8, 9)
  - [x] Audit every AC against FR-23-27, AD-3/6/9/12/15/18/19/20, the relevant PREP contract, risk row, evidence row, and dependency.
  - [x] Record separate Product Owner and System Architect decisions against the exact reviewed commit/hash. A conditional or missing approval is not acceptance.
  - [x] Resolve every blocking finding in the documents and repeat approval; retain rejected superseded review records rather than rewriting history.

- [x] Align completed Epic 1 evidence and strengthen the start gate (AC: 6, 7)
  - [x] Reconcile Story 1.3 and its manual verification guide with the 747-test Epic 1 acceptance baseline, generated-project/Godot proof, completed manual checks, and Q1625 acceptance; remove only stale pending language and preserve historical counts/outcomes.
  - [x] Add the exact AC07 gate conditions to the sprint-status comment; the comment is normative and must not imply that PO/Architect approval alone is enough.
  - [x] Link retrospective action 6 to the alignment evidence and actions 7-9 to the accepted checklist, evidence standards, and role approvals.

- [x] Index evidence and close only the owned gate (AC: 10)
  - [x] Create `_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/index.md` plus immutable approval/risk/evidence/traceability artifacts and hashes.
  - [x] Link accepted evidence from retrospective actions 7-9. Do not mark action 6 (tracking alignment) or PREP-2.4 complete unless its independent evidence already proves the exact required state.
  - [x] Follow normal story transitions. At accepted closure update only PREP-2.4, its exact owned open `v2-1` action rows, and `last_updated`; preserve comments/order and keep `v2-epic-2: backlog` until separate kickoff authorization.

### Review Findings

- [x] [Review][Patch] [High] Regenerate `tracking-diff.patch` from durable PREP-2.3 baseline `a0c7043` so it contains only PREP-2.4-owned retrospective and sprint transitions; the current patch includes PREP-2.3-owned changes and contradicts the claimed exact owned delta. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/tracking-diff.patch:1]
- [x] [Review][Patch] [High] Replace all five `Pending` rows in the story Evidence Index with accepted artifact links/outcomes; the story is in review while its canonical evidence table still says no AC group is complete. [_bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md:229]
- [x] [Review][Patch] [Medium] Remove superseded completion notes saying action 9/PREP-2.4 remain open and validation still reports blockers; they contradict the subsequent accepted reapproval and current sprint state. [_bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md:262]
- [x] [Review][Patch] [Medium] Replace the manual guide's obsolete instruction to move Story 1.3 from `review` to `done` with a historical completion statement. [_bmad-output/implementation-artifacts/v2-epic-1-manual-verification-guide.md:298]
- [x] [Review][Patch] [Medium] Make the validator require the exact contiguous acceptance-criterion identifier sets, not only per-story counts and global uniqueness. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1:77]
- [x] [Review][Patch] [High] Make integrity validation reject malformed or duplicate inventory rows and require every claimed closure artifact, not only the five approval-bound inputs. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1:153]
- [x] [Review][Patch] [Medium] Resolve the Story 2.5 scaffold-evidence contradiction: P-SAVE requires a clean generated-scaffold run while the evidence matrix marks scaffold N/A. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:61]
- [x] [Review][Patch] [High] Map Story 2.2 invalid-input and failure-atomicity risks to `S2.2-AC07`; `S2.2-AC06` only specifies duplicate FileWatcher notification coalescing. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:85]
- [x] [Review][Patch] [Medium] Bound every cancel window's `end_frame` to the move's `total_frames`; ordered but post-recovery ranges are currently accepted by the planning contract. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:41]
- [x] [Review][Patch] [Medium] Define unknown snapshot component discriminators as candidate-rejecting input before mutation; current compatibility rules cover unknown optional fields but not unknown component types. [_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md:375]

- [x] [Review][Patch] [High] Require a durable PREP-2.3 implementation commit, then repeat PREP-2.4 role approvals against that exact commit; do not treat the current dirty working tree or clean HEAD `cddba608...` as the reviewed implementation baseline. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/role-approvals.md:3]
- [x] [Review][Patch] [High] Add `.gitignore` exceptions for the PREP-2.4 story, evidence directory, Epic 2 package, checklist, UX contract, and closure reassessment so a normal commit contains the reviewed artifacts. [.gitignore:27]
- [x] [Review][Patch] [High] Remove remaining stale Story 1.3 completion prose that still says Tasks 3.2 and 10 require Godot verification. [_bmad-output/implementation-artifacts/v2-1-3-character-scene-template.md:356]
- [x] [Review][Patch] [High] Replace bundled AC ranges with an exhaustive per-story, per-risk mapping covering all eleven dimensions; each dimension must point to a specific AC and evidence ID or an explicit N/A rationale. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:65]
- [x] [Review][Patch] [High] Redesign horizontal ViewModel/codec/container slices as independently verifiable vertical outcomes, especially Stories 2.3-2.5. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:89]
- [x] [Review][Patch] [Medium] Resolve scaffold evidence applicability definitively for Stories 2.2-2.5 instead of using conditional “N/A unless distributed” wording. [_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md:70]
- [x] [Review][Patch] [High] Strengthen the PREP-2.4 validator to verify all risk dimensions, evidence columns, N/A rationales, unique AC IDs, anchors, and risk-to-AC/evidence mappings. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1:31]
- [x] [Review][Patch] [High] Make the validator parse and recompute the SHA-256 inventory and verify the five hashes bound in role approvals; text-searching for “SHA-256 inventory” is insufficient. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1:47]
- [x] [Review][Patch] [Medium] Extend the integrity inventory to cover the PREP-2.4 story, sprint status, tracking delta, and other mutable closure artifacts. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/sha256.txt:1]
- [x] [Review][Patch] [Medium] Replace the prose-only `tracking-diff.patch` with an actual unified baseline diff that can be inspected or applied and that exposes every status transition. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/tracking-diff.patch:1]
- [x] [Review][Patch] [Medium] Add OS, exact .NET SDK and Godot versions, reviewed content identity, reviewer, and acceptance state to the full regression evidence. [_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/full-suite.log:1]
- [x] [Review][Patch] [High] Reject looping an empty or zero-duration Story 2.4 recording to prevent a zero-length loop. [_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md:300]
- [x] [Review][Patch] [Medium] Define whether an entry at `relative_frame == duration` injects before terminal ownership release. [_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md:276]
- [x] [Review][Patch] [High] Make recording and training playback mutually exclusive for the same player so injected playback cannot be recursively captured or compete with capture. [_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md:258]
- [x] [Review][Defer] PREP-2.3’s `backlog -> done` sprint transition appears in the baseline diff but predates PREP-2.4 and belongs to the active PREP-2.3 workstream. [_bmad-output/implementation-artifacts/sprint-status.yaml:223] — deferred, pre-existing

## Dev Notes

### Developer Context and Guardrails

- This is governance/planning work. **Do not change runtime code, tests, scenes, JSON data, packages, or project/scaffold files.** Reading current code is required to make the stories accurate; implementation belongs to Stories 2.1-2.5.
- Existing untracked planning artifacts include the corrected Epic 2 story package, readiness gate, approved course correction, and readiness assessment. Preserve and review them in place. No UX artifact exists; the readiness assessment's UX gaps and UJ-6 frame-step contradiction are blockers to resolve or explicitly scope, not permission to invent interaction behavior.
- PREP-2.1-2.3 are prerequisites, not scope to reopen. Reuse their accepted contracts: exact lifecycle/generation ownership, deterministic isolated tests, immutable events, Data-authoritative transactions, playback-mode exclusion, and failure-atomic snapshots.
- Preserve architecture ownership: Core owns interfaces/lifecycle/snapshot/playback coordination; Data owns schema, validation, migrations and persistence; Engine owns gameplay state; UI observes through testable ViewModels; Godot EditorPlugin/Controls remain thin adapters.
- Same-frame subscriber publications remain queued for the next frame. Event ordering and lifecycle epochs are contractual; story text must name how each feature enters, exits, restores, and rejects stale work.
- Completion requires automated evidence, applicable Godot evidence, stakeholder acceptance, and consistent tracking. A green full suite alone does not close a story.
- `v2-epic-2` must remain `backlog` throughout PREP-2.4. Creating or approving plans does not start product implementation.

### Existing Files to Update

- `_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md`
  - **Current state:** corrected Stories 2.1-2.5 preserve FR-23-27 and architecture boundaries, but sizing, dependency order, concrete parameters, evidence locations, UX decisions, and approvals remain unresolved.
  - **Change:** finalize the five stories, especially an independently completable 2.4 and bounded vertical slices for oversized 2.5; link the canonical checklist and approvals.
  - **Preserve:** approved product intent and AD-12/13/15/18/19/20 constraints.
- `_bmad-output/implementation-artifacts/v2-1-3-character-scene-template.md`
  - **Current state:** status is `done`, but stale unchecked/manual-pending wording conflicts with accepted Epic 1 evidence.
  - **Change:** align current task/evidence wording and links with completed verification while preserving historical results.
- `_bmad-output/implementation-artifacts/v2-epic-1-manual-verification-guide.md`
  - **Current state:** checks are complete, but header/prerequisite/prose still contain stale pending and 603-test wording.
  - **Change:** record final completion metadata and accepted outcomes; preserve encoding, steps, and historical evidence.

- `_bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md`
  - **Current state:** actions 7-9 are open and lack accepted PREP-2.4 evidence; the readiness assessment blocks Epic 2.
  - **Change:** add exact evidence links and, only after acceptance, mark the owned planning actions done.
  - **Preserve:** Epic 1 outcome, actions 1-6, historical findings, and the rule that Epic 2 stays blocked until every gate closes.
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
  - **Current state:** PREP-2.4 and Epic 2 are backlog; the three planning actions are open.
  - **Change:** normal PREP-2.4 lifecycle updates, then exact owned action/status closure after accepted evidence.
  - **Preserve:** comments/order, all unrelated statuses, `v2-epic-1: done`, and `v2-epic-2: backlog` during this story.
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01.md`
  - **Current state:** historical assessment is `NOT READY`, with dependency-graph, UX, UJ-6, sizing, parameter, and evidence blockers.
  - **Change:** retain as input; re-run or append a new dated assessment after closure rather than silently rewriting the historical decision.

### Expected New Files

- `_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md`, the exact canonical path named by the approved course correction, including the cross-story risk matrix, required/N/A evidence layers, expected locations, execution/dependency graph, and dated PO/Architect/QA decisions.
- `_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/` containing an index, traceability audit, separate PO/Architect approvals, review history, tracking diff, and SHA-256 inventory.

### Testing and Review Requirements

- Validate links and anchors, unique story/AC identifiers, complete 5 stories × risk matrix coverage, and every AC-to-evidence trace. A small deterministic document-validation script is allowed under evidence tooling only if needed; do not add a runtime dependency.
- Review the package against the checked-in code at the recorded commit, not architectural memory. At minimum inspect the current Data transaction/persistence boundary, snapshot coordinator/contracts, playback coordinator, EventBus lifecycle/phase behavior, SceneManager cleanup, ViewModel conventions, and scaffold manifest/parity rules.
- Approval evidence must be attributable and immutable: reviewer identity/role, decision, date, exact commit or content hashes, blocking findings, and disposition.
- No new library or upgrade is authorized. The repository remains pinned to Godot.NET.Sdk 4.5.1, `net8.0`, and the checked-in test packages; dependency changes require a separate architecture decision.

### Previous Story and Git Intelligence

- PREP-2.3 is the immediate predecessor and establishes the contracts Epic 2 must consume: immutable state snapshots/events, `MoveStarted` before first `MoveFrameChanged`, Data transaction atomicity, expected versions, a shared playback-mode coordinator, and two-phase snapshot restore.
- PREP-2.2 established durable evidence conventions: exact environment/commands, deterministic seeds, bounded timeouts, accepted manifests, hashes, and evidence matching the reviewed implementation commit.
- PREP-2.1 established gate isolation: close only exact owned story/action rows and preserve both completed Epic 1 and backlog Epic 2.
- Recent commits pair implementation with focused/full regression evidence. PREP-2.4 must instead pair document changes with traceability, independent approvals, and a narrow tracking diff.
- The worktree contains active PREP-2.3 changes. Do not overwrite or reformat them while preparing or implementing PREP-2.4.

### Latest Technical Notes

- Godot 4.5 `EditorPlugin` provides editor lifecycle hooks, state handling, and `EditorUndoRedoManager`; Story 2.1 must specify cleanup on plugin disable and undoable editor mutations where appropriate. [Source: https://docs.godotengine.org/en/4.5/classes/class_editorplugin.html]
- Godot 4.5 requires .NET 8 or later for C# projects. The repository's `net8.0` target and Godot 4.5.1 pin are compatible; PREP-2.4 does not authorize upgrading to the newer Godot 4.6 support line. [Source: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html]

### Project Structure Notes

- Planning artifacts belong under `_bmad-output/planning-artifacts/`; readiness evidence belongs under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/`.
- Use stable identifiers `v2-2-1` through `v2-2-5` matching sprint status. Preserve English document output and repository-relative source citations.
- `docs/architecture.md` and `docs/source-tree-analysis.md` describe an older V1 snapshot of planned modules. Use the current Architecture Spine and checked-in source as authority when they differ.

### References

- [Source: _bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md#Next-Epic-Preparation]
- [Source: _bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md#Action-Items]
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml#development_status]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-1-runtime-and-scaffold-boundary-hardening.md]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-2-deterministic-test-infrastructure.md]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-3-data-event-and-snapshot-contracts.md]
- [Source: _bmad-output/implementation-artifacts/evidence/v2-prep-2-3/index.md]
- [Source: _bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md#PREP-24-Epic-2-Planning-and-Evidence-Review]
- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31.md]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01.md]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Adoption-Gate]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Capability-Architecture-Map]
- [Source: docs/development-guide.md#Test]

## Evidence Index (Complete During Implementation)

| AC | Required evidence | Status / link |
|---|---|---|
| AC01-AC03 | Corrected Stories 2.1-2.5, executable dependency graph, bounded vertical-slice plans | Accepted — [Epic 2 package](../planning-artifacts/epics/epic-2-move-authoring-training-suite.md) |
| AC04-AC05 | Complete 5-story risk/evidence checklist with applicability rationale and expected locations | Accepted — [risk/evidence checklist](../planning-artifacts/epic-2-risk-and-evidence-checklist.md) |
| AC06-AC07 | Story 1.3/manual/retro/sprint alignment and strengthened start-gate diff | Accepted — [tracking evidence](evidence/v2-prep-2-4/index.md#ac06-ac07) |
| AC08-AC09 | Dated PO/Architect/QA decisions, blocker disposition, and gate-scope triage record | Accepted — [role approvals](evidence/v2-prep-2-4/role-approvals.md) |
| AC10 | Evidence index/hashes and narrow retrospective/sprint tracking diff | Accepted — [closure evidence](evidence/v2-prep-2-4/index.md) |

## Dev Agent Record

### Agent Model Used

OpenAI GPT-5

### Debug Log References

- RED: `validate-prep24.ps1` initially reported 27 missing/unsatisfied planning gates, including unresolved decisions, absent approvals, Story 1.3 drift, and a weak sprint start-gate comment.
- REVIEW: the strengthened validator confirms document structure and integrity while correctly blocking closure until PREP-2.3 has a durable commit and Product Owner, System Architect, and QA reapprove that exact state.
- Regression: `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore` passed 858/858 with seed 2202; 0 failed, 0 skipped.
- Integrity: the expanded `evidence/v2-prep-2-4/sha256.txt` inventory is recomputed by the validator; superseded approval-bound hashes remain intentionally failing until reapproval.

### Implementation Plan

- Preserve FR-23-27 while converting the corrected Epic 2 package into an executable dependency graph and bounded vertical slices.
- Resolve product/UX/parameter decisions in one canonical checklist; map every risk and evidence layer per story.
- Align completed Epic 1 evidence and strengthen the sprint start gate without starting Epic 2.
- Bind independent PO/Architect/QA decisions to immutable content hashes, validate documents deterministically, and run the full regression suite.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Finalized the Epic 2 execution contract, dependency graph, vertical slices, lean UX contract, and binding Data/Combo/Recording/Snapshot parameter decisions.
- Completed all five story risk/evidence rows and fixed durable evidence locations and metadata requirements.
- Preserved the superseded Product Owner, System Architect, and QA approvals and made reapproval against a durable PREP-2.3 commit an explicit closure blocker.
- Aligned Story 1.3/manual/retrospective/sprint evidence to the accepted 747-test and Godot verification record while preserving historical milestones.
- Strengthened the Epic 2 start gate; actions 6-9 are evidenced and closed after reapproval while `v2-epic-2` remains `backlog` pending separate kickoff authorization.
- Added deterministic document validation and closure evidence; the 858-test full regression and all approval/baseline integrity gates pass.
- Resolved the final high-severity review finding: bound refreshed Product Owner, System Architect, and QA approvals to durable PREP-2.3 commit `a0c7043` and the exact planning hashes.
- Re-ran the full regression suite (858 passed, 0 failed, 0 skipped; seed 2202) and deterministic PREP-2.4 document/integrity validation before closure.
- Code review resolved 10 findings: narrowed the tracking patch, completed story evidence state, removed stale guidance, strengthened exact AC/inventory validation, and closed contract gaps for Story 2.2/2.5, cancel windows, and snapshot discriminators.

### File List

- _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md
- _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md
- _bmad-output/planning-artifacts/epic-2-ux-contract.md
- _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-prep24-closure.md
- _bmad-output/implementation-artifacts/v2-1-3-character-scene-template.md
- _bmad-output/implementation-artifacts/v2-epic-1-manual-verification-guide.md
- _bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/index.md
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/role-approvals.md
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/traceability.md
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/review-history.md
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/tracking-diff.patch
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/full-suite.log
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-4/sha256.txt
- .gitignore
- _bmad-output/implementation-artifacts/deferred-work.md

## Change Log

- 2026-08-01: Created PREP-2.4 implementation-ready planning and evidence review story.
- 2026-08-01: Implemented PREP-2.4 planning correction, evidence contract, role approvals, Epic 1 alignment, deterministic validation, and gate-safe tracking updates; moved story to review.
- 2026-08-01: Applied adversarial review fixes; reopened the story as in-progress until PREP-2.3 is durably committed and the three role approvals are repeated against that exact state.
- 2026-08-01: Addressed the final code-review finding; PREP-2.3 commit `a0c7043` and refreshed PO/Architect/QA approvals accepted, validations passed, and story moved to review.
- 2026-08-01: Addressed second-pass code review findings — 10 items resolved; story and sprint tracking moved to done while Epic 2 remains backlog.
