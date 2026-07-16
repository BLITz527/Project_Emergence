# ADR 0007: Authoritative world-session ownership and lifecycle

Status: Accepted for Phase 0.4

## Decision

`WorldSessionDefinition` is an immutable Model value binding world/branch identity, exact ruleset registry entry and digests, root seed, Phase 0.4 algorithm catalog, and scheduler graph. `WorldSession` is mutable execution state owned by Simulation. Each instance starts `Paused` at tick zero, is explicitly single-thread-owned, and guards concurrent or reentrant stepping. There is no global current-world/session singleton and no Godot-owned authoritative state.

`Paused` prevents stepping without changing logical time. `Ready` permits exactly one-tick steps. A serious invariant failure transitions atomically to `Faulted`; the tick, due-command queue, event sequence, and committed state remain unchanged, no partial event escapes, and Phase 0.4 provides no reset or recovery. Tick exhaustion fails before execution.

## Consequences

The session is authoritative in memory but is not a save file. Its bounded latest receipt is diagnostic/presentation state, not an unbounded history engine. Coherent snapshots, save/load, checksums, recovery, branching persistence, and complete session/RNG persistence belong to Phase 0.5 or later. No biological state is introduced.
