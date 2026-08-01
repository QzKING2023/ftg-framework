---
epic: v2-1
story: 3
story_id: 1.3
baseline_commit: 6e221fa
---

# Story 1.3: Godot Character Scene Template

Status: done

## Story

As a developer starting a new character,
I want a Godot scene template that scaffolds the correct node structure for a framework-compatible character,
so that I can drop in my sprite art and the character immediately responds to framework input, state machine, and physics without manual node setup.

## Acceptance Criteria

### AC 1: Correct Node Structure
**Given** the framework is installed in a Godot project
**When** the developer creates a new character by duplicating `character_template.tscn`
**Then** the scene contains: root Node2D with `CharacterController` script, SpriteContainer (Node2D), HurtboxContainer (Node2D) with pre-configured hurtbox Area2D children, AnimationPlayer, and framework attachment points
**And** all attachment points are `[Export]`-ed and visible in the Godot Inspector

### AC 2: No Placeholder Art
**Given** a character created from the template
**When** the developer assigns their own sprites to the SpriteContainer
**Then** the character renders correctly in gameplay
**And** no placeholder art is bundled — the sprite node starts empty

### AC 3: Framework Wiring — State Machine
**Given** a character instantiated from the template (PlayerId=1)
**When** the game runs
**Then** `CharacterController._Ready()` wires into GameLoop's services
**And** the character's state machine initializes to `Idle` for its PlayerId
**And** `StateChangedEvent` subscriptions are active for this player

### AC 4: Responds to Input
**Given** two character instances (P1 and P2) from the same template are added to the training scene
**When** the game runs and directional/button inputs are pressed (WASD + attack keys per GameLoop)
**Then** the state machine for the correct player transitions in response to input-triggered moves
**And** the CharacterController updates animation/sprite based on the current state
**And** each character operates independently — separate state stacks, separate visual states

### AC 5: Hurtbox Registration
**Given** a character instantiated from the template
**When** `_Ready()` runs
**Then** the character's hurtbox Area2D nodes are collected and exposed via a public property
**And** hurtboxes are structured for future physics engine registration (Story 1.5)

### AC 6: Template Data File
**Given** the character template is used by a new developer
**When** they open the project
**Then** `template_fighter.json` is present in `Data/` with pre-configured move definitions
**And** the JSON demonstrates the complete move schema: startup, active, recovery, hit_advantage, block_advantage, damage, knockback_profile_id, cancel_windows
**And** includes at minimum: `5A` (light), `5B` (medium), `2A` (crouch light) moves

### AC 7: Framework Attachment Points
**Given** the character template
**When** inspected in the Godot editor
**Then** the following are visible as `[Export]` properties on the root CharacterController:
- `SpriteContainer` (Node2D)
- `HurtboxContainer` (Node2D)
- `AnimationPlayer`
- `PlayerId` (int, default 1)
- `CharacterId` (string)
- `StateDebugLabel` (Label, optional — shows current state for development)

### AC 8: Two-Player Independence
**Given** two characters are instantiated from the same template with PlayerId 1 and 2
**When** both are added to the scene
**Then** each character's state machine stack is independent — pushing a state on P1 does not affect P2
**And** each character's hurtbox set is tracked separately
**And** each responds only to events matching its own PlayerId

### AC 9: dotnet test — ViewModel
**Given** `CharacterViewModel` is a pure C# class (no Godot dependency)
**When** tested under `dotnet test`
**Then** state→animation name mapping, direction-facing logic, and default-value behavior all pass
**And** all existing tests continue to pass (no regressions)

### AC 10: Godot Editor Verification
**Given** the character template is opened in the Godot editor
**When** the scene is inspected
**Then** the node tree matches the documented structure
**And** all `[Export]` properties are visible in the Inspector dock
**And** duplicating the scene and changing PlayerId/CharacterId produces a working second character

## Tasks / Subtasks

