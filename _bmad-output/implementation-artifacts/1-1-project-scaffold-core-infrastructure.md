---
baseline_commit: f66625d620de05bd7b1073a51dbf9e8d192517e7
---

# Story 1.1: Project scaffold & Core infrastructure

Status: done

## Story

As a developer integrating FTG Framework,
I want a well-structured directory layout with the EventBus singleton, GameLoop autoload, and base interfaces in place,
So that I can add framework modules that communicate through a centralized event system.

## Acceptance Criteria

1. **Given** a Godot 4.x C# project with FTG Framework installed
   **When** the project runs
   **Then** the directory structure matches `Scripts/Framework/{Core,Input,Data,Engine/FrameData,Engine/Combo,UI/Training}/`

2. **Given** the framework is initialized
   **When** any module accesses `EventBus.Instance`
   **Then** the instance is a sealed singleton (not a Godot Node)

3. **Given** the Godot scene is running
   **When** `_Process(float delta)` fires each frame
   **Then** `GameLoop` autoload calls `EventBus.Instance.ProcessFrame()`
   **And** `GameLoop` contains zero game logic beyond the bridge call

4. **Given** the Core namespace is compiled
   **When** modules reference Core
   **Then** Core exposes `IModule`, `IInputHistory`, `IFrameDataEngine`, `IDataStore` interfaces

5. **Given** a module subscribes to an event type
   **When** another module publishes that event type
   **Then** `EventBus.Subscribe<T>(Action<T>)` and `EventBus.Publish<T>(T)` compile and execute without error

## Tasks / Subtasks

- [x] Task 1: Create directory scaffold (AC: 1)
  - [x] 1.1 Create `Scripts/Framework/Core/` directory
  - [x] 1.2 Create `Scripts/Framework/Input/` directory
  - [x] 1.3 Create `Scripts/Framework/Data/` directory
  - [x] 1.4 Create `Scripts/Framework/Engine/FrameData/` directory
  - [x] 1.5 Create `Scripts/Framework/Engine/Combo/` directory
  - [x] 1.6 Create `Scripts/Framework/UI/Training/` directory

