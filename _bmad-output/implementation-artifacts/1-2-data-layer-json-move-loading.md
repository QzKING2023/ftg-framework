---
baseline_commit: f66625d620de05bd7b1073a51dbf9e8d192517e7
---

# Story 1.2: Data layer & JSON move loading

Status: done

## Story

As a developer defining character moves,
I want to describe moves in JSON files that the framework loads at startup into immutable data objects,
So that my move data is the single source of truth and cannot be corrupted at runtime.

## Acceptance Criteria

1. **Given** a valid JSON file defining moves with startup, active, recovery, hit_advantage, block_advantage, cancel_windows, and damage
   **When** the framework starts up
   **Then** `MoveDataLoader` parses the JSON into `MoveDefinition` objects with `init`-only properties
   **And** loaded definitions are accessible via `IDataStore.GetMove(string moveId)`
   **And** `IDataStore.GetAllMoves()` returns all registered moves

2. **Given** `MoveDefinition` objects are constructed by `MoveDataLoader`
   **When** consumer code attempts to assign a property after construction
   **Then** it is a compile error (`init`-only properties enforced by the compiler)

3. **Given** a JSON file with malformed data (missing required field, negative frame count, invalid cancel window start > end)
   **When** the framework starts up
   **Then** a descriptive exception is thrown with module prefix `[Data]` and the specific error
   **And** the application crashes (fail-fast per AD-8)

## Tasks / Subtasks

- [x] Task 1: Define the JSON move data schema (AC: 1, 3)
  - [x] 1.1 Define all required MoveDefinition fields: `move_id` (string), `startup` (int >= 0), `active` (int >= 0), `recovery` (int >= 0), `hit_advantage` (int), `block_advantage` (int), `damage` (int >= 0)
  - [x] 1.2 Define CancelWindow sub-structure: `start_frame` (int >= 0), `end_frame` (int >= start_frame), `target_category` (string non-empty)
  - [x] 1.3 Define optional fields: `cancel_windows` (CancelWindow[]), `chain_repeatable` (bool, default false)
  - [x] 1.4 Create a sample JSON file `example_moves.json` that passes all validation as a reference

- [x] Task 2: Implement MoveDefinition and CancelWindow data models (AC: 2)
  - [x] 2.1 Create `Scripts/Framework/Data/MoveDefinition.cs` — `init`-only properties for all fields, namespace `FTG_Framework.Data`
  - [x] 2.2 Create `Scripts/Framework/Data/CancelWindow.cs` — `init`-only properties for start_frame, end_frame, target_category
  - [x] 2.3 MoveDefinition holds `IReadOnlyList<CancelWindow> CancelWindows` — empty list if not defined in JSON
  - [x] 2.4 All properties use `init` accessor — assignment only valid during object initialization

- [x] Task 3: Implement MoveDataLoader (AC: 1, 3)
  - [x] 3.1 Create `Scripts/Framework/Data/MoveDataLoader.cs` — `internal` class, namespace `FTG_Framework.Data`
  - [x] 3.2 Implement `MoveDefinition[] LoadFromFile(string path)` — reads JSON, deserializes with `System.Text.Json`
  - [x] 3.3 Implement `MoveDefinition[] LoadFromJson(string json)` — for programmatic/inline loading
  - [x] 3.4 Validation rules (run after deserialization, before returning):
    - `move_id` is non-null, non-empty, unique within the file
    - `startup`, `active`, `recovery` are >= 0
    - `damage` is >= 0
    - `cancel_windows`: each window has `start_frame <= end_frame`, both >= 0, `target_category` is non-null and non-empty
  - [x] 3.5 On validation failure: throw `FormatException` with message prefix `[Data]`, include field name and invalid value
  - [x] 3.6 Use `System.Text.Json.JsonSerializer` with `PropertyNameCaseInsensitive = true` and `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` for snake_case JSON keys → PascalCase C# properties

- [x] Task 4: Implement IDataStore interface and DataStore concrete class (AC: 1)
  - [x] 4.1 Update `Scripts/Framework/Core/IDataStore.cs` — add `MoveDefinition? GetMove(string moveId)` and `IReadOnlyList<MoveDefinition> GetAllMoves()`
  - [x] 4.2 Create `Scripts/Framework/Data/DataStore.cs` — `internal` class implementing `IDataStore`
  - [x] 4.3 DataStore stores loaded moves in `Dictionary<string, MoveDefinition>` keyed by `move_id`
  - [x] 4.4 `GetMove` returns null for unknown move_id (no exception)
  - [x] 4.5 `GetAllMoves` returns a snapshot copy (`ToList()`) to prevent mutation of internal state

