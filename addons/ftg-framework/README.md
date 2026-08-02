# FTG Framework

Modular fighting game framework for Godot 4.x C#.

## Requirements

- Godot 4.5+ (4.7+ recommended)
- .NET SDK 10+ (install separately from https://dotnet.microsoft.com/download)
- C# build tools enabled in Godot

The SDK 10+ requirement applies to scaffold/build tooling. Generated projects
continue to target `net8.0` (`net9.0` for Android) with
`Godot.NET.Sdk/4.5.1`.

## Installation

### Godot Asset Library (planned)

The framework has not yet been submitted to the Godot Asset Library. Until that
release work is complete, use the repository-checkout CLI or manual installation
below.

### Manual Installation

1. Download the latest release zip from GitHub
2. Extract the zip into your project's `addons/` directory — the archive contains a top-level `ftg-framework/` folder, so the files land in `addons/ftg-framework/`

The addon ships a C# EditorPlugin. Build the C# project first, then enable
**FTG Framework** under Project → Project Settings → Plugins. Disabling the
plugin removes and frees its dock; enabling it again reconstructs the dock from
authoritative framework data. Runtime setup still uses the autoload steps below.

### Autoload Setup

After installing the addon, configure autoloads in Project → Project Settings → Autoload:

| Name | Path |
|------|------|
| FrameRateManager | `res://addons/ftg-framework/src/FrameRateManager.cs` |
| GameLoop | `res://Scripts/GameLoop.cs` (created in the next step) |

`addons/ftg-framework/src/Core/GameLoop.cs.template` is a reference implementation
(the `.template` extension keeps it out of compilation). To use the full training setup:

1. Copy `GameLoop.cs.template` into your project's `Scripts/` directory
2. Rename the copy to `GameLoop.cs`
3. Add the `GameLoop` autoload pointing at `res://Scripts/GameLoop.cs`

Or write your own GameLoop that instantiates only the modules you need.

### Quick Start with CLI

The fastest way to start a new project is with the `ftg` CLI tool. It runs from an
FTG Framework repository checkout (it copies the template and framework source from
the repo on disk):

```bash
git clone https://github.com/QzKING2023/ftg-framework
cd ftg-framework
dotnet run --project Scaffold/ftg-cli -- new MyFighter --output ./projects
cd projects/MyFighter
dotnet build
# Open project.godot in the Godot editor
```

The name becomes a C# identifier, assembly, project file, and directory, so use a
valid identifier such as `MyFighter`; `my-fighter` and `9Lives` are rejected.
This command currently requires a repository checkout. A standalone CLI has not
been published.

With the repository, .NET SDK 10+, and matching editor already installed, this
onboarding path is intended to reach training in under five minutes; a cold restore
or first import can add environment-dependent time. Press Play to auto-confirm both
characters. At a viewport of at least 500×120, `[P1] Idle` and `[P2] Idle` are
visible. Press A, D, S, and Space to populate P1's visible input history, then press
U from neutral to run `5LP` and return to `[P1] Idle`.

Asset Library submission and standalone CLI publishing remain unresolved
release/distribution work; this repository-checkout workflow is the current
dogfood entry point, not a claim of final public distribution.

## Modules Included

| Module | Description |
|--------|-------------|
| **EventBus** | Centralized event publishing/subscription system (15 event types) |
| **Input System** | Dual-track input history, buffer, leniency matching, charge tracking, SOCD cleaning |
| **Data Layer** | Move definitions, Gatling tables, character roster — loaded from JSON |
| **Frame Data Engine** | Per-frame move timelines, cancel windows, frame advantage calculation |
| **Combo Engine** | Combo execution, chain validation, combo state tracking |
| **Object Pool** | Pre-allocated, recycled pool for frequently-spawned Godot Nodes |
| **Training UI** | Frame data panel, advantage display, input log, playback controls, hitbox overlay |
| **Character Select** | Character selection flow with roster management |
| **EventBus Debug Panel** | Live event type display with subscriber counts and payload traces |

## Documentation

See the [FTG Framework repository](https://github.com/QzKING2023/ftg-framework) for full documentation, architecture guide, and API reference.

## License

This project is open source. A LICENSE file will be bundled with release packages;
Asset Library submission requires one at the repository root.
