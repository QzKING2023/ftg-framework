# Source Tree Analysis

## Implemented CORR-2 Structure

The following files and scene composition exist in the current repository:

```text
Scripts/Framework/UI/Training/
  TrainingPresentationLayout.cs
  TrainingPresentationAdapter.cs
  TrainingShortcutRouter.cs
  RuntimeTuningPanel.cs
  TrainingInputPlaybackPanel.cs

TrainingScene
├── WorldPresentationRoot
│   ├── Stage
│   └── Characters
└── TrainingUiLayer
    └── TrainingUiRoot
        ├── LeftDiagnosticsRegion
        ├── TopRightTuningRegion
        └── BottomRightPlaybackRegion
```

## Full Directory Tree

```
ftg-framework/                          # Project root
├── .editorconfig                       # Editor config (UTF-8, LF)
├── .gitattributes                      # Git EOL normalization
├── .gitignore                          # Ignores: .godot/, .idea/, AI agents, build artifacts
├── global.json                         # .NET SDK 10.0.0 (roll-forward)
├── project.godot                       # Godot project file (4.5.1)
├── icon.svg                            # Project icon
├── FTG_Framework.sln                   # Solution file
├── FTG_Framework.csproj                # Main project (Godot.NET.Sdk 4.5.1, net8.0)
│
├── Scripts/                            # C# source root
│   ├── FrameRateManager.cs             # Frame rate configuration
│   └── Framework/
│       ├── Core/                       # Foundational interfaces and types
│       │   ├── EventBus.cs             # Sealed singleton event dispatcher
│       │   ├── GameLoop.cs             # Godot autoload → EventBus bridge
│       │   ├── Events/                 # 12 event type definitions
│       │   │   ├── FrameAdvancedEvent.cs
│       │   │   ├── InputReceivedEvent.cs
│       │   │   ├── InputBufferExpiredEvent.cs
│       │   │   ├── ChargeStateChangedEvent.cs
│       │   │   ├── MoveFrameChangedEvent.cs
│       │   │   ├── CancelWindowEnteredEvent.cs
│       │   │   ├── CancelWindowExitedEvent.cs
│       │   │   ├── HitConnectedEvent.cs
│       │   │   ├── MoveBlockedEvent.cs
│       │   │   ├── ComboStartedEvent.cs
│       │   │   ├── MoveCanceledEvent.cs
│       │   │   └── ComboEndedEvent.cs
│       │   ├── IModule.cs              # Module lifecycle interface
│       │   ├── IDataStore.cs           # Data access interface
│       │   ├── IFrameDataEngine.cs     # Frame data engine interface
│       │   ├── IInputHistory.cs        # Input history query interface
│       │   ├── IInputLeniency.cs       # Leniency matcher interface
│       │   ├── IInputBuffer.cs         # Input buffer interface
│       │   ├── IChargeTracker.cs       # Charge tracker interface
│       │   ├── IPriorityResolver.cs    # Priority resolver interface
│       │   ├── InputEntry.cs           # Input record struct
│       │   ├── InputType.cs            # Directional/Button enum
│       │   ├── ButtonValue.cs          # Button enum (LP, MP, HP, LK, MK, HK)
│       │   ├── DirectionValue.cs       # Direction enum (numpad notation)
│       │   ├── MoveInputConfig.cs      # Per-move input configuration
│       │   ├── MoveCategory.cs         # Normal/Special/Super enum
│       │   └── MatchResult.cs          # Input match result struct
│       ├── Input/                      # Input subsystem implementations
│       │   ├── CircularBuffer.cs       # Fixed-capacity ring buffer
│       │   ├── InputHistory.cs         # Dual-track input storage (FR-1)
│       │   ├── InputLeniencyMatcher.cs # Directional sequence matcher (FR-2)
│       │   ├── InputBuffer.cs          # Input buffer window (FR-3)
│       │   ├── ChargeTracker.cs        # Charge retention tracking (FR-4)
│       │   └── DefaultPriorityResolver.cs  # Priority resolution (FR-5)
│       ├── Data/                       # Data layer
│       │   ├── MoveDataLoader.cs       # JSON → MoveDefinition parser
│       │   ├── DataStore.cs            # Runtime data store (IDataStore impl)
│       │   ├── MoveDefinition.cs       # Immutable move data model
│       │   ├── CancelWindow.cs          # Cancel window data model
│       │   └── example_moves.json      # Sample move definitions
│       ├── Engine/
│       │   ├── FrameData/              # (planned: Epic 2)
│       │   └── Combo/                  # (planned: Epic 3)
│       └── UI/
│           └── Training/               # (planned: Epic 2)
│
├── Tests/
│   └── FTG_Framework.Tests/            # xUnit test project
│       ├── FTG_Framework.Tests.csproj
│       ├── obj/                        # Build artifacts
│       └── bin/                        # Build outputs
│
├── addons/
│   └── rider-plugin/                   # JetBrains Rider Godot plugin
│
└── docs/                               # Generated documentation
    ├── index.md
    ├── project-overview.md
    ├── architecture.md
    ├── source-tree-analysis.md
    ├── development-guide.md
    ├── component-inventory.md
    └── data-models.md
```

## Critical Directories

| Directory | Purpose |
|-----------|---------|
| `Scripts/Framework/Core/` | Interfaces, event definitions, value types — the framework's public contract |
| `Scripts/Framework/Input/` | Input system: history, buffer, leniency, charge, priority |
| `Scripts/Framework/Data/` | Data loading and storage: JSON parsing, immutable models, IDataStore |
| `Scripts/Framework/Engine/` | (planned) Frame data engine and combo executor |
| `Scripts/Framework/UI/` | Training mode UI components, including responsive presentation and recording/playback controls |
| `Tests/FTG_Framework.Tests/` | xUnit test project for framework verification |

## Entry Points

- **Primary**: `GameLoop._Process()` — called each frame by Godot, bridges to `EventBus.ProcessFrame()`
- **Startup**: `GameLoop._Ready()` — constructs input pipeline: DataStore → InputHistory → ChargeTracker → LeniencyMatcher → InputBuffer → PriorityResolver
