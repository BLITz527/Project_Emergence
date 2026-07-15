# State ownership foundation

Foundation owns immutable build metadata, diagnostics, deterministic value primitives, immutable configuration documents, and operation-result values. `CheckedSequenceCounter` is explicitly instance-scoped state whose serialized value is only its last issued sequence; no global allocator exists.

`WorldIdentity` and `BranchIdentity` reject empty IDs and represent identity only. Typed entity IDs create no entity stores or behaviors. Model remains marker-only, and the App owns only presentation state and ephemeral runtime checks.

`DeterministicAddressedRng` is immutable session-scoped input (root seed plus allowed-domain catalog), not a global service and not a mutable stream. RNG addresses carry explicit domain, scope, and sample index; callers own stable index assignment. Ruleset descriptors and registries are immutable values; Persistence constructs a registry only after every bounded untrusted input validates.

Future authoritative state must remain in headless libraries and cross the presentation boundary through deliberate contracts. Phase 0.3 defines no world state, scheduler, biological clock, biological RNG domain, or save format.