- [x] Task 1: Create `CharacterViewModel` — pure C#, testable under dotnet test (AC: 9)
  - [x] 1.1 Create `Scripts/Framework/Characters/CharacterViewModel.cs`
    - `string GetAnimationName(CharacterState state)` — maps state to animation string. `CharacterState.AttackStartup`/`AttackActive`/`AttackRecovery` are distinct enum values, so `MovePhase` is redundant — the state already encodes the phase.
    - `bool ShouldFaceRight(DirectionValue currentDirection)` — determines sprite flip
    - Default mappings: Idle→"idle", Walk→"walk", JumpStartup→"jump_startup", AttackStartup→"attack_startup", Hitstun→"hitstun", etc.
  - [x] 1.2 `#nullable enable` — all new code null-safe
  - [x] 1.3 Namespace: `FTG_Framework.Characters`

- [x] Task 2: Create `CharacterController` — Godot script for scene root (AC: 1, 3, 4, 5, 7, 8)
  - [x] 2.1 Create `Scripts/Framework/Characters/CharacterController.cs` — `public partial class CharacterController : Node2D`
  - [x] 2.2 `[Export]` properties:
    - `Node2D? SpriteContainer`
    - `Node2D? HurtboxContainer`
    - `AnimationPlayer? AnimationPlayer`
    - `int PlayerId = 1`
    - `string CharacterId = ""`
    - `Label? StateDebugLabel` (optional)
  - [x] 2.3 Private fields: `CharacterViewModel _viewModel`, `List<Area2D> _hurtboxes`, subscriptions
  - [x] 2.4 `_Ready()`:
    - Validate all [Export] references are assigned (log warning if not, continue gracefully)
    - `_viewModel = new CharacterViewModel()`
    - Get GameLoop autoload: `GetNode<GameLoop>("/root/GameLoop")`
    - Access `gameLoop.StateMachine` → `InitializePlayer(PlayerId)` (idempotent per Story 1.2 guard)
    - Subscribe: `EventBus.Instance.Subscribe<StateChangedEvent>(OnStateChanged)`
    - Collect hurtbox Area2D nodes from HurtboxContainer children → `_hurtboxes`
    - Update debug label if assigned
  - [x] 2.5 `OnStateChanged(StateChangedEvent e)`:
    - If `e.PlayerId != PlayerId`, return immediately
    - Get current state and phase from top of `e.NewStack`
    - `_viewModel.GetAnimationName(state)` → `AnimationPlayer?.Play(name)`
    - Update sprite facing via `_viewModel.ShouldFaceRight(...)` — reads from `GameLoop.InputHistory.GetDirectionalHistory(PlayerId)`
    - Update debug label text
  - [x] 2.6 `GetHurtboxes()` → `IReadOnlyList<Area2D>` — public accessor for Physics engine (Story 1.5)
  - [x] 2.7 `_ExitTree()`: unsubscribe all event handlers
  - [x] 2.8 Namespace: `FTG_Framework.Characters`

- [x] Task 3: Create `character_template.tscn` (AC: 1, 2, 7)
  - [x] 3.1 Create `Characters/character_template.tscn` in Godot editor with this node tree:
    ```
    Character (Node2D) [script=CharacterController.cs]
    ├── SpriteContainer (Node2D)
    │   └── Sprite (Sprite2D)        ← empty texture, developer replaces
    ├── HurtboxContainer (Node2D)
    │   ├── HeadHurtbox (Area2D)
    │   │   └── CollisionShape2D     ← rectangle shape, disabled by default
    │   ├── BodyHurtbox (Area2D)
    │   │   └── CollisionShape2D     ← rectangle shape, disabled by default
    │   └── LegsHurtbox (Area2D)
    │       └── CollisionShape2D     ← rectangle shape, disabled by default
    ├── AnimationPlayer
    └── StateDebugLabel (Label)      ← positioned at top of character, shows current state
    ```
  - [x] 3.2 Wire all `[Export]` references in the scene (drag nodes into Inspector slots) — verified in the completed Epic 1 manual guide and accepted by Q1625 on 2026-07-31.
  - [x] 3.3 Hurtbox Area2D nodes: set `Monitoring = false`, `Monitorable = true` (they are detected, not detectors). CollisionShape2D shapes are approximate (50×50 px rectangles) — developers resize per character.

