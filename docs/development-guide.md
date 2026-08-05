# FTG Framework — Development Guide

## Training UI Layout Rules

- Do not assign final runtime overlay positions through absolute `Control.Position` values.
- Add runtime tools to a named `TrainingUiRoot` region.
- Keep layout calculation pure and apply results through thin Godot adapters.
- Do not alter authoritative world state in viewport resize callbacks.
- Add every primary training action through InputMap and expose its resolved binding in the UI.
- Verify the required viewport/UI-scale matrix and generated-scaffold parity before accepting a training UI change.

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| Godot | 4.5.1+ | With .NET support (mono build) |
| .NET SDK | 10.0.0+ | Roll-forward enabled, prerelease allowed |
| IDE | Rider 2024+ or VS 2022+ | Rider plugin included in `addons/rider-plugin/` |

## Environment Setup

1. Clone the repository
2. Open `project.godot` in Godot 4.5.1 (mono)
3. Godot will auto-generate the C# solution
4. Open `FTG_Framework.sln` in your IDE

## Build

```bash
# From IDE: Build → Build Solution
# Or via CLI:
dotnet build FTG_Framework.sln
```

## Run

Press **F5** in the Godot editor. The `GameLoop` autoload initializes the full input pipeline on `_Ready()`.

## Test

```bash
# Run all tests
dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj

# Run specific test
dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --filter "FullyQualifiedName~ChargeTracker"
```

Test project: `Tests/FTG_Framework.Tests/` using xUnit.

## Project Configuration

| File | Purpose |
|------|---------|
| `global.json` | .NET SDK version (10.0.0, roll-forward) |
| `.editorconfig` | UTF-8 charset, LF line endings |
| `.gitattributes` | EOL normalization |
| `.gitignore` | Excludes `.godot/`, IDE files, AI agents |
| `FTG_Framework.csproj` | Godot.NET.Sdk 4.5.1, net8.0, InternalsVisibleTo tests |

## Code Conventions

- **Namespaces**: `FTG_Framework.{Layer}` (e.g., `FTG_Framework.Core`, `FTG_Framework.Input`)
- **Accessibility**: Concrete classes `internal`, public APIs via interfaces in Core
- **Naming**: PascalCase for public members, camelCase for private fields
- **Nullability**: `#nullable enable` on new files
- **Data objects**: `init`-only properties, immutable after construction
- **Error handling**: Fail-fast with descriptive `[Module]` prefix on startup errors
- **Dependencies**: Same-layer direct coupling forbidden. `using` statements follow layer order

## Adding a New Module

1. Define interface in `Scripts/Framework/Core/` (e.g., `INewModule.cs`)
2. Implement in appropriate layer directory (e.g., `Scripts/Framework/Input/NewModule.cs`)
3. Inject dependencies via constructor
4. Register in `GameLoop._Ready()` via `RegisterModule()`
5. Subscribe to events via `EventBus.Instance.Subscribe<T>()`

## Debug Keys (Development)

In `GameLoop._Process()`, keyboard inputs are hardcoded for testing:
- **P** → HP button
- **D** → Forward
- **S** → Down
- **C** → Down-Forward
- **A** → Back

Debug output is enabled via `#if DEBUG` in the GameLoop.
