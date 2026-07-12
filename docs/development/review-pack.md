# Review pack

Run `eng/review-pack.ps1` after collecting clean single-run build, test, diagnostics, App, screenshot, and package evidence. Phase 0.1R output uses `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.1R_<UTC timestamp>` and is intentionally outside the repository.

The tool captures Git identity/state, the exact tracked source snapshot for a clean reviewed commit, normalized test/TRX/coverage outcomes, build/CLI/App/package evidence, imported design status, required development and architecture documents, a complete implementation report, SHA-256 file inventory, and source-tree digest. Creation fails unless required tests, App evidence, and package evidence parse and validate as passed.

The hardened verifier rejects missing, extra, duplicate, unsafe, traversal, stale, generated, archived, hash-mismatched, or semantically contradictory evidence. `MANIFEST.json` excludes only its own impossible self-hash. Verify it independently with `eng/verify-review-pack.ps1 -ManifestPath <path>`.
