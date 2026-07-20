# FTG Framework — Project Overview

## Executive Summary

FTG Framework is a Godot 4.x C# library for building local fighting games. It provides a modern input system (directional leniency, input buffering, charge retention), a frame data engine, and combo system infrastructure so developers can focus on their game's identity rather than rewriting foundational systems.

## Quick Reference

| Item | Detail |
|------|--------|
| **Engine** | Godot 4.5.1 (Godot.NET.Sdk) |
| **Language** | C# (.NET 8.0, SDK 10.0.0 roll-forward) |
| **Test Framework** | xUnit |
| **Architecture** | Event-driven, layered (Input → Data → Engine → UI) |
| **Repository** | Monolith |
| **Entry Point** | `Scripts/FrameRateManager.cs` / `GameLoop` autoload |

## Project Structure

```
ftg-framework/
├── Scripts/
│   ├── Framework/
│   │   ├── Core/         # EventBus, interfaces, value types, events
│   │   ├── Input/        # Input history, buffer, leniency, charge, priority
│   │   ├── Data/         # Move definitions, JSON loading, data store
│   │   ├── Engine/
│   │   │   ├── FrameData/  # (planned: Epic 2)
│   │   │   └── Combo/      # (planned: Epic 3)
│   │   └── UI/
│   │       └── Training/   # (planned: Epic 2)
│   └── FrameRateManager.cs
├── Tests/
│   └── FTG_Framework.Tests/  # xUnit test project
├── docs/                      # Generated documentation
├── project.godot
├── FTG_Framework.sln
└── FTG_Framework.csproj
```

## Development Status

| Epic | Status | Stories |
|------|--------|---------|
| Epic 1: Core Framework & Input System | Done | 7/7 complete |
| Epic 2: Frame Data Engine & Training Mode | Backlog | 0/6 |
| Epic 3: Combo System API | Backlog | 0/4 |

## Key Design Decisions

1. **EventBus as sealed singleton** — centralized event dispatch, not a Godot Node
2. **GameLoop as thinnest possible Godot bridge** — only calls `EventBus.ProcessFrame()`
3. **Data immutability** — `MoveDefinition` uses init-only properties
4. **Fail-fast error handling** — malformed data throws on startup
5. **Fixed frame processing order** — FrameAdvanced → Input → FrameData → Combo → UI
