# PROJECT EMERGENCE
## Version 1.0 Design Archive

**Archive edition:** 1.0  
**Design status:** Complete baseline for implementation  
**Archive date:** 2026-07-12  
**Creative Director and final acceptance authority:** Timothy Nitz  
**Design, production, architecture, and QA synthesis:** ChatGPT project team  

> This archive is a consolidated authoritative edition of the accepted design. It preserves the locked decisions and implementation requirements from the design conversation in a durable project format. It is not intended as a verbatim chat transcript.

# Design Pass 14 — Long-Time Simulation, Adaptive Fidelity, Historical Compression, Replay, and Persistence

## Four Separate Concepts

1. **Simulation speed** — real time needed to advance world time.
2. **Simulation fidelity** — how explicitly biology is represented.
3. **Historical detail** — how much evidence about the past is retained.
4. **Rendering detail** — what is currently drawn.

They must never be treated as one setting.

## Locked Decisions

- Current authoritative state contains everything required to continue correctly.
- Historical records are evidence and may be compressed without changing current biology.
- Organism memory is in current biological state, not in analytical history.
- One exact logical timeline spans all regions and processes.
- Multi-rate scheduling is permitted only with formal phase ordering.
- Fast-forward cannot blindly multiply timestep.
- Temporal skipping requires demonstrated equivalence.
- Important event boundaries must be resolved.
- Simulation, analysis, and presentation RNG are isolated.
- Full explicit simulation is the biological reference.
- Lower fidelity uses formal contracts defining represented variables, invariants, unsupported phenomena, error envelope, and conversion rules.
- Preserved invariants include matter, energy, heredity, population, migration, mortality, reproduction, environment, and intervention history.
- Novelty, instability, invasion, collapse, colony formation, multicellularity, learning, experimentation, and rare-lineage risk may trigger promotion.
- Demotion is conservative and uses stability windows and hysteresis.
- Player preferences affect computation, not biology.
- Rare lineages cannot be rounded out of existence, though they may truly go extinct.
- Notable individuals may remain explicit within aggregated regions.
- Mixed-fidelity interaction must prevent double counting.
- Rehydration reconstructs a current microstate consistent with stored distributions; it cannot recover discarded exact detail.
- Rehydration cannot invent genotype, learning, development, or ecological relationships.
- Complex learned or multicellular organisms may require specialized abstraction or remain explicit.
- History uses rolling exact, event-resolved, aggregate, and permanent-provenance layers.
- Compression is adaptive and event-aware, preserving peaks, collapses, transitions, branches, bookmarks, and distributions where means are insufficient.
- Extant genotypes retain traceable ancestry.
- Canonical genomes, mutation deltas, and checkpoints reduce storage.
- Exact replay, deterministic resimulation, reconstructed replay, and summary-only history are distinct.
- Bookmarks may protect detail at visible storage cost.
- Snapshots capture coherent authoritative state.
- Periodic full snapshots bound incremental dependency chains.
- Branches share immutable pre-divergence data.
- Saves use manifests, checksums, staged writes, atomic commit, journals, recovery, and independent backups.
- Storage policies affect records rather than biology.
- Archival preserves current state, major history, rulesets, and provenance.
- Worlds bind to versioned rulesets and algorithm identities.
- Ruleset migration creates an explicit transformed branch.
- Schema migration is separate from biological migration.
- Save/load preserves learned state, development, fidelity state, scheduled events, and all RNG streams.
- Save/load continuation matches uninterrupted deterministic execution.
- Long runs require wide integer time and IDs, numeric drift tests, conservation audits, integrity checks, and fault containment.
- Performance reporting is scenario-specific.
- Resource pressure first reduces rendering, analytics, history, or validated fidelity—not living populations.
- Unsafe continuation pauses and preserves recovery evidence.

## Validation Requirements

- repeated deterministic fast-forward;
- save/load equivalence;
- full versus reduced differential runs;
- rare-lineage handling;
- mixed-fidelity transfer;
- repeated promote/demote/rehydrate cycles;
- history compression preserving major events;
- exact versus reconstructed replay;
- interrupted save recovery;
- corrupted manifest and chunk behavior;
- ruleset and schema migration;
- long soak tests;
- storage growth;
- conservation over deep time.

## Acceptance Statement

Deep time is valid only when the world may become ancient without fast-forward, compression, storage, rendering, or fidelity systems silently changing what life is allowed to do.
