# PREP-2.3 Runtime Adoption Matrix

| Boundary | Decision | Evidence |
|---|---|---|
| GameLoop / AD-12 | accepted with evidence | Full suite and Godot runtime preserve frame, pause, replay, and physics ownership. |
| EventBus / AD-12, AD-18, AD-20 | corrected and accepted in PREP-2.3 | Phase order, immutable payloads, epochs, quiescence, restore notification, queue metadata. |
| Replay / AD-13, AD-20 | corrected and accepted in PREP-2.3 | Exhaustive registry, phase/policy metadata, `ReplayCodec`, atomic rejection, suppression, v3 compatibility, mode tests. |
| SceneManager | accepted with evidence | Lifecycle/subscription cleanup suites and full regression. |
| FileWatcher / AD-15 | accepted with evidence | Deterministic canonical-path observation and main-thread handoff suites. |
| Data / AD-15, AD-19 | corrected and accepted in PREP-2.3 | Presence/schema/cross-reference matrix, optimistic version, migration, atomic persistence. |
| Input | accepted with evidence | Required versioned snapshot adapter; SOCD/history/buffer/charge regression green. |
| FrameData | corrected and accepted in PREP-2.3 | MoveStarted order; legacy rewind API preserved; versioned adapter covered. |
| Physics | accepted with evidence | Motion discriminator and generation graph-validation boundary; PREP-2.1 tuple tests green. |
| StateMachine | corrected and accepted in PREP-2.3 | Immutable stack values, participant adapter, lifecycle/generation tests green. |
| Combo | accepted with evidence | Catalog policy and no-duplicate derived publication; owner regressions green. |
| Training/replay modes | corrected and accepted in PREP-2.3 | Playback modes are mutually exclusive per epoch. |
| Runtime/scaffold parity | accepted with evidence | `FtgCliTests`: 58 passed; Godot 4.5.1 headless: exit 0. |

Epic 3 product scope remains closed.
