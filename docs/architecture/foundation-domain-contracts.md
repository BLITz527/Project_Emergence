# Foundation domain contracts

## Durable primitives

Typed 128-bit IDs use exact lowercase hexadecimal text. Ticks/spans and checked sequences use canonical UInt128 decimal text. Matter, energy, and volume remain incompatible exact UInt64 quanta. `FieldChannelId` is strict lowercase dotted ASCII and `FieldChannelRole` currently permits only `ConservedMaterial`. Durable JSON is exact and closed; strict UTF-8 rejects BOM, malformed bytes, and invalid surrogate text.

## Algorithms, rulesets, and configuration

IDs use bounded lowercase ASCII dotted segments. Catalogs contain one version per ID, sort ordinally, and carry redundant digests. Phase05 contains the exact seven persistence-era algorithms and has digest `78818c4c6a6a4aeb498a634e4cd77e5854c3fa35be2d075aabb888cb0fe7d9a1`. Immutable configuration and rulesets are data only and cannot execute code.

Phase11 extends, without altering, Phase05 and binds field catalogs, region/environment definitions and state, field chunks, environment session state, V2 packages, and field presentation. Its catalog digest is `b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65`.

Addressed RNG is the pure function of root seed, registered domain, scope, and sample index. It has no mutable cursor to serialize. Persistence records the root seed and algorithm/domain/ruleset identities rather than inventing stream position.

## Session definitions and snapshots

V1 definition `1.0.0` remains exact for regression. V2 `2.0.0` is the first saveable definition and adds the Phase05 runtime algorithm catalog and command-processor catalog digest. `WorldSessionSnapshot` format `1.0.0` permits only Paused/Faulted, includes exact identity/time/counters/queue/fault state, and carries independently recomputable state and snapshot digests.

Session ceilings remain: 256 systems, 64 dependencies/system, 256 command processors, 4,096 pending commands, 1,024 due commands/tick, 4,096 proposals/system/tick, 16,384 events/tick, 128 receipt issues, and 128 fault issues. These are safety limits, never biological capacities.

V3 definition `3.0.0` binds the exact Phase11 catalog and environment-definition digest. Snapshot V2 `2.0.0` binds the exact environment-state digest. The static reference environment uses one 16×12 region, chunk edge 8, base/effective fluid volume 1024, and zero-volume solids. Amounts and volumes are integers; concentration is derived and not stored.

## World packages

World-package format `1.0.0` has exactly three root entries: definition (8,388,608-byte maximum), snapshot (50,331,648), and manifest (1,048,576). The package maximum is 67,108,864 bytes, total uncompressed maximum is 59,768,832 bytes, entry count is exactly three, and JSON depth is at most 64. Compression ratio, traversal, links, reparse points, duplicates, extra entries, BOM, malformed Unicode, unknown properties, noncanonical values, and redundant digest mismatches fail closed.

The package identity binds semantic identity and expected data paths. The manifest digest additionally binds data-document lengths and SHA-256 hashes. These semantic digests—not ZIP timestamp, CRC, compression, host path, or wall clock—are authoritative.

World-package V2 has definition and snapshot JSON, all canonical field chunks, and one manifest. The reference package has seven entries. Chunk integers are little-endian; RegionId and SHA-256 bytes use canonical displayed byte order. V2 limits are 268,435,456 package bytes, 2,097,152 manifest bytes, 16,777,216 bytes per definition/snapshot JSON, 8,388,608 bytes per chunk, 201,326,592 total chunk bytes, and 4,099 entries. Package identity is `a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685`; the locked manifest digest is `b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee`.
