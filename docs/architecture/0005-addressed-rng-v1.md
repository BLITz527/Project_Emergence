# ADR 0005: Addressed RNG V1

Status: Accepted for Phase 0.3

## Decision

Random sampling is an immutable function of a 256-bit explicit root seed and a typed address: exact registered domain, nonempty 128-bit scope, UInt128 sample index, and internal rejection-attempt index. `DeterministicAddressedRng` contains no cursor, stream position, global instance, entropy factory, clock, GUID, or operating-system random input.

The V1 block algorithm is `foundation.rng-addressed-sha256@1.0.0`. It hashes the CanonicalHashWriter V1-equivalent sequence `ProjectEmergence.RngBlock.v1`, seed bytes, domain, scope high/low, sample index, and attempt. The production hot path emits those tags and little-endian fields into a bounded stack buffer before `SHA256.HashData`.

The locked primary fixture block is `8c39412c47d92f7367ae49de9f122d232aa011d2442e393572265bdc231a34e7`; lane zero is `8300091537975490956`. Bounded UInt64 sampling uses rejection threshold `unchecked(0UL - bound) % bound` and checked attempt increment.

## Consequences

Request order, rendering, storage order, threading, and unrelated domains cannot shift an existing address. Callers must assign stable sample indices; this phase supplies no scheduler or biological policy. Only the three foundation domains are registered, so no biological randomness exists.
