# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.3_<UTC timestamp>` outside the repository.

Schema 4 adds structured RNG and ruleset evidence to restore, Debug, Release, assembly inventory, CLI, five test projects, App, and package outcomes. The verifier independently reconstructs the RNG address bytes and SHA-256, recomputes catalog/configuration/descriptor/registry digests, compares reviewed source and packaged rulesets, and rejects stale or contradictory App/package reports.

Phase 0.1R protections remain: bidirectional exact file inventory, duplicate/unsafe/path-traversal rejection, no unlisted extras, no generated clutter or nested archives, source/design digests, semantic TRX counters, exact package manifest, App checks, and all 19 implementation-report headings.

Verify independently with:

```powershell
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
