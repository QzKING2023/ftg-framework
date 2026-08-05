# FTG Framework — Component Inventory

## Implemented CORR-2 Components

These components are present in the repository. Their acceptance evidence and current review state are tracked separately in `sprint-status.yaml`.

| Component | Responsibility |
|---|---|
| `TrainingPresentationLayout` | Pure-C# calculation of reference-region scale, center, safe margins, and responsive overlay allocations |
| `TrainingPresentationAdapter` | Thin Godot adapter applying world transforms and screen-space layout on viewport changes |
| `TrainingUiRoot` | Screen-space owner for training/developer Controls |
| `TrainingShortcutRouter` | Maps resolved InputMap actions to the same validated ViewModel commands used by visible controls |

## Core Components

### Event Bus
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Core/EventBus.cs` |
| **Type** | Sealed singleton |
| **Purpose** | Centralized event dispatch, 12 event types, double-buffered queue |
| **Key API** | `Subscribe<T>()`, `Unsubscribe<T>()`, `Publish<T>()`, `ProcessFrame()` |

### GameLoop
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Core/GameLoop.cs` |
| **Type** | Godot autoload (`partial class GameLoop : Node`) |
| **Purpose** | Bridges Godot `_Process` to `EventBus.ProcessFrame()`. Module initialization in `_Ready()` |
| **Key API** | `RegisterModule(IModule)` |

### Event Types (12)
| Event | Phase | Purpose |
|-------|-------|---------|
| `FrameAdvancedEvent` | 1: Frame Tick | Frame counter advance |
| `InputReceivedEvent` | 2: Input | New input recorded |
| `InputBufferExpiredEvent` | 2: Input | Buffer entry timed out |
| `ChargeStateChangedEvent` | 2: Input | Charge hold/release |
| `MoveFrameChangedEvent` | 3: Frame Data | Move phase transition |
| `CancelWindowEnteredEvent` | 3: Frame Data | Cancel window opened |
| `CancelWindowExitedEvent` | 3: Frame Data | Cancel window closed |
| `HitConnectedEvent` | 3: Frame Data | Move hit opponent |
| `MoveBlockedEvent` | 3: Frame Data | Move blocked |
| `ComboStartedEvent` | 4: Combo | Combo initiated |
| `MoveCanceledEvent` | 4: Combo | Move canceled into another |
| `ComboEndedEvent` | 4: Combo | Combo terminated |

## Input Components

### InputHistory
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/InputHistory.cs` |
| **Interface** | `IInputHistory` |
| **Purpose** | Dual-track input storage: directional and button inputs stored independently with frame timestamps (FR-1) |
| **Details** | Uses `CircularBuffer<InputEntry>`, default capacity 600 frames |

### InputLeniencyMatcher
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/InputLeniencyMatcher.cs` |
| **Interface** | `IInputLeniency` |
| **Purpose** | Matches player directional input history against per-move accepted sequences (FR-2) |
| **Details** | Order-respecting matching, intervening inputs tolerated |

### InputBuffer
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/InputBuffer.cs` |
| **Interface** | `IInputBuffer` |
| **Purpose** | Configurable global buffer window (default 6f) for input forgiveness (FR-3) |

### ChargeTracker
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/ChargeTracker.cs` |
| **Interface** | `IChargeTracker` |
| **Purpose** | Charge retention window (default 5f), per-move min charge duration (FR-4) |

### DefaultPriorityResolver
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/DefaultPriorityResolver.cs` |
| **Interface** | `IPriorityResolver` |
| **Purpose** | Default priority: Super > Special > Normal; within category, longer sequence wins (FR-5) |

### CircularBuffer
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Input/CircularBuffer.cs` |
| **Type** | Generic data structure |
| **Purpose** | Fixed-capacity ring buffer used by InputHistory |

## Data Components

### MoveDataLoader
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Data/MoveDataLoader.cs` |
| **Purpose** | Parses JSON move definitions into immutable `MoveDefinition` objects |

### DataStore
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Data/DataStore.cs` |
| **Interface** | `IDataStore` |
| **Purpose** | Runtime data access: `GetMove()`, `GetAllMoves()` |

### MoveDefinition
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Data/MoveDefinition.cs` |
| **Properties** | MoveId, Startup, Active, Recovery, HitAdvantage, BlockAdvantage, Damage, CancelWindows, ChainRepeatable |
| **Immutability** | All properties `init`-only |

### CancelWindow
| Property | Value |
|----------|-------|
| **File** | `Scripts/Framework/Data/CancelWindow.cs` |
| **Properties** | Start frame, end frame, target category |

## Test Components

| Project | Framework | Location |
|---------|-----------|----------|
| `FTG_Framework.Tests` | xUnit | `Tests/FTG_Framework.Tests/` |

## Third-Party

| Component | Version | Purpose |
|-----------|---------|---------|
| GodotSharp | 4.5.1 | Godot C# API bindings |
| xUnit | via NuGet | Test framework |
| Newtonsoft.Json | via NuGet | Test dependency |
| Rider Plugin | bundled | JetBrains Rider IDE integration |
