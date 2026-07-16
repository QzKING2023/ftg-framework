---
stepsCompleted: [1, 2, 3]
inputDocuments:
  - '_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md'
---

# FTG Framework - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for FTG Framework, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: Dual-track input storage — Framework stores directional input history and button press history as two independent, non-interfering, frame-stamped sequences. Default capacity 600 frames (~10s), configurable. Queryable by all subsystems.

FR-2: Directional input leniency — Developer defines accepted directional input sequences per special/super move. Framework matches player directional history against these sequences, honoring per-move leniency rules. Order constraint respected, intervening inputs tolerated.

FR-3: Input buffer — Configurable global buffer window holds button presses in memory. Default 6f. If valid move input completes within buffer, move triggers on next available frame. Buffer set to 0 disables buffering.

FR-4: Charge retention — Global charge retention window (default 5f) preserves charge state after direction release. Per-move minimum charge duration configurable. Charge states tracked independently per player and per direction.

FR-5: Input priority resolution — When multiple moves match simultaneously, one move selected. Default: Super > Special > Normal; within category, longer sequence wins. Developer can override with custom resolver.

FR-6: Move frame data registration — Developer registers move with startup, active, recovery, hit advantage, block advantage, cancel windows, damage. Framework drives gameplay timing from this data. Runtime changes take effect on next execution.

FR-7: Cancel window tracking — Framework tracks cancel windows per move (start frame, end frame, target category). Signals cancel-availability. Multiple cancel windows per move supported, targeting different categories.

FR-8: Frame data panel — Built-in UI component displays current move's frame data in real time. Shows move name, timeline position, durations, advantage values. Customizable appearance (position, size, color, font).

FR-9: Frame advantage display — Real-time frame advantage computation and display after hit or block. Decrements each frame until both characters are actionable. Correct during hitstun and blockstun.

FR-10: Historical input log — Scrollable UI component showing frame-stamped record of both players' inputs (directional and button). Reads from FR-1 input storage. Toggleable P1/P2 display.

FR-11: Frame-by-frame playback — Pause gameplay, step forward/backward one frame at a time. Hitbox/hurtbox overlays rendered during paused playback. Must not desync from future replay system.

FR-12: Gatling table registration — Per-character Gatling table defining which normals cancel into which. Framework validates cancel attempts against table and active cancel window. Tables queryable at runtime.

FR-13: Cancel window consumption — Cancel-eligible input during active cancel window interrupts current move and initiates target move. Window consumed (single use). Overlapping windows for different categories supported.

FR-14: Combo chain uniqueness enforcement — Configurable move-repetition rules. Default: move already used in chain cannot be reused. Per-move "chain-repeatable" exception supported. Resets on combo end.

FR-15: Combo state tracking — Framework tracks combo.active, combo.hit_count, exposes hook point on_combo_hit(move, hit_count) for developer damage scaling. Combo ends when opponent exits hitstun and becomes actionable.

### NonFunctional Requirements

NFR-1: Frame-by-frame playback architecture must accommodate future replay system integration without rework — playback and replay must not desync (from FR-11, Architecture AD-4).

NFR-2: Fail-fast error handling — Invalid input data at startup (malformed JSON, illegal frame timing, circular Gatling routes) throws descriptive exception and crashes. Gameplay errors log warnings and resolve via default rules (AD-8).

NFR-3: Dependency direction enforcement — `using` statements must follow layer order: UI → Engine → Data → Input. Same-layer direct coupling forbidden. Core namespace referenced by all layers (AD-1).

NFR-4: Data immutability — Data layer objects use init-only properties. No field writable after construction by MoveDataLoader (AD-6).

### Additional Requirements

- **GameLoop autoload**: Single Godot autoload node bridging `_Process` to `EventBus.ProcessFrame()`. Zero game logic — thinnest possible Godot-to-framework adapter (AD-7).
- **EventBus singleton**: `sealed` class, `EventBus.Instance`, not a Godot Node. 12 event types defined. Subscribe/Publish pattern. `ProcessFrame()` dispatches events in fixed order (AD-3, AD-5).
- **Frame processing order**: FrameAdvanced → Input System events → Frame Data Engine events → Combo Exec events → UI events. Same-frame response events queued for next frame (AD-4).
- **JSON data loading**: Move definitions, Gatling tables loaded from JSON via System.Text.Json at startup. Data layer owns loaded instances; Engine queries via IDataStore (AD-6).
- **Directory layout**: `Scripts/Framework/{Core,Input,Data,Engine/FrameData,Engine/Combo,UI/Training}/` (AD-2).
- **Module initialization**: Concrete classes `internal`, public APIs via interfaces in Core. Modules receive dependencies via constructor injection at startup.

