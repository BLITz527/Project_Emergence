# ADR 0015: Hidden field lattice and one-region ownership

Status: Accepted for Milestone 1 Phase 1.1

## Decision

A V3 `WorldSessionDefinition` binds exactly one immutable `EnvironmentDefinition`, which binds exactly one rectangular `RegionLatticeDefinition`. Coordinates map row-major by Y then X. Chunks are traversed RegionId, chunk Y, chunk X. Model owns topology and immutable captures; Simulation owns dense field arrays and the live session owns that store.

## Consequences

The lattice is authoritative even when normal rendering hides its grid. No Godot node, pixel, texture, package path, or interpolated value is a cell identity. Connected regions, topology migration, and field dynamics are later decisions.
