# Known issues

- The physical scale of one matter or energy quantum is deliberately undefined until a later versioned numeric/ruleset decision.
- `AlgorithmCatalog` records one active version per algorithm ID; compatibility ranges and migrations are deferred.
- Immutable configuration and rulesets are non-executable; compatibility ranges, inheritance, includes, migrations, and hot reload are deliberately absent.
- The caller remains responsible for stable addressed-RNG sample-index assignment. Phase 0.4R scheduler order does not allocate biological samples or define future contention fairness.
- The session retains only the latest bounded tick receipt, not long-term event history.
- Fault recovery/reset, session branching, rollback, and coherent save/load are deliberately absent; Phase 0.5 owns persistence.
- No biological runtime or save format exists in Phase 0.4R.
- The authoritative Version 1.0 design archive remains immutable; its raw external ZIP stays ignored.
