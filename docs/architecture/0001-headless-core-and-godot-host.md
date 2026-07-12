# ADR 0001: Headless core and Godot presentation host

Status: Accepted for Phase 0.1

## Decision

Project Emergence uses headless .NET libraries for authoritative future logic and Godot as a presentation host. Core assemblies cannot reference Godot; architecture tests enforce the reference directions. The App owns only shell state, build metadata presentation, and application diagnostics.

The initial future direction is CPU-authoritative. A scalable design must not use one Godot node per future cell. Phase 0.1 contains no biology, world state, simulation loop, biological RNG, or placeholder life animation.

## Consequences

Headless tools and automated tests can operate without the engine. Godot-specific runtime details remain in the App. Future phase decisions must preserve the dependency boundary or replace this ADR explicitly.
