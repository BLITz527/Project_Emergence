# ADR 0010: Immutable presentation snapshot boundary

Status: Accepted with Phase 0.4R hardening

## Decision

Presentation.Contracts defines immutable non-Godot DTOs. Simulation converts committed session state and a supplied tick receipt into `SessionPresentationSnapshot`; no authoritative state lives in the DTO assembly. A receipt carries the immutable producing `SessionDefinitionDigest`, and snapshot production rejects failed, stale, or cross-definition receipts even when tick and event counters coincide. A snapshot carries identity, tick, lifecycle status, ruleset/session/state digests, explicit `HasBiologicalState=false`, and immutable event summaries.

Snapshot creation cannot step the session, consume RNG, allocate command/event sequences, or mutate the state fingerprint. Repeated snapshots may differ only by an explicit presentation sequence. The App renders a real paused-at-zero snapshot but cannot feed selection, rendering frames, focus, resizing, or dropped/replaced snapshots back into simulation state.

## Consequences

Rendering is eventually-consistent presentation, not ownership. Snapshots may be skipped or discarded without simulation effect. Phase 0.4 displays truthful nonbiological state and provides no fake-life animation.
