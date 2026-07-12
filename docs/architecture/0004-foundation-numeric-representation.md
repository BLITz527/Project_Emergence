# ADR 0004: Foundation numeric representation

Status: Accepted for Phase 0.2

## Decision

Stable IDs use two unsigned 64-bit parts and canonical 32-character lowercase hexadecimal text. Zero is representable but empty and invalid in authoritative identity records.

Absolute logical ticks, tick durations, and sequence numbers use UInt128. Counters issue one first, store MaxValue as the last successful value, and fail rather than wrap. Time has no wall-clock conversion.

Matter and energy use separate UInt64 raw-quanta types. Their physical quantum scale is deferred. Addition/subtraction are checked; multiplication uses a UInt128 intermediate before checked narrowing. Authoritative numeric JSON uses invariant decimal strings so no JavaScript-number precision is implied.

Configuration decimals use `System.Decimal` canonical `G29` text and normalize negative zero. Floating-point values are not accepted in authoritative Phase 0.2 foundation data.

## Consequences

Overflow and underflow are explicit programming failures, with `TrySubtract` and `TryIssueNext` supplied for expected boundary handling. No implicit cross-type conversions exist.
