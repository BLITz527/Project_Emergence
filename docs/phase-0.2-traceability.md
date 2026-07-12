# Phase 0.2 implementation traceability

| Requirement | Source | Tests | CLI/review evidence | Status |
|---|---|---|---|---|
| Stable and typed IDs; empty policy | `Foundation/Identifiers` | `IdentifierTests` | domain stable/typed-ID checks | Implemented |
| Logical time and checked sequence | `Foundation/Time` | `TimeAndQuantityTests` | max tick/counter checks | Implemented |
| Exact matter/energy quanta | `Foundation/Quantities` | `TimeAndQuantityTests` | quantity separation check | Implemented |
| SHA-256 and canonical V1 | `Foundation/Hashing` | `HashingAndVersionTests` | exact encoding/digest fields; verifier | Implemented |
| Semantic algorithms/catalog | `Foundation/Versioning` | `HashingAndVersionTests` | catalog digest `a8d497cee1881fe786f414ebd2a944c2da4ccb9433430feef675b1aeb17fd6dc` | Implemented |
| Immutable configuration | `Foundation/Configuration` | `ConfigurationAndResultTests` | fixture digest `75b8257ce1bbcf5599165648ea4601e64029afb562667639e271dfde14bc2cb5` | Implemented |
| Structured issues/results | `Foundation/Results` | `ConfigurationAndResultTests` | domain result checks | Implemented |
| Durable canonical JSON | `JsonDefaults`, explicit converters | all Foundation suites | byte-stable domain self-test | Implemented |
| Phase 0.1 vector preserved | `FoundationSelfTest` | `DomainSelfTestTests`, CLI tests | `self-test.json`; verifier exact vector | Implemented |
| Phase 0.2 domain self-test | `FoundationDomainSelfTest`, CLI | `DomainSelfTestTests`, CLI tests | `domain-self-test.json`; verifier exact vectors | Implemented |
| Headless/no biology/no nondeterministic input | Foundation boundary | `ArchitectureTests` | source snapshot and architecture TRX | Implemented |
| Structured build/CLI evidence | ReviewPack validators/manifest | `ReviewPackEvidenceTests` | schema 3 independent verification | Implemented |
| App/package version and phase | App shell/export metadata | architecture/CLI plus runtime gates | App screenshot/doctor and packaged doctor | Implemented; runtime evidence generated post-commit |
