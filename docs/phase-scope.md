# Phase 0.4 scope

Phase 0.4 preserves every accepted vector and adds an authoritative in-memory `WorldSession`, immutable `WorldSessionDefinition`, formal six-phase deterministic `SchedulerGraph`, bounded safe-boundary command intake, transactional event commitment, state/trace fingerprints, and immutable presentation snapshots. Execution is explicitly single-threaded and session-scoped. Pausing stops logical time; serious invariant failures fault without advancing the tick, consuming due commands/event sequence, or exposing partial events.

Commands are ordered by execute tick then authoritative acceptance sequence. That technical intake order is not a future biological fairness mechanism: later biological contention will use staged intents and fair batched resolution. Events are immutable committed outputs, but current state is not reconstructed solely from events. Presentation snapshots may be replaced or dropped without changing simulation state.

Out of scope are persistent snapshots/save-load/recovery (Phase 0.5), session branching and rollback, long-term event history, networking, plugins or reflection discovery, multithreaded execution, biological RNG domains, genomes, cells, regions, fields, metabolism, mutation, movement, ecology, adaptive fidelity, and UI redesign. No biological simulation exists.
