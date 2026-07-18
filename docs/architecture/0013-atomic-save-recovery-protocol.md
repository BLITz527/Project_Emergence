# ADR 0013: Atomic save and recovery protocol

Status: Accepted for Phase 0.5

## Decision

Saving uses sibling files derived from the target: `.writing` for flushed staging, `.previous` for the displaced valid target, `.lock` for writer exclusion, and `.corrupt` for quarantined invalid target evidence. Target, parent, and sidecars must be ordinary local filesystem paths rather than directories or reparse points.

The writer acquires the lock, recovers any interrupted state, writes a new deterministic package to `.writing`, flushes it to disk, reopens it through the bounded semantic reader, moves a valid target to `.previous`, promotes `.writing`, validates the promoted target, then removes `.previous`. Failures preserve a valid old target or a valid recovery candidate. Success leaves no temporary sidecars.

Recovery validates content and applies this ordered table; timestamps are never consulted:

| Target | `.writing` | `.previous` | Action |
|---|---|---|---|
| valid | any | any | keep target; delete validated stale writing/previous |
| absent | valid | any | promote writing; remove stale previous |
| absent | invalid/absent | valid | remove invalid writing if present; restore previous |
| invalid | any | valid | move target to `.corrupt`; restore previous |
| invalid | valid | absent | move target to `.corrupt`; promote writing |
| no valid candidate | any | any | fail without destructive cleanup |

An existing `.corrupt` conflict blocks quarantine. Recovery requires the lock and reports structured actions/issues.

## Consequences

Atomic replacement is expressed as a recoverable protocol rather than an assumption about one filesystem call. Fault injection at each writer boundary must leave a deterministic recoverable state. Recovery preserves evidence instead of guessing or selecting the newest file.
