# ADR 0018: Environment world package V2

Status: Accepted for Milestone 1 Phase 1.1

## Decision

Package V1 remains unchanged. Package V2 contains definition JSON, snapshot metadata with canonical chunk descriptors, every field chunk, then a V2 manifest. The reader validates metadata, manifest paths/lengths/hashes, bounded chunks, reconstructed region/environment/session/snapshot digests, and compatibility before returning a document.

## Consequences

Missing, duplicate, unknown, swapped, cross-region, corrupt, or extra chunks yield no partial session. Old packages are never silently upgraded. Phase 0.5R atomic replacement, live-handle lease, recovery, and sidecar rules remain unchanged.
