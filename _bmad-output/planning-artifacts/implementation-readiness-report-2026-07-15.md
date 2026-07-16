---
stepsCompleted: [1]
filesIncluded:
  prd: '_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md'
  architecture: '_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md'
  epics: '_bmad-output/planning-artifacts/epics.md'
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-15
**Project:** ftg-framework

## Document Inventory

| Type | Path | Status |
|------|------|--------|
| PRD | `_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md` | final |
| Architecture | `_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md` | final |
| Epics & Stories | `_bmad-output/planning-artifacts/epics.md` | complete |
| UX Design | N/A | not present |

---

## PRD Analysis

### Functional Requirements

| FR | Description | User Journey |
|----|-------------|--------------|
| FR-1 | Dual-track input storage — directional and button histories as independent, frame-stamped sequences | UJ-2 |
| FR-2 | Directional input leniency — per-move configurable accepted sequences, order constraint respected | UJ-2 |
| FR-3 | Input buffer — configurable global buffer (default 6f), holds button presses in memory | UJ-2 |
| FR-4 | Charge retention — global retention window (default 5f), per-move min charge duration | UJ-2 |
| FR-5 | Input priority resolution — default Super > Special > Normal, developer-overridable | UJ-2 |
| FR-6 | Move frame data registration — startup/active/recovery/advantage/cancel windows/damage | UJ-1 |
| FR-7 | Cancel window tracking — per-move windows with start/end frame and target category | UJ-3 |
| FR-8 | Frame data panel — real-time UI component showing current move timing and state | UJ-1 |
| FR-9 | Frame advantage display — real-time hit/block advantage countdown | UJ-1 |
| FR-10 | Historical input log — scrollable frame-stamped input record from FR-1 storage | UJ-1 |
| FR-11 | Frame-by-frame playback — pause/step forward/step backward with hitbox overlay | UJ-1 |
| FR-12 | Gatling table registration — per-character cancel route definitions, queryable at runtime | UJ-3 |
| FR-13 | Cancel window consumption — valid input during window interrupts current move, single-use | UJ-3 |
| FR-14 | Combo chain uniqueness — default no-repeat rule, per-move "chain-repeatable" exception | UJ-3 |
| FR-15 | Combo state tracking — combo.active, hit_count, on_combo_hit hook point | UJ-3 |

Total: 15 FRs

### Non-Functional Requirements

| NFR | Description | Source |
|-----|-------------|--------|
| NFR-1 | Frame-by-frame playback must accommodate future replay system without desync | FR-11, AD-4 |
| NFR-2 | Fail-fast error handling — invalid startup data throws; gameplay errors log warnings | AD-8 |
| NFR-3 | Dependency direction enforcement — UI → Engine → Data → Input, no circular deps | AD-1 |
| NFR-4 | Data immutability — Data layer objects use init-only properties | AD-6 |

Total: 4 NFRs

### Additional Requirements

- GameLoop autoload (AD-7): single Godot Node bridging `_Process` to `EventBus.ProcessFrame()`, zero game logic
- EventBus singleton (AD-3, AD-5): sealed class, 12 event types, fixed frame processing order
- JSON data loading (AD-6): System.Text.Json, Data layer owns loaded instances, Engine queries via IDataStore
- Directory layout (AD-2): `Scripts/Framework/{Core,Input,Data,Engine/FrameData,Engine/Combo,UI/Training}/`
- Module initialization: concrete classes internal, public APIs via Core interfaces, constructor injection

### PRD Completeness Assessment

---

## Epic Coverage Validation

### Coverage Matrix

| FR | PRD Requirement | Epic Coverage | Status |
|----|----------------|---------------|--------|
| FR-1 | Dual-track input storage | Epic 1, Story 1.3 | ✓ Covered |
| FR-2 | Directional input leniency | Epic 1, Story 1.4 | ✓ Covered |
| FR-3 | Input buffer | Epic 1, Story 1.5 | ✓ Covered |
| FR-4 | Charge retention | Epic 1, Story 1.6 | ✓ Covered |
| FR-5 | Input priority resolution | Epic 1, Story 1.7 | ✓ Covered |
| FR-6 | Move frame data registration | Epic 2, Story 2.1 | ✓ Covered |
| FR-7 | Cancel window tracking | Epic 2, Story 2.2 | ✓ Covered |
| FR-8 | Frame data panel UI | Epic 2, Story 2.3 | ✓ Covered |
| FR-9 | Frame advantage display | Epic 2, Story 2.4 | ✓ Covered |
| FR-10 | Historical input log | Epic 2, Story 2.5 | ✓ Covered |
| FR-11 | Frame-by-frame playback | Epic 2, Story 2.6 | ✓ Covered |
| FR-12 | Gatling table registration | Epic 3, Story 3.1 | ✓ Covered |
| FR-13 | Cancel window consumption | Epic 3, Story 3.2 | ✓ Covered |
| FR-14 | Combo chain uniqueness | Epic 3, Story 3.3 | ✓ Covered |
| FR-15 | Combo state tracking | Epic 3, Story 3.4 | ✓ Covered |

