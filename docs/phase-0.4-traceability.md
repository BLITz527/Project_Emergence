# Phase 0.4 implementation traceability

| Requirement | Source types/components | Automated tests | CLI/App/package/review evidence | Status |
|---|---|---|---|---|
| Phase 0.3 enum/default-write hardening | `ConfigurationValueKind`, RNG/ruleset converters | Foundation JSON regression tests | all prior vectors retained | Implemented |
| Phase 0.4 algorithm catalog | `AlgorithmCatalog.Phase04` | catalog membership/order/digest tests | digest `bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418` | Implemented |
| Strict session lexical/status/phase types | `SimulationSystemId`, `SessionCommandTypeId`, `WorldEventTypeId`, `SimulationPhase`, `WorldSessionStatus` | Model validation, ordering, strict JSON/default-write tests | session JSON semantic parse | Implemented |
| Immutable formal scheduler graph | `SimulationSystemDescriptor`, `SchedulerGraph` | permutation, tie-break, dependency/cycle/limit tests | graph digest `3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a`; App doctor | Implemented |
| Authoritative definition | `WorldSessionDefinition` | registry/catalog/identity validation, immutability, JSON/digest tests | definition digest `fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456` | Implemented |
| Session-scoped lifecycle | `WorldSession`, `WorldSessionStatus` | paused/ready/faulted, tick exhaustion, ownership/reentrancy tests | source/package App paused at tick 0 | Implemented |
| Bounded safe command intake | `SessionCommandRequest`, `AcceptedSessionCommand`, `CommandProcessorRegistry` | validation, order, sequence, future/past-due and limit tests | self-test accepts 4 commands | Implemented |
| Transactional event commitment | `WorldEventProposal`, `CommittedWorldEvent`, `TickExecutionReceipt` | canonical order, limits, ID separation, failure atomicity tests | ten exact EventIds and 10 committed events | Implemented |
| Deterministic state and trace | `WorldSession`, `SessionSelfTest` | permutation/repeat/full-vector tests | trace `58f7313342790881b43875ba1bf3461e2aa8b1dd4b23d19278dd32cd973a7491`; state `6de0d3bee6901dfdd83b080545ce58efcd86a2b52bf67f21692a947d19fb9ff0` | Implemented |
| Immutable presentation boundary | `SessionPresentationSnapshot`, `SessionPresentationSnapshotProducer` | field, immutability, sequence/non-mutation/stable JSON tests | App doctor/source/package and screenshot | Implemented |
| App consumption | `MainShell`, smoke/doctor fixture | CLI and architecture integration tests | real nonbiological paused tick-zero snapshot; no frame stepping | Implemented |
| Technical ceilings | `SessionTechnicalLimits`, `SchedulerGraph` | exactly-at and one-over boundary tests | diagnostics and docs | Implemented |
| Dependency/scope exclusions | project references and ADRs 0007–0010 | forbidden API/reference/type scans | no Godot in core, save format, global session, parallelism, or biology | Implemented |
| Review evidence schema 5 | `Phase04EvidenceValidator`, `ManifestVerifier` | valid fixture plus every malformed session/App semantic | independent verifier, exact manifest, 19-section report | Implemented |

Configured ceilings are 256 systems, 64 dependencies per system, 4,096 pending commands, 1,024 commands per tick, 4,096 proposals per system per tick, 16,384 committed events per tick, and 128 fault issues. They are technical safety ceilings, not biological carrying capacities.

Command order is authoritative intake order; future biological fairness requires staged intent resolution and does not inherit scheduler order. Events are committed outputs, not the sole current-state store. Pausing stops logical time. Faults are tick-atomic. Ownership is single-threaded. Presentation snapshots may be dropped without simulation effect. Phase 0.5 owns coherent persistence, save/load, recovery, and complete session/RNG persistence.