- [x] Task 5: Integrate DataStore into framework startup (AC: 1)
  - [x] 5.1 Update `GameLoop.cs` `_Ready()` to call `MoveDataLoader.LoadFromFile` and store results in DataStore
  - [x] 5.2 DataStore instance is created at startup; modules access it via constructor injection (per Architecture consistency conventions)
  - [x] 5.3 On successful load, log loaded move count: `Console.WriteLine("[Data] Loaded {count} moves.")`
  - [x] 5.4 On failure, exception propagates uncaught → application crashes (fail-fast per AD-8)

- [x] Task 6: Create example JSON and verify build
  - [x] 6.1 Create `Scripts/Framework/Data/example_moves.json` with 2-3 valid moves demonstrating the full schema
  - [x] 6.2 Run `dotnet build FTG_Framework.sln` — 0 warnings, 0 errors

## Dev Notes

### Architecture Compliance (Mandatory)

This story implements **AD-6 (Data Ownership)** — the Data layer loads all JSON definitions at startup into immutable objects. Engine modules query IDataStore; they never cache or own copies.

- **AD-1 (Layered Architecture):** Data layer is `FTG_Framework.Data`. Data depends on Core (interfaces, types). Data does NOT reference Input, Engine, or UI namespaces. No `using` to upper layers.
- **AD-2 (Directory Layout):** All new files go in `Scripts/Framework/Data/` except the IDataStore update in `Scripts/Framework/Core/`.
- **AD-3 (Event-Driven):** This story does NOT publish or subscribe to events. The Data layer is passive — it provides query APIs, not event-triggered behavior.
- **AD-6 (Data Ownership):** MoveDefinition and CancelWindow use `init`-only properties. Once loaded, no field is writable. DataStore is the single point of truth. Engine modules query via IDataStore interface; they must NOT cache move definitions.
- **AD-8 (Fail-Fast):** All JSON parsing and validation errors throw immediately with `[Data]` prefix. The application crashes — no silent recovery from bad data.

### Technical Details

