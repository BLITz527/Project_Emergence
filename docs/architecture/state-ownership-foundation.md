# State ownership foundation

Foundation owns immutable build metadata, diagnostics, deterministic value primitives, immutable configuration documents, and operation-result values. `CheckedSequenceCounter` is explicitly instance-scoped state whose serialized value is only its last issued sequence; no global allocator exists.

`WorldIdentity` and `BranchIdentity` reject empty IDs and represent identity only. Typed entity IDs create no entity stores or behaviors. Model owns immutable session definitions, phase graphs, commands, proposals, committed-event contracts, and receipts. Simulation alone owns the mutable, instance-scoped `WorldSession`; it is deliberately single-thread-owned, guarded against concurrent/reentrant stepping, and is never a global current-world singleton.

`DeterministicAddressedRng` is immutable session-scoped input (root seed plus allowed-domain catalog), not a global service and not a mutable stream. RNG addresses carry explicit domain, scope, and sample index; callers own stable index assignment. Ruleset descriptors and registries are immutable values; Persistence constructs a registry only after every bounded untrusted input validates.

Command acceptance order is authoritative technical input order. Processors receive a restricted execution context, stage proposals, and cannot mutate the session. A successful tick commits canonicalized events atomically; a failed tick advances nothing, consumes no due command or event sequence, exposes no partial event, and moves the session to `Faulted`. Events describe committed transitions but are not the sole current-state store. Pausing prevents stepping and never changes logical time.

Presentation.Contracts contains immutable DTOs only. Snapshot production reads committed state and the latest receipt without consuming RNG, command/event sequence, or logical time; snapshots may be dropped or replaced without simulation effect. App owns presentation state only and never authoritative state. Phase 0.4 defines no biology, long-term history, recovery, or save format; coherent persistence begins in Phase 0.5.
