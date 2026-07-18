# Phase 0.5 scope

Phase 0.5 preserves all accepted Phase 0.1-0.4R vectors and adds the first supported persistent foundation session. `WorldSessionSnapshot` is an immutable coherent view of committed authoritative state. Capture is permitted only while Paused or Faulted, does not mutate the session, and records identity, definition, logical tick, status, command/event counters, canonical pending commands, bounded fault issues, and state digest. Ready and active-tick capture fail closed.

`WorldSessionDefinition` V1 remains an exact regression format and is not saveable. V2 is the first saveable definition and binds the Phase05 algorithm catalog plus the immutable command-processor catalog. Restore validates the ruleset, algorithm catalog, command processors, scheduler systems, graph, definition, state, and snapshot before constructing a new session. Systems and processors are reattached code dependencies; they, delegates, reflection metadata, and event history are never serialized. Restore invokes no callback and performs no RNG draw.

The `.emergence-world` package is a bounded ZIP transport with exactly `definition.json`, `snapshot.json`, and `package-manifest.json` in that order. Strict UTF-8 without BOM, strict JSON schemas, redundant hashes/lengths, semantic digests, and cross-document identities are validated. ZIP bytes are transport; canonical semantic digests are authoritative. Addressed RNG has no mutable hidden cursor, so root seed plus algorithm/domain identity preserve its continuation.

Writes use `.writing`, `.previous`, and `.lock`; invalid displaced targets may be quarantined as `.corrupt`. Staging is flushed and semantically re-read before replacement. Recovery validates candidates and follows a fixed decision table without timestamps. Success leaves no temporary sidecars.

The App provides one manual save location, `user://saves/foundation-session.emergence-world`, with Save, Load, and Verify controls. Failed load leaves the current session unchanged. No background simulation runs while the App is closed.

Out of scope are event-history persistence/replay, migrations, branching/forking/rollback, incremental or chunk stores, cloud/network saves, autosave, multiple slots, encryption, dynamic code loading, Phase 1 fields, and every biological domain. No cells, organisms, genomes, fields, regions, bonds, ecology, metabolism, reproduction, death, or fake-life visuals exist.