- [x] Task 4: Create `template_fighter.json` (AC: 6)
  - [x] 4.1 Create `Scripts/Framework/Data/template_fighter.json` — JSON array of move definitions
  - [x] 4.2 Include moves: `5A` (light punch), `5B` (medium punch), `2A` (crouch light)
  - [x] 4.3 Each move demonstrates all schema fields: `move_id`, `move_name`, `startup`, `active`, `recovery`, `hit_advantage`, `block_advantage`, `damage`, `knockback_profile_id`, `cancel_windows`
  - [x] 4.4 Use realistic frame data values that sum correctly (startup + active + recovery = total frames)
  - [x] 4.5 Reference `KnockbackProfile` IDs (e.g., `"light_hit"`, `"heavy_hit"`). Create `Scripts/Framework/Data/example_knockback_profiles.json` with at minimum `"light_hit"` and `"heavy_hit"` profiles (Story 1.1 defined the data model but did not create the JSON file — this story creates it).
  - [x] 4.6 Use **snake_case** field names: `move_id`, `startup`, `active`, `recovery`, `hit_advantage`, `block_advantage`, `damage`, `knockback_profile_id`, `cancel_windows`. The `MoveDataLoader` uses `JsonNamingPolicy.SnakeCaseLower` — camelCase fields will fail to deserialize.

- [x] Task 4b: Extend `MoveDefinition` (AC: 6)
  - [x] 4b.1 Modify `Scripts/Framework/Data/MoveDefinition.cs` — add `string? KnockbackProfileId` and `string? MoveName` properties (`init`-only, per AD-6 data immutability). Both are optional (nullable) for backward compatibility with existing move JSON.
  - [x] 4b.2 Verify `example_moves.json` still loads correctly after the change (new properties should be null for existing entries, not cause deserialization errors).
  - [x] 4b.3 Run `dotnet test` — all existing tests pass (MoveDefinition fields are additive).

- [x] Task 5: Update `GameLoop` for character support (AC: 3, 4)
  - [x] 5.1 Add public properties:
    - `public IStateMachine? StateMachine => _stateMachine;`
    - `public IInputHistory? InputHistory => _inputHistory;` (needed by CharacterController for direction-facing logic)
  - [x] 5.2 Remove or guard hardcoded `InitializePlayer(1)` / `InitializePlayer(2)` — these are now called by `CharacterController._Ready()`. The StateMachine's guard (added in Story 1.2 review) prevents double-init, so keeping them is safe but unnecessary. **Decision: keep them** as a safety net for headless/test scenarios where no CharacterController exists. `CharacterController` will call `InitializePlayer` which no-ops if already initialized.
  - [x] 5.3 Current `_Process` routes ALL input to PlayerId 1 only (hardcoded in `RecordInput(1, ...)`, `ChargeTracker.Update(1, ...)`, `TryMatch(1)`, `Resolve(..., 1, ...)`). For this story, P2 key bindings are not yet implemented — that is deferred to Story 1.5 (Physics) or a future input-routing story. **Document this limitation** in the story dev notes.

- [x] Task 6: Update `TrainingScene` to instantiate characters (AC: 4, 8)
  - [x] 6.1 In `Enter()`, after creating UI panels:
    - Load template: `ResourceLoader.Load<PackedScene>("res://Characters/character_template.tscn")`
    - Instantiate P1: `var p1 = scene.Instantiate<CharacterController>(); p1.PlayerId = 1; AddChild(p1);`
    - Instantiate P2: `var p2 = scene.Instantiate<CharacterController>(); p2.PlayerId = 2; AddChild(p2);`
    - Position them at reasonable spots (e.g., P1 at (-200, 0), P2 at (200, 0))
  - [x] 6.2 Store references for cleanup in `Exit()`

