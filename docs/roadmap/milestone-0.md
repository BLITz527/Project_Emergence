# Milestone 0

Status: accepted baseline completed through Phase 0.5R. Milestone 1 work preserves every locked vector and package V1 behavior below.

## Phase 0.1 / 0.1R - accepted baseline

Repository/toolchain, diagnostics, Godot shell, packaging, tests, imported design baseline, and hardened review evidence.

## Phase 0.2 - foundational domain types (accepted)

Typed IDs, wide logical time, checked counters, exact quantities, SHA-256/canonical encoding, immutable configuration, structured results, durable JSON, and locked vectors.

## Phase 0.3 - deterministic RNG and ruleset registry (accepted)

Explicit 256-bit seeds, addressed RNG, exact vectors, unbiased bounded sampling, domain/algorithm catalogs, strict rulesets/registry, bounded loading, and one nonbiological reference ruleset.

## Phase 0.4 / 0.4R - world session and deterministic scheduler (accepted)

Immutable definition, single-owner in-memory session, six-phase scheduler, bounded commands, transactional event commitment, deterministic vectors, immutable presentation, and hardened callback transaction boundaries.

## Phase 0.5 / 0.5R - coherent snapshots and crash-recoverable atomic save/load

Current scope: strict V2 definitions, coherent Paused/Faulted snapshots, exact compatibility checks, callback-free restore, bounded three-entry world packages, atomic replacement, deterministic recovery, deterministic RNG/command/event continuation, CLI/App/package workflows, and independent review evidence. Phase 0.5R corrects lock ownership to use a live exclusive OS-handle lease, makes stale ordinary lock files reacquirable, keeps active contention fail-closed, and reports post-commit cleanup warnings without negating committed state. The package format, recovery candidate order, and all deterministic vectors remain unchanged.

## Deferred

Event-history replay, migrations, branching, rollback, incremental/chunk stores, networking/cloud persistence, autosave, multiple slots, Phase 1 environmental fields, and all biological implementation remain later work.
