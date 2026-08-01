# Scaffold Boundary Snapshots

- Invalid manifest and direct-call project-name cases assert the destination remains absent.
- The immutable plan is fully constructed before target mutation and rejects duplicate canonical destinations.
- Existing source and destination chains are checked for reparse points before planning and immediately before access.
- Existing cleanup tests preserve user-created content in a pre-existing destination during injected failure.
- Portable limitation retained: a privileged post-check path replacement cannot be made race-free with portable `System.IO`; deterministic pre-existing and hook-time swaps are the supported threat boundary.

