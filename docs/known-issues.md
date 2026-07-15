# Known issues

- The physical scale of one matter or energy quantum is deliberately undefined until a later versioned numeric/ruleset decision.
- `AlgorithmCatalog` records one active version per algorithm ID; compatibility ranges and migrations are deferred.
- Immutable configuration and rulesets are non-executable; compatibility ranges, inheritance, includes, migrations, and hot reload are deliberately absent.
- The caller remains responsible for stable RNG sample-index assignment; no scheduler policy exists yet.
- No biological runtime, world session, scheduler, or save format exists in Phase 0.3.
- The authoritative Version 1.0 design archive remains immutable; its raw external ZIP stays ignored.
