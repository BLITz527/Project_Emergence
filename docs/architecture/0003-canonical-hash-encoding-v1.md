# ADR 0003: Canonical hash encoding V1

Status: Accepted for Phase 0.2

## Decision

Durable foundation digests use `CanonicalHashWriter` algorithm `foundation.canonical-hash@1.0.0`. Every stream begins with ASCII `PE-CANONICAL/1` and a zero byte. Values then use unambiguous tags: `01` UTF-8 string plus UInt64 little-endian byte length, `02` bytes plus length, `03` UInt64 little-endian, `04` UInt128 little-endian, `05` Boolean byte, and `06` raw 32-byte SHA-256 digest. Null is unsupported. Finalization seals the writer.

The golden sequence `"Project Emergence"`, UInt64 42, UInt128 `2^80+7`, true, and bytes `00 ff 10` hashes to `82c8ccdd15e3c521c298553e1fc02360048f5a115ad96dc6d82802e244a7c370`.

Algorithm catalogs write domain marker `ProjectEmergence.AlgorithmCatalog.v1`, entry count, then ordinally sorted algorithm ID/version strings.

Configurations write, in order: string domain marker `ProjectEmergence.Configuration.v1`; schema ID string; schema-version string; UInt64 entry count; then each ordinal key, kind-name string, and selected value. Boolean uses the Boolean tag, UInt64 the UInt64 tag, Digest the digest tag; Int64/Decimal/String use canonical strings. The Phase 0.2 fixture hashes to `75b8257ce1bbcf5599165648ea4601e64029afb562667639e271dfde14bc2cb5`.

## Consequences

Type tags prevent concatenation ambiguity; ordinal ordering removes insertion-order influence; callers own Unicode normalization. Any incompatible encoding requires a new registered algorithm version.
