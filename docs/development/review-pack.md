# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates exactly one external `M0_P0.5_<UTC timestamp>` directory under the chosen output root.

Schema 6 records build, all eight test projects, prior RNG/ruleset/session regression vectors, CLI, App, Windows package, and Phase 0.5 persistence outcomes. It includes one valid `.emergence-world` fixture plus exact extracted `definition.json`, `snapshot.json`, `package-manifest.json`, and package-inventory evidence. The custom extension is explicitly allowed; nested `.zip`, `.7z`, `.rar`, `.tar`, and unrelated archives remain prohibited.

The independent verifier reopens the fixture, requires the exact ordered three entries, enforces bounded strict UTF-8/JSON and production semantic validation, recomputes hashes and lengths, validates all cross-document identities and locked vectors, checks extracted bytes, and then checks the package file itself through the outer exact manifest. It also validates exact TRX totals and coverage files, build metadata, source/design digests, feature ancestry, App/package round trips, and all 19 report headings.

The verifier fails on missing or extra files, unsafe paths, duplicate/case-colliding entries, a renamed arbitrary ZIP, stale Phase 0.4R evidence labeled as Phase 0.5, altered package documents, contradictory self-test values, or failed recovery/App/package claims.

```powershell
.\eng\review-pack.ps1 -OutputRoot C:\Dev\ReviewPacks\ProjectEmergence
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
