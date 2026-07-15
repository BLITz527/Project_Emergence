# ADR 0006: Ruleset manifest and registry V1

Status: Accepted for Phase 0.3

## Decision

A ruleset key identifies exactly one nonempty `RulesetId` and semantic version. A V1 descriptor binds its exact key and display name to algorithm-catalog, RNG-domain-catalog, and immutable-configuration digests. A registry stores descriptors in key order and binds each ID, version, and descriptor digest. There is no latest-version lookup, compatibility fallback, alias, inheritance, include, migration, hot reload, scripting, reflection activation, plugin, or network retrieval.

Ruleset JSON is redundant and fail-closed: every digest is recomputed; duplicate, unknown, and missing properties are rejected. Persistence alone owns filesystem loading. The directory loader examines top-level `*.ruleset.json` files in ordinal filename order and enforces 256 files, 1 MiB per file, 8 MiB total, JSON depth 32, strict BOM-free UTF-8, no comments/trailing commas, direct-child paths, and no reparse-point inputs. Any error returns structured filename/reason evidence and no partial registry.

## Consequences

The tracked `foundation-reference.ruleset.json` is a nonbiological validation artifact. Its descriptor digest is `365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086`; the one-entry registry digest is `0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d`. Source and packaged Apps validate the same file through Persistence.
