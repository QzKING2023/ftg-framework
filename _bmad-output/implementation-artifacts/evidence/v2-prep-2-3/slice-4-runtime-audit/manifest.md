# PREP-2.3 Slice 4 Evidence Manifest

- Scope: completed-runtime audit and corrected AD-13 replay contract
- Entry baseline: `cddba608b099b00e7a0f25cc5d0e635286ff00a4`
- Depends on: accepted Slice 3
- State: accepted
- Reviewer: Codex implementation validator
- Review date: 2026-08-01

| Command | Result |
|---|---|
| Replay-focused tests | 120+ passed, 0 failed, 0 skipped |
| `dotnet test ... --filter FullyQualifiedName~FtgCliTests --no-restore` | 58 passed, 0 failed, 0 skipped |
| Deterministic stress seeds 2202-2206 | 20/20 passed, no timeout |
| Godot 4.5.1 mono `--headless --quit-after 10` | exit 0; runtime modules and training scene initialized |
| Full regression | 844 passed, 0 failed, 0 skipped |

Godot reported the existing 64×64 viewport and exit-time ObjectDB warnings; neither affected startup or the audited contracts. See `adoption-matrix.md`.
