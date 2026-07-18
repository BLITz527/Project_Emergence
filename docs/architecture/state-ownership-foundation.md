# State ownership foundation

Foundation owns immutable metadata, diagnostics, deterministic value primitives, strict UTF-8/canonical hashing, immutable configuration, addressed RNG inputs, and result values. Counters are instance scoped; no global allocator or RNG stream exists.

Model owns immutable session definitions, command-processor catalogs, scheduler graphs, accepted commands, events, receipts, and `WorldSessionSnapshot`. A snapshot is data, not a live session. It defensively owns canonical pending commands and bounded fault issues and binds them to a state digest and snapshot digest.

Simulation alone owns mutable `WorldSession`. It is single-thread-owned and transaction guarded. Capture is allowed only at a committed Paused/Faulted boundary and changes no state. Restore validates compatibility before constructing a separate session with the exact tick, lifecycle, last-issued command/event sequences, pending commands, and fault issues. It does not execute callbacks, reroll a seed, or mutate an existing session on failure.

Systems and command processors are stateless code dependencies, not serialized state. Compatibility reattaches exact registered types/catalog identities. Events record committed transitions but event history is not a current-state store and is not persisted.

Persistence owns filesystem/ZIP transport and recovery state, not authoritative simulation behavior. Package semantic documents remain Model values. `.writing`, `.previous`, `.lock`, and `.corrupt` are protocol sidecars. A valid target is authoritative; an invalid target is never overwritten without quarantine and a valid replacement. Candidate timestamps never confer authority.

Presentation snapshots remain disposable immutable views. App owns UI state and the configured `user://saves/foundation-session.emergence-world` path only. Rendering, frame callbacks, closed-app time, failed loads, and dropped presentation snapshots cannot advance or replace authoritative session state.