### UX Design Requirements

No UX design document exists for this project. Training mode UI components (FR-8 through FR-11) are specified in the PRD and will be designed within their respective stories.

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR-1 | Epic 1 | Dual-track input storage |
| FR-2 | Epic 1 | Directional input leniency |
| FR-3 | Epic 1 | Input buffer |
| FR-4 | Epic 1 | Charge retention |
| FR-5 | Epic 1 | Input priority resolution |
| FR-6 | Epic 2 | Move frame data registration (engine side) |
| FR-7 | Epic 2 | Cancel window tracking |
| FR-8 | Epic 2 | Frame data panel UI |
| FR-9 | Epic 2 | Frame advantage display |
| FR-10 | Epic 2 | Historical input log |
| FR-11 | Epic 2 | Frame-by-frame playback |
| FR-12 | Epic 3 | Gatling table registration |
| FR-13 | Epic 3 | Cancel window consumption |
| FR-14 | Epic 3 | Combo chain uniqueness enforcement |
| FR-15 | Epic 3 | Combo state tracking |

## Epic List

### Epic 1: Core Framework & Input System

**Goal:** The developer can clone the repository, install the framework into their Godot 4.x project, define character moves as JSON, configure input leniency per special move, and have their character execute the correct moves in response to player inputs — with modern input feel (buffering, leniency, charge retention).

**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5

**Architecture foundation:** Core namespace (EventBus singleton, base types), Data layer (JSON loading, IDataStore, MoveDefinition), GameLoop autoload, directory scaffold, Input layer.

### Epic 2: Frame Data Engine & Training Mode

**Goal:** The developer can open a training mode scene and see real-time frame data for every move — frame data panel, frame advantage display, historical input log, and frame-by-frame playback with hitbox overlay. The frame data engine drives all move timing and cancel window signals.

**FRs covered:** FR-6, FR-7, FR-8, FR-9, FR-10, FR-11

### Epic 3: Combo System API

**Goal:** The developer can register Gatling tables per character, and the framework handles cancel validation, combo state tracking, chain uniqueness enforcement, and exposes a damage scaling hook point. Combos work end-to-end: input → cancel window → Gatling check → move transition → combo state update.

**FRs covered:** FR-12, FR-13, FR-14, FR-15

---

## Epic 1: Core Framework & Input System

**Goal:** The developer can clone the repository, install the framework into their Godot 4.x project, define character moves as JSON, configure input leniency per special move, and have their character execute the correct moves in response to player inputs — with modern input feel (buffering, leniency, charge retention).

### Story 1.1: Project scaffold & Core infrastructure

As a developer integrating FTG Framework,
I want a well-structured directory layout with the EventBus singleton, GameLoop autoload, and base interfaces in place,
So that I can add framework modules that communicate through a centralized event system.

**Acceptance Criteria:**

**Given** a Godot 4.x C# project with FTG Framework installed
**When** the project runs
**Then** the directory structure matches `Scripts/Framework/{Core,Input,Data,Engine/FrameData,Engine/Combo,UI/Training}/`
**And** `EventBus.Instance` is accessible as a sealed singleton (not a Godot Node)
**And** `GameLoop` autoload calls `EventBus.Instance.ProcessFrame()` each `_Process`
**And** `GameLoop` contains zero game logic beyond the bridge call
**And** Core namespace exposes `IModule`, `IInputHistory`, `IFrameDataEngine`, `IDataStore` interfaces
**And** `EventBus.Subscribe<T>(Action<T>)` and `EventBus.Publish<T>(T)` compile and execute without error

### Story 1.2: Data layer & JSON move loading

As a developer defining character moves,
I want to describe moves in JSON files that the framework loads at startup into immutable data objects,
So that my move data is the single source of truth and cannot be corrupted at runtime.

**Acceptance Criteria:**

