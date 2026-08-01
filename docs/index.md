# FTG Framework — Documentation Index

These generated documents are navigation aids. The [V2 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md) is the normative technical contract, and [sprint-status.yaml](../_bmad-output/implementation-artifacts/sprint-status.yaml) is the sole authority for volatile implementation status and completion evidence.

## Start Here

- [Project Overview](./project-overview.md)
- [Architecture Navigation](./architecture.md)
- [Development Guide](./development-guide.md)
- [Source Tree Analysis](./source-tree-analysis.md)
- [Component Inventory](./component-inventory.md)
- [Data Models](./data-models.md)
- [Asset Inventory](./assets.md)

## Current Planning Contracts

- [V2 PRD](../_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-26/prd.md)
- [V2 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md)
- [Project V2 Lean UX Contract](../_bmad-output/planning-artifacts/ux-v2-lean-contract.md)
- [Epic 2 Lean UX Contract](../_bmad-output/planning-artifacts/epic-2-ux-contract.md)
- [V2 Epics Index](../_bmad-output/planning-artifacts/epics/index.md)
- [Epic 2 Risk and Evidence Checklist](../_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md)
- [Approved Planning Reconciliation](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md)

## Normative V1 Baseline

The `.archive` suffix records chronology; it does not remove normative force.

- [Final V1 PRD](../_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-15.archive/prd.md)
- [Final V1 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15.archive/ARCHITECTURE-SPINE.md)

## Agent Conventions

- [Domain layout](./agents/domain.md)
- [Issue tracker](./agents/issue-tracker.md)
- [Triage labels](./agents/triage-labels.md)

## Getting Started

1. Open `project.godot` in the checked-in Godot .NET version.
2. Let Godot generate the C# solution if needed.
3. Open `FTG_Framework.sln` and run `dotnet test Tests/FTG_Framework.Tests/`.
4. Run the project in Godot; the GameLoop autoload initializes the framework pipeline.