- [x] Task 7: Update scaffolding (AC: 1)
  - [x] 7.1 Add `Scripts/Framework/Characters` to `Scaffold/framework-source-dirs.txt`
  - [x] 7.2 Add `Characters/` directory to the CLI template copy (note: `framework-source-dirs.txt` handles code; `.tscn` files need explicit handling in `ProjectScaffolder.cs`)
  - [x] 7.3 Add `Scripts/Framework/Data/template_fighter.json` to template (already covered by `Scripts/Framework/Data` in source dirs)
  - [x] 7.4 Update `Scaffold/ftg-cli/ProjectScaffolder.cs` to copy `Characters/` directory from framework root to scaffolded project
  - [x] 7.5 Update `Scaffold/ftg-project-template/project.godot` — no changes needed (main scene is still `main.tscn`; GameLoop autoload handles character instantiation)

- [x] Task 8: Create unit tests (AC: 9)
  - [x] 8.1 Create `Tests/FTG_Framework.Tests/Characters/CharacterViewModelTests.cs`:
    - `GetAnimationName_Idle_ReturnsIdle` — default state → "idle"
    - `GetAnimationName_Walk_ReturnsWalk` — walk state
    - `GetAnimationName_JumpStartup_ReturnsJumpStartup` — jump startup
    - `GetAnimationName_AttackStartup_ReturnsAnimationName` — attack states
    - `GetAnimationName_Hitstun_ReturnsHitstun` — hitstun state
    - `GetAnimationName_UnknownState_ReturnsIdle` — fallback for unmapped states
    - `ShouldFaceRight_Forward_ReturnsTrue` — forward directions → face right
    - `ShouldFaceRight_Back_ReturnsFalse` — back directions → face left
    - `ShouldFaceRight_NeutralVertical_ReturnsNull` — neutral/vertical → no change
    - `GetAnimationName_AllStates_HaveMapping` — verify all 14 CharacterState enum values have a mapping
  - [x] 8.2 No Godot dependency in CharacterViewModelTests — runs under `dotnet test`

- [x] Task 9: Run full test suite (AC: 9)
  - [x] 9.1 `dotnet test` — all new tests pass, all existing tests pass (603/603)
  - [x] 9.2 Verify no regressions in StateMachineTests, ComboStateTrackerTests, etc.
  - [x] 9.3 Verify `ScaffoldedProject_BuildsSuccessfully` passes (fixed missing `Engine/StateMachine` in source dirs)

- [x] Task 10: Godot editor manual verification (AC: 10) — completed in the Epic 1 manual guide and accepted by Q1625 on 2026-07-31.
  - [x] 10.1 Open `character_template.tscn` in Godot editor — node tree verified
  - [x] 10.2 Verify all `[Export]` properties visible in Inspector on root node
  - [x] 10.3 Press Play — no errors in console
  - [x] 10.4 Verify two character instances appear in training scene at correct positions
  - [x] 10.5 Verify state debug labels show "Idle" on startup
  - [x] 10.6 Press attack keys (U, I) — state transitions verified
  - [x] 10.7 Press H key (test hit) — defender Hitstun verified
  - [x] 10.8 Exit — no shutdown errors

## Dev Notes

### Architecture Context

**AD-11 — Peer Model.** CharacterController subscribes to `StateChangedEvent` via EventBus. It is an observer, not a controller — it reads state and updates visuals. It does NOT push states directly (StateMachine owns transitions per AD-11 peer model).

**AD-12 Frame Order.** `StateChangedEvent` dispatches at Phase 5 (after Physics, before Combo). CharacterController's `OnStateChanged` handler runs during Phase 5 dispatch. Any visual updates are synchronous within the same frame.

**AD-16 — Read-Only Physics API.** CharacterController exposes `GetHurtboxes()` for Physics engine consumption. This is a read-only query — Physics reads hurtbox references, CharacterController never writes physics data.

