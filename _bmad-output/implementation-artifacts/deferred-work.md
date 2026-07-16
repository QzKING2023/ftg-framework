## Deferred from: code review of 1-1-project-scaffold-core-infrastructure (2026-07-15)

- Autoload path case mismatch (`res://scripts/` vs `res://Scripts/`) in project.godot — FrameRateManager path is pre-existing; not caused by this change.
- Magic integers instead of enums in event types (InputType, InputValue, Direction) — defer to Input System stories (1.3+).
- _frameNumber integer overflow in GameLoop.cs — wraps after ~414 days at 60fps. Not a practical concern for V1.