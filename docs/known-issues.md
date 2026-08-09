# Known issues

- The physical scale of one matter, energy, or volume quantum remains undefined pending a later versioned ruleset decision.
- Phase 1.1 fields are intentionally static. Diffusion, flow/advection, and reactions begin no earlier than Phase 1.2.
- Concentration is an exact amount/volume relationship but its human-facing unit and formatting policy remain provisional.
- Only one region and the locked rectangular topology are supported. Connected regions and geographic transitions are deferred.
- Normal visualization is interpolated presentation data. Only click probes and the raw-grid overlay identify authoritative cell samples.
- The session retains only the latest bounded tick receipt. Event-history persistence and replay remain deferred.
- V1/V2 session definitions and V1 packages remain regression formats. V3/V2 environment formats have no migration path, and no format is silently upgraded.
- Save/load uses one App path. Autosave, multiple slots, browsing, cloud sync, branching, rollback, and encryption are deferred.
- Recovery fails closed rather than guessing. A stale ordinary `.lock` file is harmless rendezvous state once no process owns its exclusive handle.
- The environment package is a complete static snapshot, not the future incremental large-world persistence architecture.
- No biological runtime, biological DTO, or biological save payload exists in Phase 1.1.
- The authoritative Version 1.0 design archive remains immutable; its raw external ZIP stays ignored.