**NFR-3 — ViewModel-first UI.** `CharacterViewModel` is pure C# testable under `dotnet test`. `CharacterController` is a Godot `Node2D` partial class (NOT a `Control`) so the "no logic in _Process/_Ready of Controls" rule does not strictly apply — but we extract testable logic into the ViewModel anyway. The Controller delegates state→visual mapping to the ViewModel.

**Scene Template Pattern.** Godot does not have "scene templates" like Unity prefabs. The framework provides a `.tscn` file with the correct node structure. Developers duplicate this file for each new character. This is the standard Godot workflow (duplicate `.tscn` → customize). Alternative: "New Inherited Scene" from Godot's FileSystem dock.

**Attachment Points.** Framework integration points are `[Export]` properties on the CharacterController. This allows:
- Visual setup (drag nodes in the Godot editor)
- Discoverability (visible in Inspector)
- Loose coupling (the CharacterController doesn't assume specific child paths; they're assigned in the editor)

### Key Design Decisions

**Root node is Node2D, not CharacterBody2D.** We do NOT use Godot's built-in `CharacterBody2D` for the template root. Reason: the framework's Physics engine (AD-10) owns all collision and movement computation — using Godot's physics body would create a competing physics authority. The root is a plain `Node2D`; position is set by the framework's Physics engine via `GlobalPosition`. If a developer needs Godot physics for non-framework purposes, they can change the root type — the framework only needs the attachment points.

**StateMachine.InitializePlayer is idempotent.** Story 1.2's review added a guard — calling `InitializePlayer` on an already-initialized player logs an error and no-ops. GameLoop currently calls it for both players in `_Ready()`. CharacterController also calls it in `_Ready()`. Since GameLoop runs first (autoload), the CharacterController's call will no-op. This is intentional: in headless/test scenarios without character scenes, GameLoop's init provides a safety net.

**Input is P1-only for now.** GameLoop._Process() hardcodes `PlayerId = 1` for all input recording, charge tracking, buffer matching, and priority resolution. P2 has no input bindings. This is acceptable for this story — the template demonstrates character instantiation and state response. P2 input routing is deferred to when Physics (Story 1.5) or a dedicated input-routing story adds it. The AC "responds to directional and button inputs per the V1 input system" is satisfied by P1's input flowing through the existing pipeline.

**Hurtboxes are pre-configured but inert.** The hurtbox Area2D nodes are disabled (`Monitoring = false`) pending Physics engine integration in Story 1.5. The CharacterController collects and exposes them, but no collision detection runs. This is forward-looking infrastructure — the node structure is correct from day one.

**Sprite is empty.** Per the AC, no placeholder art. The Sprite2D node has no texture assigned. When the developer assigns a texture, the framework renders it. If no texture is assigned, the character is invisible but functional (state machine, input response all work).

**AnimationPlayer has no default animations.** Creating default "idle", "walk", etc. animations requires art assets. Instead, `CharacterViewModel.GetAnimationName()` returns string names; the CharacterController calls `AnimationPlayer.Play(name)`. If no animation exists with that name, Godot's AnimationPlayer silently no-ops (no crash). Developers add their own animations with the expected names.

**`template_fighter.json` uses existing move schema — with one extension.** The format matches `example_moves.json` (loaded by `MoveDataLoader.LoadFromFile`). Field naming is **snake_case** (`move_id`, `startup`, `hit_advantage`, `knockback_profile_id`) because `MoveDataLoader` uses `JsonNamingPolicy.SnakeCaseLower`. Two new fields (`knockback_profile_id`, `move_name`) are added to `MoveDefinition` in this story (Task 4b).

**Architecture directory deviation.** The architecture Capability→Architecture Map places FR-22 under `Scripts/Editor/`, but runtime character components (scene `.tscn`, `CharacterController.cs`, `CharacterViewModel.cs`) belong under `Scripts/Framework/Characters/`. The Editor mapping was for *editor tooling* (scene template creation wizards), which is deferred. The `Characters/` directory is a necessary extension of the structural seed — it houses the framework's runtime character integration layer, analogous to how `UI/Training/` houses training mode UI.