**Given** a valid JSON file defining moves with startup, active, recovery, hit_advantage, block_advantage, cancel_windows, and damage
**When** the framework starts up
**Then** `MoveDataLoader` parses the JSON into `MoveDefinition` objects with `init`-only properties
**And** loaded definitions are accessible via `IDataStore.GetMove(string moveId)`
**And** `IDataStore.GetAllMoves()` returns all registered moves
**And** attempting to modify a `MoveDefinition` property after construction is a compile error
**Given** a JSON file with malformed data (missing required field, negative frame count)
**When** the framework starts up
**Then** a descriptive exception is thrown with module prefix `[Data]` and the specific error
**And** the application crashes (fail-fast)

### Story 1.3: Dual-track input storage (FR-1)

As a developer building game logic,
I want the framework to maintain independent histories of directional inputs and button presses with frame timestamps,
So that I can query input state without directional changes corrupting button records.

**Acceptance Criteria:**

**Given** the framework is running with default configuration
**When** a player presses button A on frame 10 and direction 6 on frame 10
**Then** both inputs are recorded independently with frame stamp 10
**And** querying button history returns the A press at frame 10
**And** querying direction history returns the 6 input at frame 10
**Given** the input storage is at capacity (default 600 frames)
**When** a new input arrives on frame 601
**Then** the oldest entry (frame 1) is evicted
**And** the new entry is stored
**Given** the developer configures `input_capacity: 300`
**When** the framework runs
**Then** storage capacity is 300 frames
**And** eviction occurs at frame 301

### Story 1.4: Directional input leniency (FR-2)

As a developer tuning game feel,
I want to define per-move accepted directional input sequences,
So that a DP motion can accept 623, 636, and 323 while a fireball stays strict at 236 only.

**Acceptance Criteria:**

**Given** a special move configured with `accepted_sequences: [623, 636, 323]`
**When** the player inputs 6, then 3, then 6 (sequence 636) within the input buffer window, followed by the required button
**Then** the DP move triggers
**Given** a special move configured with `accepted_sequences: [236]` only
**When** the player inputs 2, then 3, then 6, then 3 (sequence 2363)
**Then** the move does NOT trigger (2363 is not in the accepted list)
**Given** a super move configured with `accepted_sequences: [236236, 23626]`
**When** the player inputs 23626 followed by the button
**Then** the super move triggers on the shorter sequence
**Given** two different special moves on the same character with different accepted sequence lists
**When** each move is tested with its respective leniency variants
**Then** each move only triggers on its own configured sequences
**And** sequence matching respects order: 6 before 2 before 3 for 623, intervening inputs tolerated

### Story 1.5: Input buffer (FR-3)

As a developer designing combo feel,
I want a configurable global input buffer that holds button presses in memory,
So that players have a forgiveness window for executing links and special cancels.

**Acceptance Criteria:**

**Given** the framework is running with default buffer (6 frames)
**When** the player presses button P on frame 0 and completes direction 236 on frame 5
**Then** the move triggers on frame 5 (direction completed within buffer window)
**Given** the developer sets `input_buffer_duration: 3`
**When** the player presses button P on frame 0 and completes direction 236 on frame 4
**Then** the move does NOT trigger (buffer expired after 3 frames)
**Given** the developer sets `input_buffer_duration: 0`
**When** any input sequence is attempted
**Then** no buffering occurs — button press and direction completion must happen on the same frame
**Given** a valid input combination exists in the buffer
**When** the buffer is checked this frame
**Then** the highest-priority move triggers per FR-5 resolution rules

### Story 1.6: Charge retention (FR-4)

As a developer designing charge characters,
I want charge state to persist briefly after direction release and minimum charge duration to be configurable per move,
So that charge move combos feel fluid and character-specific.

**Acceptance Criteria:**

**Given** the framework is running with default charge retention (5 frames)
**When** the player holds 4 for 40 frames, releases on frame 40, and presses 6+P on frame 44
**Then** the charge move triggers (within the 5f retention window)
**Given** the same charge retention setting
**When** the player releases 4 on frame 40 and presses 6+P on frame 46
**Then** the charge move does NOT trigger (outside the 5f window)
**Given** a charge move configured with `min_charge: 30f`
**When** the player holds 4 for 20 frames and releases
**Then** the charge is not considered valid
**Given** the same move with `min_charge: 30f`
**When** the player holds 4 for 35 frames and releases
**Then** the charge is valid and retained for the retention window
**And** 4-charge and 2-charge states are tracked independently per player

