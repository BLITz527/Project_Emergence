# Phase 0.3 implementation traceability

| Requirement | Source | Automated evidence | Runtime/review evidence | Status |
|---|---|---|---|---|
| Default-value hardening | `AlgorithmTypes`, `AlgorithmCatalog`, `ConfigurationTypes`, `ImmutableConfiguration`, `OperationResults` | Foundation default regression tests | domain and review preservation checks | Implemented |
| 256-bit seed, domain, scope, address, block | `Randomness/RngValues.cs` | parse/format/JSON/boundary/copy/ordering/lane tests | `rng-self-test.json` | Implemented |
| Addressed SHA-256 V1 | `DeterministicAddressedRng` | exact bytes and four golden vectors; independent differential encoder; zero-allocation hot-path assertion | independent review-pack SHA-256 reconstruction | Implemented |
| Unbiased bounded UInt64 | `SampleUInt64BelowCore` | bounds, rejection-attempt, overflow, representative range tests | locked bounded-10 value `6` | Implemented |
| Domain and Phase 0.3 algorithm catalogs | `RngDomainCatalog`, `AlgorithmCatalog.Phase03` | ordering, mutation, duplicate/default, digest tests | locked CLI/review digests | Implemented |
| Ruleset key, descriptor, registry | `Rulesets/RulesetTypes.cs` | validation, strict JSON, ordering, lookup, immutability, redundant-digest tests | reference descriptor and registry locked digests | Implemented |
| Untrusted directory loading | `Persistence/Rulesets/RulesetDirectoryLoader.cs` | dedicated Persistence limits, UTF-8, JSON, path/reparse, no-partial tests | `ruleset-validation.json` structured issues | Implemented |
| Reference ruleset | `rulesets/foundation-reference.ruleset.json` | byte-for-byte canonical serialization test | source/package comparison and independent digest reconstruction | Implemented |
| CLI diagnostics | `CliApplication`, `FoundationRngSelfTest`, `eng/doctor.ps1` | CLI integration tests | six current-run CLI evidence groups | Implemented |
| App/package validation | `MainShell`, package/verification scripts | solution and architecture tests | source smoke/doctor, screenshot, packaged smoke/doctor, exact manifest | Implemented |
| Review evidence schema 4 | `tools/Emergence.ReviewPack` | twelve Phase 0.3 mutation/pass fixtures | independent RNG/ruleset verification and 19-section report | Implemented |
| Scope exclusions | architecture tests and ADRs 0005/0006 | forbidden API/dependency scans | no world/session/scheduler/biology claim in App/docs | Implemented |
