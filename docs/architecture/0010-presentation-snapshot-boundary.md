# ADR 0010: Immutable presentation snapshot boundary

Status: Accepted for Phase 0.4

## Decision

Presentation.Contracts defines immutable non-Godot DTOs. Simulation converts committed session state and the latest tick receipt into `SessionPresentationSnapshot`; no authoritative state lives in the DTO assembly. A snapshot carries identity, tick, lifecycle status, ruleset/session/state digests, explicit `HasBiologicalState=false`, and immutable event summaries.

Snapshot creation cannot step the session, consume RNG, allocate command/event sequences, or mutate the state fingerprint. Repeated snapshots may differ only by an explicit presentation sequence. The App renders a real paused-at-zero snapshot but cannot feed selection, rendering frames, focus, resizing, or dropped/replaced snapshots back into simulation state.

## Consequences

Rendering is eventually-consistent presentation, not ownership. Snapshots may be skipped or discarded without simulation effect. Phase 0.4 displays truthful nonbiological state and provides no fake-life animation.
