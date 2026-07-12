# State ownership foundation

Foundation owns immutable build metadata, diagnostics, deterministic value primitives, immutable configuration documents, and operation-result values. `CheckedSequenceCounter` is explicitly instance-scoped state whose serialized value is only its last issued sequence; no global allocator exists.

`WorldIdentity` and `BranchIdentity` reject empty IDs and represent identity only. Typed entity IDs create no entity stores or behaviors. Model remains marker-only, and the App owns only presentation state and ephemeral runtime checks.

Future authoritative state must remain in headless libraries and cross the presentation boundary through deliberate contracts. Phase 0.2 defines no world state, scheduler, biological clock, RNG, ruleset loader, or save format.
