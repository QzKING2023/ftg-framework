# FTG Framework — Documentation Index

## Project Overview

- **Type:** Monolith game framework
- **Primary Language:** C# (.NET 8.0)
- **Engine:** Godot 4.5.1
- **Architecture:** Event-driven layered (Input → Data → Engine → UI)

## Quick Reference

| Topic | Detail |
|-------|--------|
| **Tech Stack** | Godot.NET.Sdk 4.5.1, C# 12, xUnit |
| **Entry Point** | `Scripts/Framework/Core/GameLoop.cs` (Godot autoload) |
| **Architecture Pattern** | Event-driven, EventBus singleton, layered modules |
| **Development Status** | Epic 1 complete (7/7), Epic 2 backlog (0/6), Epic 3 backlog (0/4) |

## Generated Documentation

- [Project Overview](./project-overview.md)
- [Architecture](./architecture.md)
- [Source Tree Analysis](./source-tree-analysis.md)
- [Component Inventory](./component-inventory.md)
- [Data Models](./data-models.md)
- [Development Guide](./development-guide.md)
- [Asset Inventory](./assets.md)

## Existing Documentation

- [Domain Agent Instructions](./agents/domain.md) — AI agent codebase exploration guidelines
- [Issue Tracker](./agents/issue-tracker.md) — Issue tracking conventions
- [Triage Labels](./agents/triage-labels.md) — Label classification

## Planning Artifacts (BMad)

Located in `_bmad-output/planning-artifacts/`:

- Product Brief: `briefs/brief-ftg-framework-2026-07-15/brief.md`
- PRD: `prds/prd-ftg-framework-2026-07-15/prd.md`
- Architecture Spine: `architecture/architecture-ftg-framework-2026-07-15/ARCHITECTURE-SPINE.md`
- Epics & Stories: `epics.md`
- Implementation Readiness: `implementation-readiness-report-2026-07-15.md`

## Implementation Artifacts

Located in `_bmad-output/implementation-artifacts/`:

- Sprint Status: `sprint-status.yaml`
- Epic 1 Retro: `epic-1-retro-2026-07-16.md`
- Story files: `1-1` through `1-7` (all complete)
- Deferred Work: `deferred-work.md`

## Getting Started

1. Open `project.godot` in Godot 4.5.1 (mono build)
2. Let Godot generate the C# solution
3. Open `FTG_Framework.sln` in your IDE
4. Press F5 in Godot to run — `GameLoop` initializes the input pipeline automatically
5. Test with `dotnet test Tests/FTG_Framework.Tests/`

For detailed setup instructions, see [Development Guide](./development-guide.md).