### Coverage Statistics

- Total PRD FRs: 15
- FRs covered in epics: 15
- Coverage percentage: **100%**
- Missing FRs: **0**

### Missing Requirements

None. All 15 FRs have traceable implementation paths through the 3-epic, 17-story breakdown.

---

## UX Alignment Assessment

### UX Document Status

**Not Found.** No UX design document exists in the planning artifacts. The epics.md confirms: "No UX design document exists for this project."

### UX-Implied FRs

The following PRD requirements describe UI components, raising implicit UX needs:

| FR | UI Component | Addressed In |
|----|-------------|-------------|
| FR-8 | Frame data panel | Story 2.3 — customizable position/size/color/font |
| FR-9 | Frame advantage display | Story 2.4 — real-time countdown display |
| FR-10 | Historical input log | Story 2.5 — scrollable input log with P1/P2 toggle |
| FR-11 | Frame-by-frame playback | Story 2.6 — pause/step controls + hitbox overlay |

### Alignment Assessment

**PASS.** Architecture layer `UI/Training/` (AD-2, AD-3) is explicitly scoped for these components. Each UI Story defines visual behavior in acceptance criteria. The Architecture's AD-3 rule (UI as read-only EventBus subscriber) prevents UI from mutating engine state. The PRD assumption A-1 bounds V1 theming scope to position/size/color/font.

### Warnings

None. While a standalone UX document would add value for visual design decisions, the PRD + Architecture + Story ACs collectively provide sufficient specification for V1 implementation.

---

## Epic Quality Review

### Epic 1: Core Framework & Input System

**User Value:** ✓ Developer can install the framework, configure input leniency per move, and execute correct moves from player inputs.

**Independence:** ✓ Stands alone. No dependency on Epic 2 or 3.

| Story | User Value | Forward Dep? | AC Quality |
|-------|-----------|-------------|------------|
| 1.1 — Project scaffold & Core | ✓ Enabler: directory layout, EventBus, GameLoop | None | Given/When/Then, specific |
| 1.2 — Data layer & JSON loading | ✓ Developer defines moves as JSON | None (only 1.1) | Complete with error cases |
| 1.3 — Dual-track input storage | ✓ FR-1: independent input histories | None | Configurable capacity, eviction |
| 1.4 — Directional input leniency | ✓ FR-2: per-move leniency config | None | Multiple sequence variants, order constraint |
| 1.5 — Input buffer | ✓ FR-3: buffered button presses | None | Default 6f, disable at 0 |
| 1.6 — Charge retention | ✓ FR-4: charge state preserved | None | Per-move min duration, per-direction tracking |
| 1.7 — Input priority resolution | ✓ FR-5: deterministic move selection | None | Default scheme + custom resolver |

**Assessment:** PASS. 7 stories, all user-facing capabilities. Story 1.1 is a justified enabler — EventBus and directory layout must exist before any module can be built.

### Epic 2: Frame Data Engine & Training Mode

**User Value:** ✓ Developer opens training mode and sees real-time frame data, advantage display, input log, and frame-by-frame playback.

**Independence:** ✓ Builds on Epic 1 outputs only. Does not need Epic 3.

| Story | User Value | Forward Dep? | AC Quality |
|-------|-----------|-------------|------------|
| 2.1 — Move frame data registration | ✓ FR-6: authoritative move definitions | None (only Epic 1) | Phase transitions, per-player timelines |
| 2.2 — Cancel window tracking | ✓ FR-7: window signals per category | None (only 2.1) | Multi-window, independent tracking |
| 2.3 — Frame data panel UI | ✓ FR-8: real-time move info display | None (only 2.1) | Customizable appearance, per-player toggle |
| 2.4 — Frame advantage display | ✓ FR-9: hit/block advantage countdown | None (only 2.1) | Decrement per frame, reset on new hit |
| 2.5 — Historical input log | ✓ FR-10: scrollable input history | None (reads FR-1 storage) | Frame-stamped, P1/P2 toggle |
| 2.6 — Frame-by-frame playback | ✓ FR-11: pause/step/rewind + overlay | None (only Epic 1+2) | AD-4 order preserved, read-only overlay |

