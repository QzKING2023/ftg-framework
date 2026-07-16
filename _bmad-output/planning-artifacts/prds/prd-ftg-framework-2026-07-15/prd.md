---
title: "FTG Framework"
status: final
created: 2026-07-15
updated: 2026-07-15
---

# PRD: FTG Framework

## 0. Document Purpose

This PRD is the authoritative specification for FTG Framework, a Godot 4.x C# library for building local fighting games. It is written for current and future contributors — anyone who needs to understand *what* the framework must do before deciding *how* to build it. The PRD is self-contained: it assumes no prior reading of the Product Brief, though the Brief may be a useful orientation for newcomers.

Throughout, numbered functional requirements (FR-1 through FR-N) are globally unique and stable across reorganizations. `[ASSUMPTION]` tags mark decisions made without explicit confirmation; they are indexed in §9 and must be validated before downstream architecture work begins.

## 1. Vision

FTG Framework exists to make fighting game development on Godot not just possible, but *approachable*. It is not a game — it is the foundation every fighting game needs and that every developer currently rewrites from scratch.

The framework gives developers three things they cannot get from vanilla Godot. First, a modern input system — directional leniency, generous buffering, charge retention — that makes games feel responsive and forgiving without sacrificing depth. Second, a transparent frame data engine paired with a complete training-mode UI: players see what happened on every frame, and developers do not build it from scratch. Third, a combo infrastructure — Gatling registration, cancel windows — that lets developers express their game's routing logic through configuration, not custom engine code.

The framework is built for breadth: from grounded Street-Fighter-style footsies to high-speed BlazBlue/UNI aerial chains, the same foundation carries them all. It is local-only by design. It is open source, Godot 4.x native C#, and dogfooded by its author. Its ambition is simple — become the answer when a Godot developer asks "I want to make a fighting game, where do I start?"

## 2. Target User

### 2.1 Jobs To Be Done

The target user is a Godot developer building a fighting game. They have chosen Godot for its open-source nature and editor tooling, and they need a foundation that handles the infrastructure so they can focus on their game's identity.

- **Register move data**: The developer defines every move's frame data — startup, active frames, recovery, hit/block advantage, damage, cancel windows — and registers it with the framework. Once registered, the move must execute correctly within the engine: timing, hit detection, state transitions. The framework owns the execution; the developer owns the numbers.

- **Configure input leniency per move**: The developer decides, for each special move, how strict or lenient the directional input should be. A DP-style move (623) might accept 623, 636, 323, or even auto-correct 1313. A charge move (46) might accept 13, 19, or require strict 6-ending. A quarter-circle (236) might stay precise. The framework provides the configuration surface; the developer provides the rules.

- **Define the game's combo style**: The developer decides how combos flow — grounded links like Street Fighter, aerial chains like BlazBlue, reverse-beat mutual cancel like UNI, or something novel. The framework provides Gatling registration, cancel windows, and routing configuration. The developer builds their game's combo identity on top, without rewriting the routing engine.

### 2.3 Key User Journeys

- **UJ-1. Register moves and see them work.** The developer defines frame data for a character's moves in a data file or API call, presses Play in the Godot editor, and sees the move execute correctly — startup animation plays, hitbox activates on the correct frame, recovery plays out. The training mode panel shows the move's frame data live.

- **UJ-2. Tune input feel for a special move.** The developer configures a DP motion to accept 623, 636, and 323. She presses Play, tries each input variant, and the move comes out reliably for all three. She tightens the fireball motion to 236 only, tests with 2363, and confirms it does not trigger.

- **UJ-3. Build a combo route.** The developer registers a Gatling table: 5A → 5B → 5C → special. She presses Play, executes the chain in training mode, and sees the combo counter increment and the cancel windows respected. She adds a rule that 5A cannot repeat in the same chain (UNI-style), and verifies the framework enforces it.

## 3. Glossary