**Stack:**
- Godot 4.5+ (C# / .NET bundled Mono runtime)
- `System.Text.Json` (built-in .NET 6+) — zero external NuGet packages
- `System.Text.Json` API surface needed:
  - `JsonSerializer.Deserialize<T>(json, options)`
  - `JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }`

**JSON Schema (defined by this story):**

```json
{
  "moves": [
    {
      "move_id": "5LP",
      "startup": 4,
      "active": 3,
      "recovery": 6,
      "hit_advantage": 2,
      "block_advantage": -3,
      "damage": 30,
      "cancel_windows": [
        {
          "start_frame": 3,
          "end_frame": 6,
          "target_category": "normal"
        }
      ],
      "chain_repeatable": false
    },
    {
      "move_id": "236P",
      "startup": 8,
      "active": 4,
      "recovery": 18,
      "hit_advantage": 1,
      "block_advantage": -7,
      "damage": 70,
      "cancel_windows": [],
      "chain_repeatable": false
    }
  ]
}
```

**MoveDefinition class contract:**

```csharp
namespace FTG_Framework.Data;

public sealed class MoveDefinition
{
    public string MoveId { get; init; }
    public int Startup { get; init; }
    public int Active { get; init; }
    public int Recovery { get; init; }
    public int HitAdvantage { get; init; }
    public int BlockAdvantage { get; init; }
    public int Damage { get; init; }
    public IReadOnlyList<CancelWindow> CancelWindows { get; init; }
    public bool ChainRepeatable { get; init; }
}
```

**CancelWindow class contract:**

```csharp
namespace FTG_Framework.Data;

public sealed class CancelWindow
{
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }
    public string TargetCategory { get; init; }
}
```

**IDataStore interface (fills the existing stub in Core):**

```csharp
namespace FTG_Framework.Core;

public interface IDataStore
{
    MoveDefinition? GetMove(string moveId);
    IReadOnlyList<MoveDefinition> GetAllMoves();
}
```

Note: `IDataStore` is in `FTG_Framework.Core`. It references `MoveDefinition` from `FTG_Framework.Data`. This is intentional — Core interfaces describe contracts that depend on Data-layer types. Add `using FTG_Framework.Data;` to IDataStore.cs.

**MoveDataLoader class contract:**

```csharp
namespace FTG_Framework.Data;

internal static class MoveDataLoader
{
    public static MoveDefinition[] LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static MoveDefinition[] LoadFromJson(string json)
    {
        // Deserialize → validate → return
    }
}
```

`LoadFromFile` uses Godot's file system path. For editor/development, this is a `res://` path. `File.ReadAllText` handles Godot paths when running inside the engine.

**DataStore class contract:**

```csharp
namespace FTG_Framework.Data;

internal sealed class DataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves;

    public DataStore(MoveDefinition[] moves) { /* ... */ }

    public MoveDefinition? GetMove(string moveId) { /* ... */ }
    public IReadOnlyList<MoveDefinition> GetAllMoves() { /* ... */ }
}
```

**Validation rules (applied in MoveDataLoader after deserialization):**

| Rule | Field | Error message prefix `[Data]` |
|------|-------|------|
| Non-null, non-empty | move_id | `[Data] Move has null or empty move_id` |
| Unique move_id | move_id (across file) | `[Data] Duplicate move_id: '{id}'` |
| >= 0 | startup | `[Data] Move '{id}': startup must be >= 0, got {value}` |
| >= 0 | active | `[Data] Move '{id}': active must be >= 0, got {value}` |
| >= 0 | recovery | `[Data] Move '{id}': recovery must be >= 0, got {value}` |
| >= 0 | damage | `[Data] Move '{id}': damage must be >= 0, got {value}` |
| >= 0 | cancel_window.start_frame | `[Data] Move '{id}': cancel window start_frame must be >= 0` |
| end_frame >= start_frame | cancel_window | `[Data] Move '{id}': cancel window end_frame ({end}) < start_frame ({start})` |
| Non-null, non-empty | cancel_window.target_category | `[Data] Move '{id}': cancel window has null or empty target_category` |

**Integration with existing code:**

- `GameLoop._Ready()` is the startup hook. Add after `EventBus` is accessible:
  ```csharp
  public override void _Ready()
  {
      try
      {
          var moves = MoveDataLoader.LoadFromFile("res://Scripts/Framework/Data/example_moves.json");
          var dataStore = new DataStore(moves);
          GD.Print($"[Data] Loaded {moves.Length} moves.");
      }
      catch (Exception ex)
      {
          GD.PrintErr(ex.Message);
          throw; // re-throw → crash (AD-8 fail-fast)
      }
  }
  ```

**Godot file access note:** `System.IO.File.ReadAllText` works with Godot `res://` paths when the project is running inside the Godot editor or exported build. For unit testing outside Godot, `LoadFromJson(string)` bypasses file I/O.

### Previous Story Intelligence (Story 1.1)

- **EventBus** is implemented with Subscribe/Unsubscribe/Publish/ProcessFrame. Single instance, dual-queue design. Not needed by this story, but Data layer modules registered via EventBus in later stories may query IDataStore.
- **GameLoop** is registered as autoload in `project.godot`, `_Process` calls `EventBus.Instance.ProcessFrame()`. `_Ready()` is the startup initialization hook — use it for DataStore bootstrap.
- **IDataStore** exists as an empty stub interface in `Scripts/Framework/Core/IDataStore.cs`. Update in-place — add the two method signatures.
- **IModule** exists with `Initialize()` and `Shutdown()`. DataStore is NOT an IModule (it's a passive data store, not a framework module).
- **12 event types** already exist in `Core/Events/`. None are published by this story.
- **Directory scaffold** already exists — `Scripts/Framework/Data/` is an empty directory.
- **Build system** — `dotnet build FTG_Framework.sln` compiles all C# files. 0 warnings, 0 errors required.
- **No git history beyond initial commit.** All work is uncommitted on `feature` branch.

### C# Conventions (from Architecture Consistency Conventions)

- PascalCase: types, methods, properties
- camelCase: locals, parameters
- `_camelCase`: private instance fields
- Interfaces prefixed with `I`
- Namespace: `FTG_Framework.{Layer}`
- `internal` for concrete implementation classes; public interfaces in Core
- `sealed` for classes not designed for inheritance (MoveDefinition, CancelWindow, DataStore)

### Project Structure Notes

Target files for this story (NEW unless marked UPDATE):

```
Scripts/Framework/
  Core/
    IDataStore.cs      ← UPDATE (fill empty stub with GetMove, GetAllMoves)
  Data/
    MoveDefinition.cs  ← NEW
    CancelWindow.cs    ← NEW
    MoveDataLoader.cs  ← NEW
    DataStore.cs       ← NEW
    example_moves.json ← NEW
  Core/
    GameLoop.cs        ← UPDATE (add _Ready() bootstrap)
```

### Testing

- Verify `MoveDataLoader.LoadFromJson(validJson)` returns correct MoveDefinition objects
- Verify all `init`-only properties are set correctly from JSON
- Verify `IDataStore.GetMove("existing_id")` returns the correct definition
- Verify `IDataStore.GetMove("nonexistent")` returns null
- Verify `IDataStore.GetAllMoves()` returns all loaded moves
- Verify malformed JSON (missing move_id) throws FormatException with `[Data]` prefix
- Verify negative startup throws FormatException with `[Data] Move 'x': startup must be >= 0`
- Verify duplicate move_id throws FormatException
- Verify cancel window with end_frame < start_frame throws FormatException
- Verify `MoveDefinition` property assignment after construction is a **compile error** (the compiler enforces `init`-only)
- Verify empty `cancel_windows` array or missing field produces empty list, not null
- Verify `dotnet build FTG_Framework.sln` succeeds with 0 warnings, 0 errors
- Verify `GameLoop._Ready()` loads moves on startup and crashes on bad JSON (manual test — run in Godot editor)

### References

- Architecture AD-6 (Data Ownership): [ARCHITECTURE-SPINE.md §AD-6](_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md)
- Architecture AD-8 (Fail-Fast): [ARCHITECTURE-SPINE.md §AD-8](_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md)
- Architecture Consistency Conventions: [ARCHITECTURE-SPINE.md](_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md)
- PRD §4.2 FR-6 (Move frame data registration): [prd.md](_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md)
- PRD §9 Assumptions: [prd.md §9](_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15/prd.md)
- Epics — Epic 1 Story 1.2: [epics.md](_bmad-output/planning-artifacts/epics.md)
- Previous Story 1.1: [1-1-project-scaffold-core-infrastructure.md](_bmad-output/implementation-artifacts/1-1-project-scaffold-core-infrastructure.md)

## Dev Agent Record

### Agent Model Used

Claude (via BMad dev-story workflow)

### Debug Log References

- Build: `dotnet build FTG_Framework.sln` — 0 warnings, 0 errors

### Completion Notes List

- All 6 tasks completed. 5 new files created, 2 files updated.
- MoveDefinition and CancelWindow use `init`-only properties — compiler-enforced immutability.
- MoveDataLoader uses System.Text.Json with SnakeCaseLower naming policy for JSON → PascalCase C# mapping.
- Validation covers: null/empty move_id, duplicate IDs, negative timing values, cancel window ordering, empty target_category.
- IDataStore.GetMove returns null for unknown IDs (no exception).
- DataStore.GetAllMoves returns a snapshot copy (ToList) to prevent internal mutation.
- GameLoop._Ready() bootstraps DataStore at startup; crashes on bad JSON per AD-8 fail-fast.
- `#nullable enable` added to IDataStore.cs, GameLoop.cs, and DataStore.cs to resolve CS8632 warnings.

### File List

- `Scripts/Framework/Data/MoveDefinition.cs`
- `Scripts/Framework/Data/CancelWindow.cs`
- `Scripts/Framework/Data/MoveDataLoader.cs`
- `Scripts/Framework/Data/DataStore.cs`
- `Scripts/Framework/Data/example_moves.json`
- `Scripts/Framework/Core/IDataStore.cs` (modified — filled stub with GetMove/GetAllMoves)
- `Scripts/Framework/Core/GameLoop.cs` (modified — added _Ready() bootstrap)

### Review Findings

- [x] [Review][Decision] Static property instead of constructor injection [GameLoop.cs:10] — Resolved: Refactored to method injection via `IModule.Initialize(IDataStore)`. DataStore is no longer a public static property; modules receive it through their Initialize method.

- [x] [Review][Patch] `File.ReadAllText` cannot resolve `res://` paths in exported builds [MoveDataLoader.cs:20, GameLoop.cs:16] — Fixed: Changed to `Godot.FileAccess.Open()` which works in both editor and exported builds.

- [x] [Review][Patch] `CancelWindows` null after JSON deserialization causes NRE [MoveDefinition.cs:14, MoveDataLoader.cs:59] — Fixed: Added null check in Validate() that throws a descriptive FormatException.

- [x] [Review][Patch] `_Ready()` exception leaves DataStore null while _Process continues [GameLoop.cs:10,20-24] — Fixed: Added `SetProcess(false)` before rethrow to prevent _Process from running after initialization failure. Also added null guard in _Process.

- [x] [Review][Patch] `GetMove(null)` throws ArgumentNullException [DataStore.cs:21] — Fixed: Added null guard returning null for null input.

- [x] [Review][Patch] DataStore constructor crashes on null input [DataStore.cs:15] — Fixed: Added `ArgumentNullException.ThrowIfNull(moves)` guard.