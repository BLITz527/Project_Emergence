# ADR 0016: Exact environmental amounts and effective volume

Status: Accepted for Milestone 1 Phase 1.1

## Decision

Authoritative field matter and effective volume use separate exact UInt64 quantity types. Zero effective volume means solid and requires every field amount to be zero. Concentration is the derived amount/volume relation and is unavailable at zero volume; it is never stored, clamped, or evolved.

## Consequences

Totals use checked UInt128 accumulation. Scientific probes report raw amount and volume. Controlled floating point exists only in disposable presentation normalization.
