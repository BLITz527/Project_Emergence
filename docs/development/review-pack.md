# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates `C:\Dev\ReviewPacks\ProjectEmergence\M0_P0.2_<UTC timestamp>` outside the repository.

Schema 3 records structured restore, Debug, Release, assembly-inventory, CLI version, doctor, Phase 0.1 self-test, Phase 0.2 domain-self-test, tests, App, and package outcomes. The verifier independently parses their logs/JSON and rejects nonzero build warnings/errors, assembly/version/commit/framework mismatch, failed diagnostics, vector mismatch, incomplete tests, stale package content, or manifest claims that disagree with evidence.

Phase 0.1R protections remain: bidirectional exact file inventory, duplicate/unsafe/path-traversal rejection, no unlisted extras, no generated clutter or nested archives, source/design digests, semantic TRX counters, exact package manifest, App checks, and all 19 implementation-report headings.

Verify independently with:

```powershell
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
