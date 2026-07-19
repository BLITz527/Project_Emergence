# ADR 0013: Atomic save and recovery protocol

Status: Accepted for Phase 0.5; corrected in Phase 0.5R

## Decision

Saving uses sibling files derived from the target: `.writing` for flushed staging, `.previous` for the displaced valid target, `.lock` as the cross-process rendezvous for writer exclusion, and `.corrupt` for quarantined invalid target evidence. Target, parent, and sidecars must be ordinary local filesystem paths rather than directories or reparse points.

The `.lock` path's existence is not ownership. Acquisition opens or creates that ordinary file and obtains a live exclusive operating-system handle without waiting or retrying. A stale empty file or malformed/partial metadata is reacquirable as soon as no exclusive handle owns it. Bounded strict-UTF-8 marker, PID, and opaque-token metadata is diagnostic only; it is not trusted, is not selected by age or modification time, and is not included in any authoritative package or semantic digest. Directory, reparse, symbolic-link, and unavailable lock paths fail closed.

The writer acquires the lease, recovers any interrupted state, writes a new deterministic package to `.writing`, flushes it to disk, reopens it through the bounded semantic reader, moves a valid target to `.previous`, promotes `.writing`, validates the promoted target, then removes `.previous`. Failures preserve a valid old target or a valid recovery candidate. An active lease blocks Save and Recover before recovery inspection. Normal success releases through ownership-safe delete-on-close semantics while the Windows handle remains exclusive, leaving no temporary lock sidecar; there is no close-then-blind-delete window in which an earlier owner can delete a successor's rendezvous.

Recovery validates content and applies this ordered table; timestamps are never consulted:

| Target | `.writing` | `.previous` | Action |
|---|---|---|---|
| valid | any | any | keep target; delete validated stale writing/previous |
| absent | valid | any | promote writing; remove stale previous |
| absent | invalid/absent | valid | remove invalid writing if present; restore previous |
| invalid | any | valid | move target to `.corrupt`; restore previous |
| invalid | valid | absent | move target to `.corrupt`; promote writing |
| no valid candidate | any | any | fail without destructive cleanup |

An existing `.corrupt` conflict blocks quarantine. Recovery requires the lease and reports structured actions/issues. Package/recovery transaction outcomes are separate from lock metadata and cleanup outcomes. A fully promoted and revalidated package or recovered target remains successful if lease cleanup subsequently reports a structured nonfatal warning; its identity, manifest digest, byte count, and recovery actions remain available.

## Consequences

Atomic replacement is expressed as a recoverable protocol rather than an assumption about one filesystem call. Fault injection at each writer boundary must leave a deterministic recoverable state and a reacquirable lock rendezvous. Recovery preserves evidence instead of guessing or selecting the newest file. The Phase 0.5 package/snapshot/definition schemas, semantic digests, deterministic continuation, and recovery candidate table are unchanged by the Phase 0.5R lease correction.
