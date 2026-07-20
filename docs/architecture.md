# FTG Framework — Architecture

## Architecture Pattern

**Event-driven layered architecture.** The framework is organized into strict layers (Input → Data → Engine → UI) with a centralized EventBus singleton mediating all inter-module communication. No direct coupling between same-layer modules.

## Layer Diagram

```
┌─────────────────────────────────────────┐
│                  UI                      │
│           (read-only observer)           │
├─────────────────────────────────────────┤
│            Engine/Combo                  │
│     FrameDataEngine, ComboExecutor       │
├─────────────────────────────────────────┤
│              Engine/FrameData            │
│         Move timing, cancel windows       │
├─────────────────────────────────────────┤
│                Data                      │
│     MoveDefinition, DataStore, JSON      │
├─────────────────────────────────────────┤
│               Input                      │
│  InputHistory, Buffer, Leniency, Charge  │
├─────────────────────────────────────────┤
│                Core                      │
│  EventBus, Interfaces, Value Types       │
└─────────────────────────────────────────┘
```

## Core Design Principles

### 1. EventBus Singleton (AD-3, AD-5)

The `EventBus` is a `sealed` class with lazy initialization, not a Godot Node. It uses a double-buffered queue: events published during dispatch are queued for the next frame, preventing infinite loops and ensuring deterministic ordering.

**Frame processing order** (AD-4):
1. `FrameAdvanced` — frame tick
2. Input System events
3. Frame Data Engine events
4. Combo Exec events
5. UI events (read-only observation)

### 2. GameLoop Autoload (AD-7)

`GameLoop` is the thinnest possible Godot-to-framework adapter. It:
- Calls `EventBus.Instance.ProcessFrame()` each `_Process`
- Contains zero game logic
- Constructs the module pipeline at startup via `_Ready()`

### 3. Module System

Modules implement `IModule` with `Initialize(IDataStore)` and `Shutdown()`. Dependencies are injected via constructor at startup. Concrete classes are `internal`; public APIs are exposed through interfaces in Core.

### 4. Data Immutability (AD-6)

`MoveDefinition` and related data objects use `init`-only properties. No field is writable after construction by `MoveDataLoader`. Data is loaded from JSON at startup via `System.Text.Json`.

### 5. Dependency Direction (AD-1)

`using` statements must follow layer order: `UI → Engine → Data → Input → Core`. Same-layer direct coupling is forbidden. The Core namespace is referenced by all layers.

### 6. Error Handling (AD-8)

- **Fail-fast**: Invalid startup data (malformed JSON, illegal frame timing) throws descriptive exception with module prefix (e.g., `[Data]`) and crashes
- **Gameplay warnings**: Runtime issues log warnings and resolve via default rules

## State Management

The framework uses an **event-sourced state model**:

- **EventBus** is the single source of truth for frame-level state transitions
- State changes are represented as events (12 event types)
- Events are dispatched in a fixed, deterministic order each frame
- Subscribers maintain their own derived state from event streams
- Same-frame response events are queued for the next frame (double-buffering)

This pattern ensures determinism — critical for future replay system compatibility (NFR-1).

## Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Engine | Godot (Godot.NET.Sdk) | 4.5.1 |
| Runtime | .NET | 8.0 (SDK 10.0.0) |
| Language | C# | 12 (nullable enabled) |
| Serialization | System.Text.Json | built-in |
| Testing | xUnit | via NuGet |
| Test Assertions | xUnit.assert | via NuGet |

## Current Implementation Status

### Implemented (Epic 1)

| Module | Interface | Implementation | FR |
|--------|-----------|----------------|-----|
| Input History | `IInputHistory` | `InputHistory` (CircularBuffer) | FR-1 |
| Input Leniency | `IInputLeniency` | `InputLeniencyMatcher` | FR-2 |
| Input Buffer | `IInputBuffer` | `InputBuffer` | FR-3 |
| Charge Tracker | `IChargeTracker` | `ChargeTracker` | FR-4 |
| Priority Resolver | `IPriorityResolver` | `DefaultPriorityResolver` | FR-5 |
| Data Store | `IDataStore` | `DataStore` | — |
| Data Loader | — | `MoveDataLoader` | — |

### Planned (Epic 2)

| Module | FR |
|--------|-----|
| Frame Data Engine | FR-6 (registration), FR-7 (cancel windows) |
| Frame Data Panel UI | FR-8 |
| Frame Advantage Display | FR-9 |
| Historical Input Log | FR-10 |
| Frame-by-Frame Playback | FR-11 |

### Planned (Epic 3)

| Module | FR |
|--------|-----|
| Gatling Table Registration | FR-12 |
| Cancel Window Consumption | FR-13 |
| Combo Chain Uniqueness | FR-14 |
| Combo State Tracking | FR-15 |
