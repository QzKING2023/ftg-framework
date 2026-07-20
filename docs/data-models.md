# FTG Framework — Data Models

## Core Data Types

### InputEntry
```csharp
// Immutable struct — one entry per input event
struct InputEntry {
    int PlayerId;
    InputType Type;      // Directional or Button
    int Value;           // Cast to DirectionValue or ButtonValue
    int FrameNumber;
    bool IsCharge;       // true if this direction entry is a charge hold
    int Duration;        // frames held (charge only)
}
```

### MoveDefinition
```csharp
// Immutable — init-only properties. Loaded from JSON at startup.
class MoveDefinition {
    string MoveId;              // Unique identifier (e.g., "fireball_p")
    int Startup;                // Startup frames
    int Active;                 // Active frames
    int Recovery;               // Recovery frames
    int HitAdvantage;           // Frame advantage on hit
    int BlockAdvantage;         // Frame advantage on block
    int Damage;                 // Base damage
    IReadOnlyList<CancelWindow> CancelWindows;
    bool ChainRepeatable;       // Allow repeat in same combo chain
}
```

### MoveInputConfig
```csharp
// Per-move input configuration — developer-defined matching rules
class MoveInputConfig {
    string MoveId;
    DirectionValue[][] AcceptedSequences;  // e.g., [[6,2,3], [6,3,6]] for DP
    ButtonValue RequiredButton;
    DirectionValue? ChargeDirection;       // For charge moves (e.g., [4]6)
    int MinChargeDuration;                 // Min charge hold (frames)
    MoveCategory Category;                 // Normal, Special, Super
}
```

### CancelWindow
```csharp
class CancelWindow {
    int Start;                // Window start frame (relative to move timeline)
    int End;                  // Window end frame
    string TargetCategory;    // "special", "super", "normal"
}
```

### MatchResult
```csharp
// Output of input matching — one per matched move
struct MatchResult {
    string MoveId;
    ButtonValue RequiredButton;
    int MatchedAtFrame;
    MoveCategory Category;
    int SequenceLength;       // Used for priority tiebreaking
    bool IsChargeMove;
}
```

## Enumerations

| Enum | Values |
|------|--------|
| `InputType` | `Directional`, `Button` |
| `ButtonValue` | `LP`, `MP`, `HP`, `LK`, `MK`, `HK` |
| `DirectionValue` | `Neutral(5)`, `Forward(6)`, `Back(4)`, `Down(2)`, `Up(8)`, `DownForward(3)`, `DownBack(1)`, `UpForward(9)`, `UpBack(7)` |
| `MoveCategory` | `Normal`, `Special`, `Super` |

## Event Types (Data Transfer)

All events are `struct` types in `FTG_Framework.Core.Events` namespace. They carry frame-accurate data between subsystems:

| Event | Key Fields |
|-------|-----------|
| `FrameAdvancedEvent` | `FrameNumber` |
| `InputReceivedEvent` | `PlayerId`, `Entry` (InputEntry) |
| `InputBufferExpiredEvent` | `PlayerId`, `ButtonValue`, `BufferedAtFrame` |
| `ChargeStateChangedEvent` | `PlayerId`, `Direction`, `IsCharged`, `HeldDuration` |
| `MoveFrameChangedEvent` | `PlayerId`, `MoveId`, `CurrentFrame`, `Phase` |
| `CancelWindowEnteredEvent` | `PlayerId`, `MoveId`, `WindowStart`, `WindowEnd`, `TargetCategory` |
| `CancelWindowExitedEvent` | `PlayerId`, `MoveId`, `TargetCategory` |
| `HitConnectedEvent` | `AttackerId`, `DefenderId`, `MoveId`, `HitAdvantage` |
| `MoveBlockedEvent` | `AttackerId`, `DefenderId`, `MoveId`, `BlockAdvantage` |
| `ComboStartedEvent` | `PlayerId`, `InitiatingMove`, `HitCount` |
| `MoveCanceledEvent` | `PlayerId`, `FromMove`, `ToMove`, `WindowCategory` |
| `ComboEndedEvent` | `PlayerId`, `TotalHits`, `FinalMove` |

## JSON Format (example_moves.json)

```json
[
  {
    "move_id": "fireball_p",
    "startup": 7,
    "active": 3,
    "recovery": 15,
    "hit_advantage": 2,
    "block_advantage": -5,
    "damage": 65,
    "cancel_windows": [],
    "chain_repeatable": false
  }
]
```

## Data Store API

```csharp
interface IDataStore {
    MoveDefinition? GetMove(string moveId);
    IReadOnlyList<MoveDefinition> GetAllMoves();
    // Future (Epic 3):
    // GatlingTable? GetGatlingTable(string characterId);
}
```
