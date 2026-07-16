# ADR 0009: Command intake and transactional event commitment

Status: Accepted for Phase 0.4

## Decision

Commands are accepted only while `Paused` or `Ready`, validated against an explicit immutable processor registry, assigned an instance-scoped checked acceptance sequence, and queued by execute tick then acceptance sequence. Submission never executes a command, consumes no wall-clock value, and a rejected submission consumes no sequence. Only commands exactly due at the current tick enter a step; past-due input is an invariant fault.

Processors stage immutable `WorldEventProposal` values. After all systems succeed, proposals are canonicalized independently of collection/invocation order, duplicate proposal keys and limits are checked, and committed events receive consecutive sequence numbers and canonical hash-derived IDs. The commit, due-command removal, state fingerprint, receipt, and tick advance are one atomic boundary. On any failure none of them occurs.

## Consequences

Only committed immutable events escape. Events record committed transitions but are not the sole store of current state and the session retains no unbounded event log. Command acceptance order is authoritative technical intake order, not a future biological fairness policy. No arbitrary scripts, network commands, or caller-supplied sequence numbers are supported.