- [x] Task 2: Implement EventBus singleton (AC: 2, 5)
  - [x] 2.1 Create `EventBus.cs` as sealed class with `Instance` static property (lazy-init, thread-safe optional for V1)
  - [x] 2.2 Implement `Subscribe<T>(Action<T> handler)` — stores handler in `Dictionary<Type, List<Delegate>>`
  - [x] 2.3 Implement `Publish<T>(T event)` — invokes all registered handlers for type T
  - [x] 2.4 Implement `ProcessFrame()` — dispatches queued events in AD-4 fixed order (5 phases)
  - [x] 2.5 Ensure EventBus extends `Node` is NOT true (pure C# class, no Godot dependency)

- [x] Task 3: Define all V1 event types (AC: 5)
  - [x] 3.1 Create `Events/FrameAdvancedEvent.cs`
  - [x] 3.2 Create `Events/InputReceivedEvent.cs`
  - [x] 3.3 Create `Events/InputBufferExpiredEvent.cs`
  - [x] 3.4 Create `Events/ChargeStateChangedEvent.cs`
  - [x] 3.5 Create `Events/MoveFrameChangedEvent.cs`
  - [x] 3.6 Create `Events/CancelWindowEnteredEvent.cs`
  - [x] 3.7 Create `Events/CancelWindowExitedEvent.cs`
  - [x] 3.8 Create `Events/HitConnectedEvent.cs`
  - [x] 3.9 Create `Events/MoveBlockedEvent.cs`
  - [x] 3.10 Create `Events/ComboStartedEvent.cs`
  - [x] 3.11 Create `Events/MoveCanceledEvent.cs`
  - [x] 3.12 Create `Events/ComboEndedEvent.cs`
  - [x] 3.13 All event types are `readonly record struct` in `FTG_Framework.Core.Events` namespace

- [x] Task 4: Create GameLoop autoload (AC: 3)
  - [x] 4.1 Create `GameLoop.cs` as a Godot `Node` with `[Tool]` attribute for autoload registration
  - [x] 4.2 Override `_Process(double delta)` → calls `EventBus.Instance.ProcessFrame()`
  - [x] 4.3 GameLoop publishes `FrameAdvancedEvent` at start of each `ProcessFrame()` call
  - [x] 4.4 Add to Godot project's autoload list (project.godot or via editor)

- [x] Task 5: Define base interfaces in Core (AC: 4)
  - [x] 5.1 Create `IModule.cs` — `void Initialize()`, `void Shutdown()`
  - [x] 5.2 Create `IInputHistory.cs` — stub interface
  - [x] 5.3 Create `IFrameDataEngine.cs` — stub interface
  - [x] 5.4 Create `IDataStore.cs` — stub interface

### Review Findings

- [x] [Review][Patch] (from D1) Move FrameAdvancedEvent into EventBus.ProcessFrame(), strip GameLoop to pure bridge — AD-7 strictly enforced. GameLoop._Process calls only ProcessFrame().
- [x] [Review][Patch] (from D2) Update AD-5 in architecture doc to allow EventBus queue/dispatch transient state — necessary for AD-4 same-frame queuing.
- [x] [Review][Patch] (from D3) Add Unsubscribe<T>() method to EventBus — guard against mid-dispatch modification, support future dynamic subscriptions.
- [x] [Review][Patch] Unhandled exception in handler permanently breaks event bus [EventBus.cs:84] — if a handler throws, _dispatching stays true forever, all future events silently lost. Wrap handler invocation in try/catch and use finally for _dispatching = false.
- [x] [Review][Patch] Subscribe during dispatch can modify collection [EventBus.cs:83-84] — if a handler subscribes to the same type being dispatched, InvalidOperationException. Defensive-copy the handler list before iteration.
- [x] [Review][Patch] Null handler accepted silently on Subscribe [EventBus.cs:18] — Subscribe(null) stores null, crashes at dispatch time far from root cause. Add ArgumentNullException.ThrowIfNull at top of Subscribe.
- [x] [Review][Patch] Orphaned events silently discarded each frame [EventBus.cs:66] — events of types not in the phase dispatch list stay in _currentQueue and are cleared without warning. Log or assert after all phases.
- [x] [Review][Patch] Duplicate handler registration silently permitted [EventBus.cs:23] — calling Subscribe twice with same handler adds it twice. Check Contains before Add.
- [x] [Review][Defer] Autoload path case mismatch [project.godot:19-20] — `res://scripts/` vs `res://Scripts/`. FrameRateManager path is pre-existing; not caused by this change.
- [x] [Review][Defer] Magic integers instead of enums in event types — InputType, InputValue, Direction are raw ints. Defer to Input System stories (1.3+).
- [x] [Review][Defer] _frameNumber integer overflow [GameLoop.cs:7] — wraps after ~414 days at 60fps. Not a practical concern for V1.

## Change Log

- 2026-07-15: Initial implementation — directory scaffold, EventBus singleton, 12 event types, GameLoop autoload, 4 base interfaces

## Dev Notes

### Architecture Compliance (Mandatory)

This story establishes the entire architectural foundation. Every subsequent story depends on these contracts being correct.

- **AD-1 (Layered Architecture):** Files in this story live in `Core/`. Core is the ONLY namespace referenced by all layers. No `using` to any other layer.
- **AD-2 (Directory Layout):** Exact structure is `Scripts/Framework/{Core,Input,Data,Engine/FrameData,Engine/Combo,UI/Training}/`. Namespace mirrors directory — `FTG_Framework.Core`, `FTG_Framework.Input`, etc.
- **AD-3 (Event-Driven):** EventBus is the sole cross-module communication channel. No direct method calls between modules.
- **AD-4 (Frame Processing Order):** `ProcessFrame()` dispatches events in 5 ordered phases: (1) FrameAdvanced, (2) Input System events, (3) Frame Data Engine events, (4) Combo Exec events, (5) UI events. Same-frame responses queued for next frame.
- **AD-5 (EventBus Singleton):** Sealed class. NOT a Godot Node. Single instance via `EventBus.Instance`. Maintains no state except subscriber registrations. ProcessFrame called once per `_Process`.
- **AD-7 (GameLoop Bridge):** GameLoop is the THINNEST possible Godot-to-framework adapter. Zero game logic. Only calls EventBus.Instance.ProcessFrame().
- **AD-8 (Fail-Fast):** Invalid state at startup throws. No silent fallback.

### Technical Details

**Stack:**
- Godot 4.5+ (C# / .NET bundled Mono runtime)
- System.Text.Json (built-in .NET 6+ — needed by Data layer in Story 1.2, not in 1.1)
- Zero external NuGet packages

**EventBus Implementation Notes:**
- Subscription storage: `Dictionary<Type, List<Delegate>>` keyed by event type
- `Subscribe<T>(Action<T> handler)` adds to list; `Publish<T>(T evt)` iterates and invokes
- Thread safety not required for V1 (all processing on main thread via `_Process`)
- Subscriptions registered at module initialization, never modified at runtime (AD-5)
- `ProcessFrame()` must handle same-frame queuing: events published during dispatch go into a pending queue, flushed in next frame (AD-4)

**Event Types (12 total, all in `FTG_Framework.Core.Events`):**
```csharp
// Phase 1: Frame tick
readonly record struct FrameAdvancedEvent(int FrameNumber);

// Phase 2: Input System events
readonly record struct InputReceivedEvent(int PlayerId, int Frame, /* ... */);
readonly record struct InputBufferExpiredEvent(int PlayerId, /* ... */);
readonly record struct ChargeStateChangedEvent(int PlayerId, /* ... */);

// Phase 3: Frame Data Engine events
readonly record struct MoveFrameChangedEvent(int PlayerId, string MoveId, int CurrentFrame, string Phase);
readonly record struct CancelWindowEnteredEvent(int PlayerId, string MoveId, string Category, int StartFrame, int EndFrame);
readonly record struct CancelWindowExitedEvent(int PlayerId, string MoveId, string Category);
readonly record struct HitConnectedEvent(int AttackerId, int DefenderId, string MoveId, int HitAdvantage);
readonly record struct MoveBlockedEvent(int AttackerId, int DefenderId, string MoveId, int BlockAdvantage);

// Phase 4: Combo Exec events
readonly record struct ComboStartedEvent(int PlayerId, string MoveId, int HitCount);
readonly record struct MoveCanceledEvent(int PlayerId, string FromMove, string ToMove, string WindowCategory);
readonly record struct ComboEndedEvent(int PlayerId, int TotalHits, string FinalMove);

// Phase 5: UI events — UI subscribes to earlier phases, publishes nothing (read-only observer per AD-3)
```
Actual field definitions in events should match the full PRD + Architecture specification. The above are structural hints — use precise types from the Architecture event catalog.

**C# Conventions (from Architecture Consistency Conventions):**
- PascalCase: types, methods, properties
- camelCase: locals, parameters
- `_camelCase`: private instance fields
- Interfaces prefixed with `I`
- Namespace: `FTG_Framework.{Layer}`

**Godot Project Integration:**
- GameLoop must be registered as an autoload in `project.godot`:
  ```ini
  [autoload]
  GameLoop="*res://Scripts/Framework/Core/GameLoop.cs"
  ```
- The `GameLoop` class: partial Godot Node, `_Process(double delta)` override
- Delta time for V1 is ignored — framework operates on frame count, not wall-clock time

### Project Structure Notes

Greenfield project. All files are NEW. No existing code to preserve.

Target file layout after this story:
```
Scripts/Framework/
  Core/
    EventBus.cs
    GameLoop.cs
    IModule.cs
    IInputHistory.cs
    IFrameDataEngine.cs
    IDataStore.cs
    Events/
      FrameAdvancedEvent.cs
      InputReceivedEvent.cs
      InputBufferExpiredEvent.cs
      ChargeStateChangedEvent.cs
      MoveFrameChangedEvent.cs
      CancelWindowEnteredEvent.cs
      CancelWindowExitedEvent.cs
      HitConnectedEvent.cs
      MoveBlockedEvent.cs
      ComboStartedEvent.cs
      MoveCanceledEvent.cs
      ComboEndedEvent.cs
  Input/          (empty — Story 1.3+)
  Data/           (empty — Story 1.2)
  Engine/
    FrameData/    (empty — Epic 2)
    Combo/        (empty — Epic 3)
  UI/
    Training/     (empty — Epic 2)
```

### Testing

- Verify EventBus.Instance is non-null and returns same instance on repeated access
- Verify Subscribe then Publish reaches the handler
- Verify unsubscribed handlers are NOT invoked
- Verify ProcessFrame dispatches events in correct 5-phase order
- Verify GameLoop._Process triggers EventBus.ProcessFrame
- Verify GameLoop contains no logic beyond the bridge (inspect — no fields, no conditional branches)
- Verify all 12 event types compile as `readonly record struct`
- Verify directory structure matches AD-2 specification

### References

- Architecture Spine §AD-1 through §AD-5, §AD-7: [ARCHITECTURE-SPINE.md](_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md)
- Architecture Event catalog (12 events): ARCHITECTURE-SPINE.md §AD-3
- Architecture Consistency Conventions: ARCHITECTURE-SPINE.md
- PRD §4.1 Input System, §6.1 MVP Scope: [prd.md](_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md)
- Epics: [epics.md](_bmad-output/planning-artifacts/epics.md) — Epic 1 overview

## Dev Agent Record

### Agent Model Used

Claude (via BMad dev-story workflow)

### Debug Log References

- Build: `dotnet build FTG_Framework.sln` — 0 warnings, 0 errors

### Completion Notes List

- All 5 tasks completed. 21 files created (6 directories, 1 EventBus, 1 GameLoop, 4 interfaces, 12 events). project.godot updated with GameLoop autoload registration.
- EventBus uses dual-queue design: `_currentQueue` for this frame's dispatch, `_nextQueue` for events published during dispatch. AD-4 ordering enforced via 5-phase type filtering.
- GameLoop implements AD-7 — pure bridge, zero game logic.
- All 12 event types are `readonly record struct` per Architecture conventions.

### File List

- `Scripts/Framework/Core/EventBus.cs`
- `Scripts/Framework/Core/GameLoop.cs`
- `Scripts/Framework/Core/IModule.cs`
- `Scripts/Framework/Core/IInputHistory.cs`
- `Scripts/Framework/Core/IFrameDataEngine.cs`
- `Scripts/Framework/Core/IDataStore.cs`
- `Scripts/Framework/Core/Events/FrameAdvancedEvent.cs`
- `Scripts/Framework/Core/Events/InputReceivedEvent.cs`
- `Scripts/Framework/Core/Events/InputBufferExpiredEvent.cs`
- `Scripts/Framework/Core/Events/ChargeStateChangedEvent.cs`
- `Scripts/Framework/Core/Events/MoveFrameChangedEvent.cs`
- `Scripts/Framework/Core/Events/CancelWindowEnteredEvent.cs`
- `Scripts/Framework/Core/Events/CancelWindowExitedEvent.cs`
- `Scripts/Framework/Core/Events/HitConnectedEvent.cs`
- `Scripts/Framework/Core/Events/MoveBlockedEvent.cs`
- `Scripts/Framework/Core/Events/ComboStartedEvent.cs`
- `Scripts/Framework/Core/Events/MoveCanceledEvent.cs`
- `Scripts/Framework/Core/Events/ComboEndedEvent.cs`
- `project.godot` (modified — added GameLoop autoload)