### Previous Story Intelligence (Story 1.2)

From `v2-1-2-state-machine-physics-association.md` (Status: done, 585/586 tests passing):

- **StateMachine is ready.** `IStateMachine` exposes `GetCurrentState`, `GetStack`, `PushState`, `PopState`, `ReplaceState`, `GetEffectivePhysicsProfile`, `RegisterStateProfile`, `InitializePlayer`. CharacterController uses `InitializePlayer` and subscribes to `StateChangedEvent`.
- **StateChangedEvent carries OldStack and NewStack.** CharacterController reads top of `NewStack` to determine current visual state.
- **StateMachine subscribes to 6 events** (AD-11). CharacterController does NOT need to subscribe to these — it only subscribes to `StateChangedEvent`.
- **Transition guard table is hardcoded but extensible.** The default guards are in StateMachine. CharacterController does not modify them.
- **InitializePlayer guard added in review.** Calling `InitializePlayer` on an existing player logs error and no-ops. GameLoop calls it first; CharacterController's call is a safe no-op.
- **Review Findings to be aware of:**
  - `ParticipatesInHitstop` uses `bool?` with `null` sentinel (F5 decision) — not relevant to CharacterController
  - `GetEffectivePhysicsProfile` merge is top-down first-wins (F3 fix) — not relevant to CharacterController
  - `StateChangedEvent` uses `CharacterState[]` arrays (fresh copies from `stack.ToArray()`) — safe to read, don't mutate

### Previous Story Intelligence (Story 1.1)

From `v2-1-1-physics-knockback-data-models.md` (Status: done):

- **`PhysicsResponseProfile` is immutable** (`init`-only properties). Not directly used by CharacterController but referenced by template_fighter.json's `knockback_profile_id` field.
- **`IDataStore.GetPhysicsResponseProfile(id)` returns null for unknown IDs.** template_fighter.json must reference profile IDs that exist in `example_knockback_profiles.json`.
- **`IDataStore.GetKnockbackProfile(id)` same pattern.** Move JSON must reference valid profile IDs.

### Previous Story Intelligence (Story 1.0)

From `v2-1-0-snapshot-helper-frame-order.md` (Status: done):

- **AD-12 phase order is live.** Phase 5 (State Machine) dispatches after Physics (Phase 4). CharacterController's OnStateChanged runs in Phase 5.
- **`Snapshot.Of<T>` is a no-op marker.** Not used by CharacterController directly; relevant for Physics (Story 1.5).

### Existing Code That Must Not Break

- **`GameLoop._Ready()`** — module registration order must be preserved. New public properties are additive. Do NOT change the initialization sequence.
- **`GameLoop._Process()`** — input processing pipeline untouched by this story. Character instantiation happens in TrainingScene, not GameLoop.
- **`StateMachine`** — no changes. All state transitions continue to work as before.
- **`TrainingScene`** — adding character instantiation in `Enter()`. Position it AFTER UI panel creation to keep UI on top of characters in the scene tree draw order.
- **`SceneManager`** — no changes. Character scenes are instantiated by TrainingScene, managed by SceneManager's Exit cleanup (QueueFree on scene root).
- **Existing tests** — all must continue to pass. New tests in `Characters/` folder, no modifications to existing test files.
- **`dotnet test`** — `CharacterViewModel` is pure C#, no Godot dependency. `CharacterController` is NOT unit-testable (depends on Godot Node) — this is expected for a scene template.
- **CLI scaffold** — adding `Characters/` directory must not break existing `ScaffoldedProject_BuildsSuccessfully` test.

### Testing Standards

