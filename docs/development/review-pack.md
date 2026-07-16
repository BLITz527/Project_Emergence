# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.4R_<UTC timestamp>` outside the repository.

Schema 5 retains structured session evidence alongside the build, RNG, ruleset, eight-test-project, App, and package outcomes. The verifier independently requires the Phase 0.4R identity and correction ancestry, focused transaction/IssueSeverity/receipt-binding TRX tests, exact digests/counters/all ten ordered EventIds, and fresh M0.4R source/package App evidence. It rejects stale Phase 0.4 substitution and contradictory reports.

Phase 0.1R protections remain: bidirectional exact file inventory, duplicate/unsafe/path-traversal rejection, no unlisted extras, no generated clutter or nested archives, source/design digests, semantic TRX counters, exact package manifest, App checks, and all 19 implementation-report headings.

Verify independently with:

```powershell
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
