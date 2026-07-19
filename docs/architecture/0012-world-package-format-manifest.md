# ADR 0012: World-package format and manifest

Status: Accepted for Phase 0.5

## Decision

The first save format is `.emergence-world` version 1.0.0: a bounded ZIP transport with exactly three root entries in this order:

1. `definition.json`
2. `snapshot.json`
3. `package-manifest.json`

All documents are compact strict UTF-8 without BOM and use closed JSON schemas. The reader rejects unknown/duplicate/missing entries, directory/link/traversal names, duplicate names, malformed Unicode, excessive depth/size/count/uncompressed total, unknown properties, noncanonical values, digest mismatches, and cross-document identity mismatches.

The package identity digest binds format, world/branch, definition/snapshot/state/ruleset/algorithm identities, and the two semantic data paths. The manifest digest additionally binds exact uncompressed lengths and SHA-256 hashes of definition and snapshot. ZIP CRC, timestamps, compression representation, host path, and wall clock are not authoritative. ZIP bytes are transport; canonical semantic digests are authoritative.

Technical limits are 67,108,864 package bytes; exactly three entries; 8,388,608 definition bytes; 50,331,648 snapshot bytes; 1,048,576 manifest bytes; 59,768,832 total uncompressed bytes; and JSON depth 64.

## Consequences

Renaming an arbitrary ZIP does not make a world package. V1 session definitions are rejected for saving. The format is a complete foundation checkpoint, not the later large-world chunk architecture. No automatic migration exists.