- Test framework: **xUnit** (matching existing `FTG_Framework.Tests`)
- Test namespace: `FTG_Framework.Tests.Characters`
- Test file location: `Tests/FTG_Framework.Tests/Characters/`
- `CharacterViewModelTests` — pure C#, runs under `dotnet test`
- `CharacterController` — manual Godot editor verification only (Task 10)
- No EventBusTestHelper needed (CharacterViewModel doesn't publish or subscribe to events)

### Files to Create

| File | Purpose |
|------|---------|
| `Scripts/Framework/Characters/CharacterViewModel.cs` | Pure C# state→visual mapping logic |
| `Scripts/Framework/Characters/CharacterController.cs` | Godot Node2D script — bridges scene to framework |
| `Characters/character_template.tscn` | Godot scene template |
| `Scripts/Framework/Data/template_fighter.json` | Reference move data demonstrating JSON schema |
| `Scripts/Framework/Data/example_knockback_profiles.json` | Knockback profile data referenced by template moves (Story 1.1 created the data model but not the JSON) |
| `Tests/FTG_Framework.Tests/Characters/CharacterViewModelTests.cs` | Unit tests for ViewModel |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Framework/Core/GameLoop.cs` | Add `public IStateMachine? StateMachine` and `public IInputHistory? InputHistory` properties |
| `Scripts/Framework/Data/MoveDefinition.cs` | Add `KnockbackProfileId` and `MoveName` properties (init-only, nullable) |
| `Scripts/Framework/Scenes/TrainingScene.cs` | Instantiate two character instances in `Enter()` |
| `Scaffold/framework-source-dirs.txt` | Add `Scripts/Framework/Characters` |
| `Scaffold/ftg-cli/ProjectScaffolder.cs` | Copy `Characters/` directory to scaffolded project |

### References

- Architecture: `_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md` — AD-11 (Peer Model), AD-12 (Frame Order), AD-16 (Read-Only Physics API), Structural Seed (directory tree)
- Epics: `_bmad-output/planning-artifacts/epics.md` — Story 1.3 ACs, FR-22
- PRD: `_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-26/prd.md` — §4.3 FR-22
- SPEC: `_bmad-output/specs/spec-ftg-framework/SPEC.md` — CAP-3 Character Construction
- Previous story (1.2): `_bmad-output/implementation-artifacts/v2-1-2-state-machine-physics-association.md`
- Previous story (1.1): `_bmad-output/implementation-artifacts/v2-1-1-physics-knockback-data-models.md`
- Previous story (1.0): `_bmad-output/implementation-artifacts/v2-1-0-snapshot-helper-frame-order.md`
- Story 3.5 (project template): `_bmad-output/implementation-artifacts/v2-3-5-project-template-polish.md`
- Existing code: `Scripts/Framework/Core/GameLoop.cs`, `Scripts/Framework/Core/IStateMachine.cs`, `Scripts/Framework/Scenes/TrainingScene.cs`, `Scripts/Framework/Data/CharacterDefinition.cs`
- Scaffold: `Scaffold/ftg-cli/ProjectScaffolder.cs`, `Scaffold/framework-source-dirs.txt`, `Scaffold/ftg-project-template/`
- Test patterns: `Tests/FTG_Framework.Tests/` directory structure

### Known Limitations (Not in Scope)

- **P2 input routing.** GameLoop only processes P1 keyboard input. P2 has no key bindings. This is deferred.
- **Physics collision.** Hurtbox Area2D nodes are inert — Physics engine is Story 1.5. This story only creates the node structure and accessor.
- **Animation creation.** The template has no pre-made animations. Developers create their own with the expected names.
- **Movement system.** Walking/jumping as player-driven continuous movement is not in the template. The `Walk` and `JumpStartup` states exist in the StateMachine, and `CharacterViewModel` maps them to animation names, but the actual position-updating logic is deferred to Physics (Story 1.5/1.6).
- **Runtime character swapping.** The template supports instantiation at scene Enter(); changing characters mid-match is not supported.

## Dev Agent Record

### Agent Model Used

Claude (via bmad-dev-story workflow)

### Debug Log References

- CS0103: `Enum` not found in CharacterController.cs — fixed by adding `using System;`
- CS8632: Nullable warnings in MoveDefinition.cs — fixed by adding `#nullable enable`
- `ScaffoldedProject_BuildsSuccessfully` failed because `Scripts/Framework/Engine/StateMachine` was missing from `framework-source-dirs.txt` (Story 1.2 omission) — fixed

### Completion Notes List

- Task 1: Created `CharacterViewModel.cs` — pure C# state→animation mapping and direction-facing logic. 10 tests.
- Task 2: Created `CharacterController.cs` — Godot Node2D partial class with 6 [Export] properties, EventBus subscription to StateChangedEvent, hurtbox collection, sprite facing via InputHistory.
- Task 3: Created `character_template.tscn` — Godot 4 text scene format with correct node hierarchy (Node2D root → SpriteContainer/Sprite, HurtboxContainer/3xArea2D, AnimationPlayer, StateDebugLabel). Subtask 3.2 Export references were verified in Godot and accepted by Q1625 on 2026-07-31.
- Task 4: Created `template_fighter.json` (3 moves: 5A, 5B, 2A) and `example_knockback_profiles.json` (4 profiles: light_hit, medium_hit, heavy_hit, default). Both use snake_case per MoveDataLoader convention.
- Task 4b: Extended `MoveDefinition.cs` with `KnockbackProfileId` and `MoveName` (init-only, nullable for backward compat).
- Task 5: Added `public IStateMachine? StateMachine` and `public IInputHistory? InputHistory` to GameLoop.
- Task 6: Added `InstantiateCharacters()` to TrainingScene.Enter() — loads character_template.tscn and creates P1+P2 at positions derived from the complete visible rectangle; supported viewports are at least 500×120 and retain exact 400-pixel spacing.
- Task 7: Updated `framework-source-dirs.txt` (added Characters, StateMachine), updated `ProjectScaffolder.cs` (copy Characters/ directory).
- Task 8: Created `CharacterViewModelTests.cs` — 10 xUnit tests covering animation mapping and facing logic.
- Task 9: Full test suite — 603/603 pass (0 regressions). Fixed pre-existing ScaffoldedProject_BuildsSuccessfully failure.
- Task 10: Godot editor verification completed and accepted by Q1625 on 2026-07-31; see the completed Epic 1 manual guide.

### File List

**Created:**
- `Scripts/Framework/Characters/CharacterViewModel.cs`
- `Scripts/Framework/Characters/CharacterController.cs`
- `Characters/character_template.tscn`
- `Scripts/Framework/Data/template_fighter.json`
- `Scripts/Framework/Data/example_knockback_profiles.json`
- `Tests/FTG_Framework.Tests/Characters/CharacterViewModelTests.cs`
- `Tests/FTG_Framework.Tests/Scenes/TrainingSceneTests.cs`

**Modified:**
- `Scripts/Framework/Core/GameLoop.cs` — Added public StateMachine and InputHistory properties
- `Scripts/Framework/Data/MoveDefinition.cs` — Added KnockbackProfileId and MoveName properties
- `Scripts/Framework/Scenes/TrainingScene.cs` — Added character instantiation in Enter()
- `Scaffold/framework-source-dirs.txt` — Added Characters and StateMachine directories
- `Scaffold/ftg-cli/ProjectScaffolder.cs` — Added Characters/ directory copy step

### Change Log

- 2026-07-30: Implemented Story 1.3 — character scene template with CharacterController, CharacterViewModel, template_fighter.json, knockback profiles, scaffolding updates. 603/603 tests passed; Tasks 3.2 and 10 were pending Godot verification at this historical milestone.
- 2026-08-01: PREP-2.4 alignment recorded the later completed Godot verification and Q1625 acceptance without changing the historical 2026-07-30 milestone.
- 2026-07-30: Fixed manual verification 2.4 visibility failure — character spawns now derive from the complete visible rectangle instead of off-screen coordinates. Added standard, minimum-size, nonzero-origin, non-finite, label-contract, and unsupported-size regression coverage; updated guide step 2.4. 617/617 tests pass. The user confirmed `[P1] Idle` and `[P2] Idle` are clearly visible; the guide's unsupported “blue” wording was corrected because color follows the current theme.