**Assessment:** PASS. 6 stories. Stories 2.3-2.5 could run in parallel (all UI components reading from shared subscriptions) — sequential ordering is a conservative choice, not an error.

### Epic 3: Combo System API

**User Value:** ✓ Developer registers Gatling tables, combos work end-to-end with cancel validation and state tracking.

**Independence:** ✓ Builds on Epic 1+2 (cancel windows, input). Delivers complete combo infrastructure.

| Story | User Value | Forward Dep? | AC Quality |
|-------|-----------|-------------|------------|
| 3.1 — Gatling table registration | ✓ FR-12: per-character cancel routes | None (only Epic 1+2) | JSON loading, runtime swap, empty table fallback |
| 3.2 — Cancel window consumption | ✓ FR-13: input triggers move cancel | None (only 3.1) | Multi-window parallel, consumed-on-use |
| 3.3 — Chain uniqueness enforcement | ✓ FR-14: no-repeat rule + exceptions | None (only 3.2) | chain_repeatable flag, per-character tracking |
| 3.4 — Combo state tracking | ✓ FR-15: combo.active + hit_count + hook | None (only 3.3) | on_combo_hit callback, blocked hits excluded |

**Assessment:** PASS. 4 stories, tight logical chain with no forward dependencies.

### Global Best Practices Checklist

| Check | Result |
|-------|--------|
| Epics deliver user value (not technical milestones) | ✓ All 3 epics describe user outcomes |
| Epic independence (N doesn't require N+1) | ✓ Verified for all 3 epics |
| No forward dependencies within epics | ✓ All stories build only on prior stories |
| Stories appropriately sized (single dev session) | ✓ Each targets one FR with bounded scope |
| Entities created when needed (not upfront) | ✓ Data layer in 1.2, GatlingTable in 3.1 |
| Clear Given/When/Then acceptance criteria | ✓ All 17 stories |
| FR traceability maintained | ✓ Coverage matrix confirms 100% |
| Architecture compliance | ✓ Layer boundaries, EventBus, AD-4 frame order all respected |

### Findings

- 🟡 **Minor:** Story 1.1 defines all Core interfaces (IInputHistory, IFrameDataEngine, IDataStore) upfront — a deliberate architectural choice per AD-3, not a defect. Interfaces in Core are contracts between layers; all were defined in the Architecture Spine before stories were written.

**Assessment: NO CRITICAL OR MAJOR VIOLATIONS.** All 17 stories pass epic quality review standards.

---

## Final Assessment

### Overall Readiness Status

**READY** — All artifacts are complete, aligned, and traceable. No critical or major issues found.

### Summary of Findings

| Step | Check | Result |
|------|-------|--------|
| 1. Document Discovery | All required docs present | PASS |
| 2. PRD Analysis | 15 FRs, 4 NFRs extracted | PASS |
| 3. Epic Coverage | 100% FR coverage (15/15) | PASS |
| 4. UX Alignment | No UX doc; UI FRs adequate in PRD+Stories | PASS (with note) |
| 5. Epic Quality | 3 epics, 17 stories validated | PASS |

### Critical Issues Requiring Immediate Action

**None.** No blocking issues found.

### Recommendations

1. **Proceed to Sprint Planning (`bmad-sprint-planning`)** — All 17 stories are implementation-ready with clear acceptance criteria.
2. **Address PRD open questions before post-V1** — 3 open questions (per-move buffer overrides, hitbox system priority, UI theming depth) are documented and non-blocking for V1, but should be resolved before scoping V1.1.
3. **Resolve PRD assumptions during implementation** — A-1 (theming scope) and A-2 (deterministic rewind) are tagged for validation. Story 2.3 and Story 2.6 will exercise both assumptions respectively.
4. **Consider a lightweight UX sketch** — While Stories 2.3-2.6 specify visual behavior in ACs, a quick wireframe or layout sketch for the training mode panel arrangement would reduce implementation ambiguity.

### Report Summary

- **Issues found:** 0 critical, 0 major, 1 minor (noted but non-blocking)
- **FR coverage:** 15/15 (100%)
- **Story count:** 17 across 3 epics
- **Architecture alignment:** All 8 ADs represented in story ACs
- **Verdict:** Ready for Sprint Planning and Phase 4 implementation.

---

*Assessment completed 2026-07-15.*

**Status: COMPLETE.** All 15 FRs have testable consequences defined. 4 NFRs are derived from explicit PRD constraints and Architecture decisions. 3 open questions are documented and non-blocking for V1. 2 assumptions indexed (A-1 theming scope, A-2 deterministic rewind). MVP scope is clearly bounded with explicit post-V1 deferred items.