- **Frame** — The fundamental unit of time. At 60fps, one frame ≈ 16.67ms. All timing in this PRD is expressed in frames unless otherwise noted.
- **Move** — Any action a character can perform: normal moves, special moves, super moves. Every move has associated frame data.
- **Normal move** — A move triggered by a single button press, optionally with a simple directional modifier (e.g., 5A, 2B, 6C).
- **Special move** — A move requiring a specific directional input sequence followed by a button press (e.g., 236A, 623B). Subject to directional input leniency rules.
- **Super move** — A special move requiring a longer input sequence (e.g., 236236A). May have different leniency rules than standard specials.
- **Charge move** — A special move requiring a direction to be held for a minimum duration before release (e.g., [4]6A). Subject to charge retention.
- **Startup frames** — Frames from move initiation until the first active frame. During startup the move has no hitbox.
- **Active frames** — Frames during which the move's hitbox can connect with an opponent's hurtbox.
- **Recovery frames** — Frames after the active period ends, before the character can act again.
- **Hit advantage** — The frame difference between attacker and defender when a move connects. Positive = attacker recovers first.
- **Block advantage** — The frame difference between attacker and defender when a move is blocked. Negative values indicate the attacker is punishable.
- **Cancel window** — A range of frames (relative to a move's timeline) during which that move can be canceled into another move. A move may have multiple cancel windows for different targets.
- **Gatling** — A chain of normal attacks that cancel into each other in a defined, configurable sequence (e.g., 5A → 5B → 5C).
- **Gatling table** — The per-character configuration mapping which moves serve as valid Gatling sources and destinations.
- **Combo** — A sequence of moves that connect before the opponent exits hitstun and can act.
- **Directional input leniency** — The set of rules determining which directional input sequences are accepted as valid for a given special/super move. Defined per move, not globally.
- **Input buffer** — A time window (in frames) during which a button press is held in memory. If a valid move input is completed within this window, the move triggers on the next available frame.
- **Charge retention** — A time window (in frames) after releasing a held direction during which a charge move remains available.
- **Hitbox** — A region that, when overlapping an opponent's hurtbox during a move's active frames, registers a hit.
- **Hurtbox** — A region on a character that, when overlapping an opponent's hitbox, receives a hit.
- **Frame data panel** — A training mode UI component displaying the current move's startup, active frames, recovery, and advantage values in real time.
- **Historical input log** — A training mode UI component showing a scrollable, frame-stamped record of player inputs (both directional and button).
- **Frame-by-frame playback** — The ability to pause gameplay and advance or rewind one frame at a time, with hitbox/hurtbox overlays visible at each step.

## 4. Features

### 4.1 Input System

**Description:** The input system is the framework's heartbeat — the layer between player hardware and game logic. It maintains two independent input histories (directional and button), applies per-move directional leniency, buffers button presses for a configurable window, and preserves charge state after direction release. The developer configures what inputs each move accepts; the framework handles detection, buffering, priority resolution, and charge tracking. Realizes UJ-2.

**Functional Requirements:**

#### FR-1: Dual-track input storage

The framework stores directional input history and button press history as two independent, non-interfering sequences. Each entry is frame-stamped. Realizes UJ-2.

**Consequences (testable):**
- A directional change on frame N does not overwrite or clear a button press that occurred on frame N.
- Both histories are queryable by any framework subsystem (frame data engine, training mode UI, replay system).
- Storage capacity is configurable, defaulting to 600 frames (~10 seconds at 60fps). Once capacity is reached, oldest entries are evicted.

**Out of Scope:**
- SOCD cleaning (Simultaneous Opposite Cardinal Directions) is not handled by the framework in V1. Developers preprocess raw hardware input themselves or contribute SOCD cleaners as extensions.

#### FR-2: Directional input leniency

The developer can define, for any special move or super move, the set of directional input sequences the framework will accept. The framework matches the player's recent directional history against these sequences, honoring the leniency rules configured for each move. Realizes UJ-2.

**Consequences (testable):**
- A DP move configured with `[623, 636, 323]` triggers when the directional history contains any of those three sequences within the input buffer window, followed by the required button.
- A quarter-circle move configured with `[236]` only (no leniency) does NOT trigger on 2363 or 26.
- A super move configured with `[236236, 23626]` triggers on the shorter sequence.
- Leniency is defined per move. Two different special moves on the same character can have different leniency rules.
- Input sequence matching respects the order constraint: an accepted sequence's elements must appear in order, but intervening inputs are tolerated. For example, a 623 match on `[623, 636, 323]` requires that 6 appeared before 2 before 3, not that they were consecutive.

**Out of Scope:**
- The developer is responsible for constructing the accepted-sequence list. The framework does not auto-generate leniency variants.

#### FR-3: Input buffer

The framework holds button presses in a configurable global buffer window. If a valid move input (direction sequence + matching button) is completed within this window, the move triggers on the next available frame. The buffer is global to ensure consistent game feel. Realizes UJ-2.

**Consequences (testable):**
- Default buffer duration is 6 frames.
- With a 6f buffer, a button press on frame 0 followed by a direction completion on frame 5 triggers the move on frame 5 (the direction completes within the buffer window).
- The buffer duration is configurable by the developer as a global setting.
- Setting the buffer to 0 disables buffering entirely.
- The buffer is checked every frame for each player; when a valid input combination exists, the highest-priority move (see FR-5) is triggered.

**Out of Scope:**
- Per-move buffer overrides. In V1 the buffer is global only. `[NOTE FOR PM]` Revisit if per-move buffer tuning is needed for specific game designs.

#### FR-4: Charge retention

The framework preserves charge state for a configurable global window after the player releases the held direction. The developer can configure, per charge move, the minimum duration a direction must be held before the charge is considered valid. Realizes UJ-2.

**Consequences (testable):**
- Default charge retention window is 5 frames (global).
- With 5f retention, releasing 4 on frame 0 and pressing 6+P on frame 4 triggers the charge move.
- The minimum charge duration is configurable per move. A charge move with `min_charge: 30f` does not become available until the direction has been held continuously for at least 30 frames.
- Charge state is tracked independently per player and per direction (4 and 2 charges coexist without interference).

#### FR-5: Input priority resolution

When multiple moves match the current input state simultaneously, the framework selects one move to trigger. The framework provides a default priority scheme; the developer can override it. Realizes UJ-2.

**Consequences (testable):**
- Default priority: Super moves > Special moves > Normal moves. Within the same category, moves with longer directional input sequences take priority.
- The developer can replace the default priority with a custom resolver.
- Priority resolution produces exactly one move per player per frame, or no move if no input matches.

### 4.2 Frame Data Engine + Training Mode Visualization

**Description:** The frame data engine is the source of truth for every timing property of every move. It computes frame data from developer-provided move definitions and exposes it to both gameplay logic (hit detection, state transitions) and the training mode UI. The training mode UI is a set of built-in, customizable visual components — frame data panel, frame advantage display, historical input log, frame-by-frame playback controls, and hitbox/hurtbox overlay — that give developers and players SF6-level transparency into what is happening on every frame. Realizes UJ-1.

**Functional Requirements:**

#### FR-6: Move frame data registration

The developer registers a move with its complete frame data: startup duration, active duration, recovery duration, hit advantage, block advantage, cancel windows (if any), and damage values. The framework stores this as the authoritative move definition and drives gameplay timing from it. Realizes UJ-1.

**Consequences (testable):**
- A move registered with `startup: 5, active: 3, recovery: 12, hit_advantage: +2, block_advantage: -5` plays out with exactly those durations when executed.
- The framework exposes the current frame of the current move (relative to its timeline) at all times during gameplay.
- Changing a move's frame data at runtime (e.g., via a training mode editor) takes effect on the next execution of that move.

#### FR-7: Cancel window tracking

The framework tracks cancel windows defined in a move's frame data and signals when the current frame falls within one. Each cancel window specifies a start frame, end frame, and a target category (e.g., "cancel to special", "cancel to super", "cancel to normal/Gatling"). Realizes UJ-3.

**Consequences (testable):**
- A move with a cancel window of `[8, 14]` targeting "special" signals cancel-availability on frames 8 through 14 inclusive.
- A move may define multiple cancel windows targeting different categories.
- The cancel signal is consumed by the combo system (see §4.3) to determine whether a chained move is legal.

#### FR-8: Frame data panel

The framework provides a built-in UI component that displays the current move's frame data in real time during gameplay. The panel updates every frame to reflect the active move's current state. Realizes UJ-1.

**Consequences (testable):**
- When a player presses a button and a move begins, the panel shows: move name, current timeline position (e.g., "startup frame 3/5"), startup/active/recovery durations, and hit/block advantage values.
- When the move hits or is blocked, the panel updates the advantage display to show the actual frame differential.
- When no move is active, the panel shows the character's idle status or "actionable" state.
- The panel's visual style is customizable (position, size, color scheme, font) via the framework's UI theming system. `[ASSUMPTION: UI customization is a V1 requirement — confirm scope of theming API.]`

#### FR-9: Frame advantage display

The framework computes and displays the real-time frame advantage between the two characters after a hit or block. Positive values indicate the attacker recovers first; negative values indicate the defender recovers first. Realizes UJ-1.

**Consequences (testable):**
- On hit, the display shows the attacker's hit advantage in real time, decrementing each frame until both characters are actionable.
- On block, the display shows block advantage with the same real-time countdown.
- The display updates correctly when either character is in hitstun or blockstun.

#### FR-10: Historical input log

The framework provides a scrollable UI component showing a frame-stamped record of both players' inputs (directional and button), read from the input storage (FR-1). Realizes UJ-1.

**Consequences (testable):**
- Each entry shows: frame number, player identifier, input type (direction or button), and the specific input value.
- The log scrolls in real time as new inputs are recorded.
- The developer can toggle display of P1 inputs, P2 inputs, or both.

#### FR-11: Frame-by-frame playback

The framework supports pausing gameplay and stepping forward or backward one frame at a time. During paused playback, hitbox and hurtbox positions are rendered as overlays on the characters at the current frame. Realizes UJ-1.

**Consequences (testable):**
- Pausing halts all game logic at the current frame.
- Advancing one frame executes exactly one frame of game logic, then halts again.
- Rewinding one frame restores the game state to the previous frame. `[ASSUMPTION: Rewind is achieved via deterministic state replay or snapshot — architecture decision deferred.]`
- During paused/stepped playback, hitbox and hurtbox regions are drawn as visible overlays on the character sprites.

**Feature-specific NFRs:**
- Frame-by-frame playback must not desync from the replay system (post-V1). The architecture must accommodate replay integration without rework.

### 4.3 Combo System API

**Description:** The combo system provides the infrastructure for defining and executing move chains. It does not prescribe how combos work — it provides the registration surface, the cancel-eligibility check, and the state tracking that developers build their game's combo logic on top of. The framework records Gatling tables, validates cancel legality against cancel windows (FR-7), tracks the current combo state, and enforces move-repetition rules. Damage scaling, hitstun behavior, and infinite-prevention logic are the developer's responsibility. Realizes UJ-3.

**Functional Requirements:**

#### FR-12: Gatling table registration

The developer registers, per character, a Gatling table defining which normal moves can cancel into which other normal moves. The framework validates cancel attempts against this table and the active cancel window (FR-7). Realizes UJ-3.

**Consequences (testable):**
- A Gatling entry `5A → [5B, 5C, 2A]` allows 5A to cancel into 5B, 5C, or 2A when a cancel window targeting "normal" is active.
- A cancel attempt from 5A to a move not in its destination list (e.g., 5D) is rejected.
- Gatling tables are queryable at runtime: the framework can answer "what moves can I cancel into from this move right now?" for use in training mode UI or AI logic.

#### FR-13: Cancel window consumption

When a cancel-eligible input is received during an active cancel window, the framework interrupts the current move and initiates the target move. The cancel window is consumed — a single cancel window cannot be used for multiple cancels. Realizes UJ-3.

**Consequences (testable):**
- During a move's cancel window ([8, 14] targeting "special"), a special move input on frame 10 interrupts the current move and starts the special.
- If no cancel-eligible input is received before frame 14, the cancel window closes and the current move plays out to recovery.
- A move with multiple overlapping cancel windows (e.g., "normal" on [5, 10] and "special" on [8, 14]) allows different target categories at different times. A cancel consumes only the relevant window.

#### FR-14: Combo chain uniqueness enforcement

The framework supports configurable move-repetition rules within a combo chain. By default, a move that has already been used in the current chain cannot be used again. Exceptions (e.g., A-series chain cancels) are configurable per move or per category. Realizes UJ-3.

**Consequences (testable):**
- In a chain `5A → 5B → 5C`, attempting to cancel back into 5A is rejected by default.
- A move tagged as "chain-repeatable" (e.g., the A-series in UNI-style systems) is exempt from uniqueness enforcement.
- The uniqueness check resets when the combo ends (see FR-15).

#### FR-15: Combo state tracking

The framework tracks whether a combo is in progress and exposes combo metadata to other systems. The combo begins on the first hit that connects and ends when the opponent exits hitstun and becomes actionable. Realizes UJ-3.

**Consequences (testable):**
- `combo.active` is true from the first connected hit until the opponent is actionable again.
- `combo.hit_count` increments by 1 for each move that connects during an active combo.
- The framework exposes a hook point for developer-defined damage scaling logic: `on_combo_hit(move, hit_count)` — the developer implements the scaling formula. The framework does NOT compute scaling itself.
- When the combo ends, chain uniqueness state (FR-14) resets, and `combo.active` becomes false.

**Out of Scope:**
- Damage scaling formulas. The framework calls the developer's hook; it does not calculate scaling values.
- Hitstun behavior chains (e.g., ground bounce vs. wall splat vs. juggle state). These are game design decisions.
- Infinite combo detection or prevention. The framework provides the data (hit count, move history); the developer decides what to do with it.

## 5. Non-Goals (Explicit)

FTG Framework is **not**:

- **A game engine.** It is a library that runs on Godot 4.x. It does not replace Godot's rendering, physics, or scene system. Developers still use the Godot editor and Godot's APIs for everything outside the framework's scope.
- **A game maker or no-code tool.** The framework provides C# APIs, not a visual editor, drag-and-drop builder, or template-based character creator. Developers write code.
- **A networking solution.** The framework is local-only. Rollback netcode, peer-to-peer, and online matchmaking are out of scope permanently. `[NON-GOAL for MVP]` Developers who need online play must integrate a separate networking layer; the framework's input and state systems are designed to be networking-friendly (deterministic, frame-based) but do not provide networking themselves.
- **Cross-engine.** Godot 4.x only. Ports to Unity, Unreal, or other engines are not planned and not accommodated in the architecture.
- **A visual scripting or Blueprint-style system.** All configuration is code-driven (C# API calls, data files loaded at runtime). Some visual feedback exists in the training mode UI, but that is consumption (viewing data), not authoring (creating data).

## 6. MVP Scope

### 6.1 In Scope

- **Input System** (FR-1 through FR-5): Dual-track input storage, directional input leniency per move, global input buffer, charge retention with per-move minimum duration, configurable input priority.
- **Frame Data Engine + Training Mode Visualization** (FR-6 through FR-11): Move frame data registration, cancel window tracking, frame data panel, frame advantage display, historical input log, frame-by-frame playback with hitbox overlay.
- **Combo System API** (FR-12 through FR-15): Gatling table registration, cancel window consumption, chain uniqueness enforcement, combo state tracking with developer hook point.

### 6.2 Out of Scope for MVP

- **Character state machine** — Deferred to post-V1. V1 provides no state machine tools. Moves are executed directly via API calls; state transitions are the developer's responsibility.
- **Hitbox / hurtbox system** — Deferred to post-V1. FR-11 renders hitboxes as overlays, but the underlying hitbox/hurtbox collision engine is not in V1. `[NOTE FOR PM]` This is the highest-priority post-V1 item — without it, FR-11's hitbox overlay has no data to render.
- **Replay system** — Deferred to post-V1. FR-11's frame-by-frame playback provides partial replay-like functionality, but full-game recording and playback is not in V1.
- **Object pool** — Deferred to post-V1. Memory management is the developer's responsibility in V1.
- **Networking** — Permanently out of scope (see §5).
- **Godot 3.x support** — Permanently out of scope (see §5).
- **Damage scaling, infinite prevention, hitstun chains** — Permanently out of scope. Developer responsibility via hook points.

## 7. Success Metrics

**Primary**

- **SM-1: Dogfooding validation** — The author's own fighting game project uses FTG Framework as its foundation, with all three V1 modules (input, frame data, combo) exercised in real gameplay. Validates all 15 FRs through real use. Target: author ships at least one playable build with the framework integrated.

**Secondary**

- **SM-2: External adoption** — An independent developer (no write access to the repo) ships a playable fighting game using the framework. Validates that FR-1 through FR-15 are documented, installable, and usable by a stranger. Target: one external game within 12 months of V1 release.
- **SM-3: Community contribution** — The framework receives a substantive PR from a non-author contributor (new input mode, additional UI theme, bug fix with test coverage). Validates code structure and contribution guide. Target: one non-trivial PR within 18 months of V1 release.

**Counter-metrics (do not optimize)**

- **SM-C1: Feature breadth over V1 completion** — Adding post-V1 modules (state machine, hitbox, replay, object pool) before V1 modules are stable and dogfooded. Counterbalances SM-2.
- **SM-C2: Vanity metrics** — GitHub stars, forks, or social media attention that does not reflect actual usage. A starred repo with zero shipped games is failure. Counterbalances SM-2 and SM-3.

## 8. Open Questions

1. **Per-move buffer overrides** — FR-3 defines a global input buffer. Is there a game design that requires per-move buffer values (e.g., a specific link that needs a tighter or looser window)? If yes, this becomes a V1.x FR; if no, the global buffer is sufficient.
2. **Hitbox system priority** — §6.2 notes the hitbox/hurtbox system is the highest-priority post-V1 item, since FR-11's hitbox overlay renders data the engine does not yet produce. Should the hitbox system be pulled into a V1.1 patch cycle?
3. **UI theming depth** — FR-8 specifies customizable panel appearance (position, size, color, font). Is the theming API surface limited to these four properties in V1, or should it support a broader CSS-like styling model?

## 9. Assumptions Index

- **A-1** (FR-8): UI component customization (position, size, color scheme, font) is within V1 scope. The theming API surface is limited to these four properties unless expanded by resolution of Open Question 3.
- **A-2** (FR-11): Frame-by-frame rewind is achievable via deterministic state replay. The architecture does not need to support snapshot-based rewind in V1. Post-V1 replay system integration will use the same mechanism.

