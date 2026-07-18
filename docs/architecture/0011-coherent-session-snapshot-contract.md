# ADR 0011: Coherent session snapshot contract

Status: Accepted for Phase 0.5

## Decision

`WorldSessionSnapshot` format 1.0.0 is an immutable Model value captured by Simulation only while a session is Paused or Faulted and no tick transaction is active. It contains the exact V2 definition, logical tick, lifecycle status, last-issued command/event sequences, canonically ordered pending commands, bounded fault issues, state digest, and snapshot digest. Ready sessions are not coherent persistent checkpoints and cannot be captured.

Capture acquires the owner/transaction boundary, copies all collections, recomputes state through the shared Model fingerprint contract, and performs no mutation, callback, RNG draw, counter allocation, or logical-time advance. Paused snapshots have no fault issues; Faulted snapshots have at least one. Pending command execution ticks cannot precede the current tick and accepted sequence numbers are unique and bounded by the recorded last command sequence.

V1 definition 1.0.0 remains an exact regression format. V2 definition 2.0.0 is the first saveable definition because it binds the Phase05 algorithms and the command-processor compatibility catalog.

## Consequences

A snapshot is coherent reconstructible state, not a serialized `WorldSession`. Systems, processors, delegates, runtime types, and event history are excluded. Event-history replay, incremental snapshots, branching, rollback, and migration are deferred.