### Story 1.7: Input priority resolution (FR-5)

As a developer,
I want a default input priority scheme that resolves ambiguous inputs predictably,
So that a 236236P input always triggers the super, not the fireball, without requiring perfect input timing.

**Acceptance Criteria:**

**Given** default priority scheme is active
**When** both a super move (236236P) and a special move (236P) match the current input
**Then** the super move triggers (Super > Special)
**Given** default priority scheme is active
**When** both a special move (623P) and a normal move (5P) match
**Then** the special move triggers (Special > Normal)
**Given** two special moves with different sequence lengths (623P vs 236P) both match
**When** default priority resolves
**Then** the move with the longer directional sequence (236P, length 3 vs 623P, length 3 — tie goes to registration order)
**And** exactly one move triggers per player per frame
**Given** the developer replaces the default resolver with a custom implementation
**When** input is processed
**Then** the custom resolver's result is used instead of the default priority scheme

---

## Epic 2: Frame Data Engine & Training Mode

**Goal:** The developer can open a training mode scene and see real-time frame data for every move — frame data panel, frame advantage display, historical input log, and frame-by-frame playback with hitbox overlay. The frame data engine drives all move timing and cancel window signals.

**FRs covered:** FR-6, FR-7, FR-8, FR-9, FR-10, FR-11

### Story 2.1: Move frame data registration (FR-6)

As a developer registering character moves,
I want the framework to drive move progression through startup, active, and recovery phases based on registered frame data,
So that all gameplay timing derives from a single authoritative data source.

**Acceptance Criteria:**

**Given** a valid MoveDefinition with startup: 5, active: 3, recovery: 7, hit_advantage: +2, block_advantage: -3, damage: 80
**When** the move is initiated on a character
**Then** FrameDataEngine begins the startup phase on frame 0
**And** the move transitions to active phase on frame 5
**And** the move transitions to recovery phase on frame 8
**And** the move completes on frame 15
**And** hit_advantage (+2) and block_advantage (-3) are queryable from the active move timeline

**Given** FrameDataEngine is tracking an active move
**When** the current frame advances
**Then** the engine publishes `MoveFrameChanged` event containing move_id, current_frame, current_phase (startup/active/recovery), and total_duration

**Given** a new MoveDefinition is registered at runtime
**When** the developer queries the active move
**Then** the runtime change takes effect on the next move execution — not on the currently executing move

**Given** two players each executing their own moves
**When** FrameDataEngine processes the frame
**Then** each player's move timeline advances independently

**Given** no move is active on a character
**When** queried
**Then** FrameDataEngine returns neutral state with phase "idle" and current_frame 0

### Story 2.2: Cancel window tracking (FR-7)

As a developer designing cancel mechanics,
I want the framework to track cancel windows per move and signal when a cancel is available,
So that my combo system can respond to cancel-eligible inputs at the right time.

**Acceptance Criteria:**

**Given** a move configured with `cancel_windows: [{start: 3, end: 7, target: "special"}]`
**When** FrameDataEngine reaches frame 3 of the move
**Then** `CancelWindowEntered` event is published with move_id, window_start=3, window_end=7, target_category="special"
**And** the cancel window remains active through frame 7
**When** FrameDataEngine reaches frame 8
**Then** `CancelWindowExited` event is published

**Given** a move configured with two cancel windows targeting different categories: `[{start: 3, end: 5, target: "special"}, {start: 6, end: 8, target: "super"}]`
**When** FrameDataEngine advances through the move
**Then** both cancel windows are tracked independently
**And** `CancelWindowEntered`/`CancelWindowExited` events fire for each window at its respective boundary frames

**Given** a move with no cancel_windows defined
**When** the move executes
**Then** no `CancelWindowEntered` or `CancelWindowExited` events are published for that move

**Given** a move with a cancel window starting on frame 0 and ending on the final active frame
**When** the move is initiated
**Then** `CancelWindowEntered` fires immediately on frame 0
**And** the window remains open for the entire move duration specified

### Story 2.3: Frame data panel UI (FR-8)

