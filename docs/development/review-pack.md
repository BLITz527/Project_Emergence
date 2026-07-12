# Review pack

Run `eng/review-pack.ps1` after collecting build, test, diagnostics, App, and package evidence. The default output is `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.1_<UTC timestamp>` and is intentionally outside the repository.

The tool captures Git identity/state, an exact tracked-plus-untracked nonignored source snapshot, available evidence, architecture/roadmap docs, SHA-256 file inventory, source-tree digest, and honest warnings. `MANIFEST.json` excludes its own impossible self-hash. Verify it with `eng/verify-review-pack.ps1 -ManifestPath <path>`.
