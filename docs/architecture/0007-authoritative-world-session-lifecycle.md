# ADR 0007: Authoritative world-session ownership and lifecycle

Status: Accepted with Phase 0.4R hardening

## Decision

`WorldSessionDefinition` is an immutable Model value binding world/branch identity, exact ruleset registry entry and digests, root seed, Phase 0.4 algorithm catalog, and scheduler graph. `WorldSession` is mutable execution state owned by Simulation. Each instance starts `Paused` at tick zero, is explicitly single-thread-owned, and guards concurrent or reentrant stepping. There is no global current-world/session singleton and no Godot-owned authoritative state.

`Paused` prevents stepping without changing logical time. `Ready` permits exactly one-tick steps. While a tick is active, owner-thread `Pause`, `Resume`, `SubmitCommand`, and nested `StepOneTick` calls are rejected without changing status, queues, sequences, tick, or committed state; the attempt marks the outer transaction for a Critical fault before commit. Wrong-thread calls remain rejected without affecting the owner transaction. Other serious invariant failures are likewise atomic, and Phase 0.4R provides no reset or recovery.

## Consequences

The session is authoritative in memory and is never serialized as a save object. Its bounded latest receipt is diagnostic/presentation state, not an unbounded history engine. Phase 0.5 layers a separate coherent snapshot and package contract over committed Paused/Faulted state; branching persistence and event-history replay remain later work. No biological state is introduced.