As a developer testing character moves,
I want a built-in UI panel that displays the current move's frame data in real time,
So that I can visually verify timing during training mode without external tools.

**Acceptance Criteria:**

**Given** a character is executing a move during gameplay
**When** the frame data panel is visible
**Then** the panel displays the move_name, current_phase (startup/active/recovery/idle), current_frame within the phase, and total phase durations

**Given** the developer configures panel appearance via position, size, color, and font
**When** the panel renders
**Then** it respects all customization values

**Given** no move is currently active (neutral state)
**When** the panel is visible
**Then** it displays "Idle" as the move name and shows no active phase timing

**Given** the panel subscribes to `MoveFrameChanged` events
**When** a move transitions to a new phase
**Then** the panel updates within the same frame to reflect the new phase

**Given** two players are in a match
**When** the panel is configured to show P1
**Then** only P1's move data is displayed
**When** configured to show P2
**Then** only P2's move data is displayed

### Story 2.4: Frame advantage display (FR-9)

As a developer verifying frame advantage values,
I want a real-time frame advantage display that decrements each frame after hit or block,
So that I can confirm advantage/disadvantage timing is correct during training.

**Acceptance Criteria:**

**Given** a move with hit_advantage +4 connects on the opponent
**When** the active move phase ends and recovery completes
**Then** the advantage display shows "+4" and begins decrementing by 1 each frame
**And** reaches 0 after 4 frames, indicating both characters are now actionable

**Given** a move with block_advantage -5 is blocked
**When** the recovery phase ends
**Then** the advantage display shows "-5"
**And** decrements toward 0 (negative values climb toward 0 as the disadvantaged player recovers)

**Given** both characters are in neutral (advantage = 0)
**When** no hit or block is registered
**Then** the advantage display shows "0" or is hidden (configurable)

**Given** a new hit connects while advantage from a previous hit is still counting down
**When** the `HitConnected` event fires
**Then** the advantage display resets to the new move's hit_advantage value immediately

**Given** the advantage display subscribes to `HitConnected` and `MoveBlocked` events
**When** a hit connects or a move is blocked
**Then** the display updates with the correct advantage value on the same frame the event fires

### Story 2.5: Historical input log UI (FR-10)

As a developer reviewing input timing,
I want a scrollable UI component showing the frame-stamped record of both players' inputs,
So that I can trace exactly which directional and button inputs occurred on which frame.

**Acceptance Criteria:**

**Given** the framework has recorded input history (from FR-1 storage)
**When** the historical input log is visible
**Then** each entry shows frame_number, player_id, input_type (directional/button), and the specific input value (e.g., "6", "LP")

**Given** input history spans 300 frames
**When** the historical input log is rendered
**Then** it displays inputs in chronological order (oldest at top, newest at bottom)
**And** is scrollable with a configurable visible row count (default: 10)

**Given** P1 and P2 have separate input tracks
**When** the display toggle is set to show P1 only
**Then** only P1 inputs appear in the log
**When** the toggle is set to show both players
**Then** both players' inputs appear, differentiated by player label

**Given** a new `InputReceived` event fires
**When** the historical input log receives the event
**Then** the new input entry appears at the bottom of the list within the same frame

**Given** input storage exceeds capacity and evicts old entries (FR-1)
**When** the historical input log queries the storage
**Then** only entries currently in storage are displayed — evicted entries are no longer visible

### Story 2.6: Frame-by-frame playback (FR-11)

As a developer analyzing frame-precise interactions,
I want to pause gameplay and step forward or backward one frame at a time with hitbox overlays,
So that I can inspect exactly what happened on any given frame during testing.

**Acceptance Criteria:**

**Given** gameplay is running
**When** the developer presses the pause input
**Then** `EventBus.ProcessFrame()` stops being called by GameLoop
**And** the current frame state is preserved (characters, positions, move timelines frozen)

**Given** gameplay is paused at frame N
**When** the developer presses step-forward
**Then** GameLoop calls `EventBus.ProcessFrame()` exactly once
**And** the frame counter advances to N+1
**And** all systems (input, frame data, UI) process normally for that single frame advancement

**Given** gameplay is paused at frame N (N > 0)
**When** the developer presses step-backward
**Then** the framework restores state to frame N-1
**And** the frame data panel and historical input log reflect the state at frame N-1

