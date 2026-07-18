# ADR 0014: Save/load compatibility and restoration boundary

Status: Accepted for Phase 0.5

## Decision

Load is a fail-closed pipeline: bounded package parse, exact manifest/hash/cross-document validation, V2 definition validation, ruleset and Phase05 algorithm identity, command-processor catalog identity, scheduler system descriptors/graph identity, shared state-digest recomputation, then callback-free construction of a new `WorldSession`.

The runtime supplies registered systems and processors. These are reattached code compatibility dependencies and are never loaded from package data. Restore preserves world/branch/ruleset identity, root seed, logical tick, Paused/Faulted status, last command/event sequences, pending commands, and fault issues. It does not execute processors/systems, reroll RNG, reset counters, resume Ready, or mutate an existing session on failure.

Addressed RNG has no hidden cursor. Exact continuation follows from the preserved root seed, domain/algorithm/ruleset identities, and stable explicit sample addresses. The CLI fixture verifies identical next command sequence, continuation EventIds, final state, and persistence trace.

## Consequences

Compatibility is exact; no migration or best-effort reinterpretation occurs. The App assigns a restored candidate only after the entire pipeline succeeds, so failed load leaves the current session unchanged. No background simulation occurs while closed.
