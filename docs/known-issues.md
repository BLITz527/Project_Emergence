# Known issues

- The physical scale of one matter or energy quantum remains undefined pending a later versioned numeric/ruleset decision.
- Algorithm catalogs record one active version per algorithm ID. Compatibility ranges and migrations are deferred.
- Rulesets are immutable and non-executable; inheritance, includes, hot reload, downloads, and migrations are absent.
- Addressed RNG has no hidden cursor, but callers remain responsible for stable domain/scope/sample-index assignment.
- The session retains only the latest bounded tick receipt. Event history is not persisted and saves cannot replay it.
- V1 session definitions remain a regression format. V2 is the first saveable format; no automatic or user-authored migration exists.
- Save/load supports one foundation package path in the App. Autosave, multiple slots, world browsing, cloud sync, branching, and rollback are deferred.
- Recovery is explicit and deterministic. When no valid candidate exists it preserves evidence and fails rather than guessing.
- The package is a complete foundation-session snapshot, not the future large-world incremental/chunk persistence architecture.
- No biological runtime or biological save payload exists in Phase 0.5.
- The authoritative Version 1.0 design archive remains immutable; its raw external ZIP stays ignored.