**Given** playback is paused
**When** hitbox overlays are enabled
**Then** character hitbox/hurtbox geometry for the current frame is rendered as overlay shapes
**And** the overlay rendering must not mutate any engine state (read-only observation)

**Given** frame-by-frame playback is active
**When** the developer steps through frames
**Then** the frame processing order (per AD-4) is preserved for each step
**And** event dispatching follows the same fixed order as real-time play — ensuring deterministic behavior compatible with future replay system integration


---

## Epic 3: Combo System API

**Goal:** The developer can register Gatling tables per character, and the framework handles cancel validation, combo state tracking, chain uniqueness enforcement, and exposes a damage scaling hook point. Combos work end-to-end: input → cancel window → Gatling check → move transition → combo state update.

**FRs covered:** FR-12, FR-13, FR-14, FR-15

### Story 3.1: Gatling Table Registration

As a fighting game developer,
I want to register per-character Gatling tables defining which normals can cancel into which moves,
So that the framework validates cancel attempts against these tables during gameplay.

**Acceptance Criteria:**

**Given** the developer has defined a Gatling table JSON file for a character (e.g., `ryu_gatling.json`)
**When** the framework starts up
**Then** `MoveDataLoader` parses the JSON into `GatlingTable` objects via `IDataStore`
**And** each entry maps a source move ID to a list of valid target move IDs with an associated cancel category
**And** invalid entries (malformed move IDs, circular self-cancels without explicit permission) throw a descriptive exception per AD-8

**Given** a `GatlingTable` is loaded into `IDataStore`
**When** `ComboExecutor` needs to validate a cancel from move A to move B
**Then** it queries `IDataStore.GetGatlingTable(characterId)` to retrieve the character's Gatling table
**And** checks whether move B is listed as a valid target for move A under the active cancel window's category

**Given** combat is in progress
**When** the developer swaps a character's Gatling table at runtime (e.g., via developer console for testing)
**Then** the new table takes effect on the next cancel validation query
**And** any in-progress cancel window evaluation (current frame) uses the table that was active when the cancel window opened

**Given** no Gatling table is registered for a character
**When** `ComboExecutor` queries for that character's table
**Then** `IDataStore` returns an empty table
**And** no cancels are permitted for that character (any cancel attempt is rejected)

**Given** the Gatling table data includes entries targeting cancel categories that don't exist in the registered moves
**When** the framework loads the data at startup
**Then** the framework logs a warning (per AD-8 gameplay-level error handling)
**And** the invalid entries are ignored at runtime without crashing

### Story 3.2: Cancel Window Consumption

As a fighting game developer,
I want the framework to consume cancel windows when valid cancel input is received — interrupting the current move and initiating the target move,
So that cancel-based combo routing works correctly without requiring manual timing logic in game code.

**Acceptance Criteria:**

**Given** a character is executing move A and a cancel window is active (category: "special")
**When** the player inputs a valid cancel target move B that is listed in the Gatling table under that category
**Then** `ComboExecutor` publishes `MoveCanceled` with `(fromMove: A, toMove: B, windowCategory: "special")`
**And** the Frame Data Engine (subscribed per AD-3) interrupts move A and initiates move B from frame 0
**And** the cancel window is consumed — the same window cannot trigger a second cancel

**Given** a move has two overlapping cancel windows targeting different categories (e.g., "special" from frame 8-12, "super" from frame 10-14)
**When** the player inputs a "super" move at frame 11
**Then** only the "super" cancel window is consumed
**And** the "special" cancel window remains active until its end frame or until a valid "special" input triggers it

**Given** a cancel window is active
**When** the player inputs a move that is NOT in the Gatling table for the active window's category
**Then** no cancel occurs
**And** the cancel window remains active until its end frame
**And** the current move A continues normally

**Given** a cancel window is active
**When** no cancel-eligible input arrives before the window's end frame
**Then** `CancelWindowExited` is published by the Frame Data Engine
**And** `ComboExecutor` stops monitoring that window
**And** move A continues through its remaining frames without interruption

**Given** a cancel is executed (moves transition A → B)
**When** move B enters its startup phase
**Then** move B's own cancel windows are tracked independently by the Frame Data Engine
**And** the combo chain can continue with further cancels from move B

