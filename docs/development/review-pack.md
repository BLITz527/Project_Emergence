# Review pack

After post-commit evidence collection, `eng/review-pack.ps1` creates exactly one external `M0_P0.5R_<UTC timestamp>` directory under the chosen output root.

Schema 7 records build, all eight test projects, prior RNG/ruleset/session regression vectors, CLI, App, Windows package, Phase 0.5R persistence/lock outcomes, and Phase 1.1 environment evidence. Schema 6 remains verifiable for accepted Phase 0.5R packs. The pack includes valid V1 and V2 `.emergence-world` fixtures plus extracted semantic documents, raw chunks, decoded reports, and package inventories. The custom extension and required field `.bin` files are explicitly allowed; nested `.zip`, `.7z`, `.rar`, `.tar`, and unrelated archives remain prohibited.

The Milestone 1 Phase 1.1 review extension must additionally carry deterministic environment-self-test and independent-vector JSON, field catalog/region/environment digests, totals and probes, all four raw field chunks plus decoded reports, one valid seven-entry V2 `.emergence-world`, its extracted manifest/semantic inventory, conservation and save/load evidence, normal/raw screenshots, App and packaged doctor output, and the informational performance/memory report. The verifier recomputes chunk hashes and semantic digests and rejects missing, extra, duplicate, swapped, corrupt, or stale M0.5R-only evidence.

The independent verifier reopens the fixture, requires the exact ordered three entries, enforces bounded strict UTF-8/JSON and production semantic validation, recomputes hashes and lengths, validates all cross-document identities and unchanged locked vectors, checks extracted bytes, and then checks the package file itself through the outer exact manifest. It also validates exact TRX totals and coverage files, build metadata, source/design digests, the accepted-main/original-Phase-0.5/correction ancestry chain, named stale/active/cleanup regressions, CLI lock checks, App/package stale-lock probes, and all 19 report headings.

The verifier fails on missing or extra files, unsafe paths, duplicate/case-colliding entries, a renamed arbitrary ZIP, stale Phase 0.5 evidence labeled as Phase 0.5R, altered package documents, contradictory self-test/lock values, wrong correction ancestry, or failed recovery/App/package claims.

```powershell
.\eng\review-pack.ps1 -OutputRoot C:\Dev\ReviewPacks\ProjectEmergence
.\eng\verify-review-pack.ps1 -ManifestPath <review-pack>\MANIFEST.json
```
