# ADR 0008: Deterministic scheduler phase graph

Status: Accepted for Phase 0.4

## Decision

Reference execution is single-threaded and uses the fixed ordinal phase order `Commands`, `Prepare`, `Evaluate`, `Resolve`, `Commit`, `Finalize`. Systems are registered explicitly; reflection/plugin discovery is forbidden. Each immutable descriptor has a typed ID, one phase, and sorted same-phase dependencies. `SchedulerGraph` rejects missing/cross-phase dependencies and cycles, then resolves ready ties by smallest ordinal system ID. Insertion order cannot affect execution order or graph digest.

Processors receive restricted contexts, addressed RNG access, due commands where appropriate, and proposal sinks. They never receive `WorldSession` or mutable authoritative collections. Wall clock, frame delta, thread-pool ordering, storage enumeration, and nondeterministic ID generation are excluded.

## Consequences

Scheduler order is a technical dependency order only. It must never become a hidden biological advantage. Later resource contention will stage intents and resolve them in fair deterministic batches rather than using processor invocation order. Phase 0.4 systems are synthetic and nonbiological.
