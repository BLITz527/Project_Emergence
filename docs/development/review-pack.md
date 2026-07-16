# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.4_<UTC timestamp>` outside the repository.

Schema 5 adds structured session evidence to the preserved build, RNG, ruleset, eight-test-project, App, and package outcomes. The verifier independently parses the full session self-test, exact digests/counters/all ten ordered EventIds, paused tick-zero App snapshot, non-biological claim, and snapshot non-mutation. It rejects stale Phase 0.3 substitution and contradictory source or packaged reports.

Phase 0.1R protections remain: bidirectional exact file inventory, duplicate/unsafe/path-traversal rejection, no unlisted extras, no generated clutter or nested archives, source/design digests, semantic TRX counters, exact package manifest, App checks, and all 19 implementation-report headings.

Verify independently with:

```powershell
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
