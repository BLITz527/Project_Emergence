# ADR 0009: Command intake and transactional event commitment

Status: Accepted with Phase 0.4R hardening

## Decision

Commands are accepted only while no tick transaction is active and the session is `Paused` or `Ready`, validated against an explicit immutable processor registry, assigned an instance-scoped checked acceptance sequence, and queued by execute tick then acceptance sequence. Submission never executes a command, consumes no wall-clock value, and a rejected submission consumes no sequence. A callback submission is rejected and faults the outer transaction before commit. Only the originally captured due-command sequences are removed at commit; past-due input is an invariant fault.

Processors stage immutable `WorldEventProposal` values. Information and Warning issues from successful callbacks are copied into the receipt in callback order, bounded at 128, and excluded from authoritative state/history. Error or Critical issues fail the transaction. After all systems succeed, proposals are canonicalized independently of collection/invocation order, duplicate proposal keys and limits are checked, and committed events receive consecutive sequence numbers and canonical hash-derived IDs. The commit, due-command removal, state fingerprint, receipt, and tick advance are one atomic boundary. On any failure none of them occurs. `IssueSeverity` is a closed four-value set with exact-string JSON.

## Consequences

Only committed immutable events escape. Events record committed transitions but are not the sole store of current state and the session retains no unbounded event log. Command acceptance order is authoritative technical intake order, not a future biological fairness policy. No arbitrary scripts, network commands, or caller-supplied sequence numbers are supported.