**Given** multiple valid cancel inputs arrive within the same cancel window
**When** `ComboExecutor` evaluates them in the fixed frame processing order (per AD-4)
**Then** the first valid input processed consumes the window
**And** subsequent inputs within the same window are ignored (window already consumed)

### Story 3.3: Combo Chain Uniqueness Enforcement

As a fighting game developer,
I want the framework to enforce combo chain uniqueness rules — preventing move repeats within a chain by default, with per-move exceptions,
So that infinite loops and unintended repeated sequences are blocked without manual validation in game code.

**Acceptance Criteria:**

**Given** a combo chain is active with moves [5LP, 5MP, 5HP]
**When** the player attempts to cancel into 5MP again
**Then** `ComboExecutor` rejects the cancel attempt
**And** the current move continues without interruption
**And** the cancel window is consumed (the attempt was processed, just rejected)

**Given** a move is marked as `chain_repeatable: true` in its move definition
**When** that move appears earlier in the chain and the player attempts to cancel into it again
**Then** the cancel is permitted
**And** the move is added to the chain as a new entry

**Given** a combo chain is in progress
**When** the combo ends (opponent exits hitstun and becomes actionable, per FR-15)
**Then** the chain uniqueness tracker resets
**And** all moves are eligible again for the next combo

**Given** a chain currently contains [5LP, 5LP] (where 5LP is chain-repeatable)
**When** the player attempts a third 5LP cancel
**Then** the cancel is permitted again (chain-repeatable moves have no repetition limit per chain)

**Given** two different characters use the same move ID (e.g., both have a "5LP" in their respective move definitions)
**When** `ComboExecutor` validates a cancel
**Then** it uses the chain state of the specific character instance
**And** move IDs from another character's chain do not block cancels for this character

**Given** the developer has not explicitly set `chain_repeatable` on any move
**When** a cancel would introduce a duplicate move into the chain
**Then** the default behavior rejects the cancel (no move may repeat in a chain)

### Story 3.4: Combo State Tracking

As a fighting game developer,
I want the framework to track combo state (active/inactive, hit count) and expose a hook point for custom damage scaling logic,
So that I can implement damage scaling and other combo-dependent mechanics without building my own hit counter.

**Acceptance Criteria:**

**Given** no combo is active
**When** a `HitConnected` event is published by the Frame Data Engine
**Then** `ComboStateTracker` sets `combo.active = true` and `combo.hit_count = 1`
**And** publishes `ComboStarted` with `(initiatingMove, hit_count: 1)`
**And** invokes the developer-registered `on_combo_hit(move, hit_count)` callback

**Given** a combo is active with `hit_count = 3`
**When** another `HitConnected` event is published
**Then** `ComboStateTracker` increments `hit_count` to 4
**And** publishes no new `ComboStarted` event (combo already in progress)
**And** invokes `on_combo_hit(move, hit_count: 4)`

**Given** a combo is active
**When** the opponent exits hitstun and becomes actionable (per AD-3: the opponent's Frame Data Engine signals the opponent is no longer in hitstun)
**Then** `ComboStateTracker` publishes `ComboEnded` with `(total_hits, finalMove)`
**And** resets `combo.active = false` and `combo.hit_count = 0`
**And** the chain uniqueness tracker resets (per Story 3.3)

**Given** `on_combo_hit` has not been registered by the developer
**When** a hit connects during a combo
**Then** combo state tracking still operates normally (hit_count increments, events published)
**And** the missing callback is silently skipped — no exception thrown

**Given** a combo is active
**When** the developer queries `ComboStateTracker` via its public API
**Then** the tracker exposes `bool IsActive`, `int HitCount`, `string CurrentMoveId`, and `int ComboStartFrame`

**Given** a combo is active with `hit_count = N`
**When** a `MoveBlocked` event (blocked hit) is published
**Then** combo state tracking does NOT increment `hit_count` (blocked hits are not combo hits)
**And** the combo remains active

**Given** the combo is active and two `HitConnected` events arrive in the same frame (e.g., multi-hit move)
**When** `ComboStateTracker` processes the frame per AD-4 ordering
**Then** both hits increment `hit_count` in order
**And** `on_combo_hit` is invoked once per hit, sequentially

