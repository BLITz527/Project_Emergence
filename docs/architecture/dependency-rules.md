# Dependency rules

The permitted source graph is:

```text
Foundation
├─ Model ─┬─ Simulation ─ Presentation.Contracts
│         ├─ Analytics
│         └─ History
├─ Persistence
├─ Presentation.Contracts
├─ Cli
└─ App (Godot host)
```

Model owns immutable session-domain contracts and references only Foundation. Simulation owns mutable `WorldSession` execution and references Foundation, Model, and the deliberate immutable presentation conversion contract. Presentation.Contracts references Foundation and Model but never Simulation or Godot. Analytics and History reference Foundation and Model. Persistence references Foundation only and owns untrusted ruleset filesystem loading; it defines no session save format.

CLI and App are outer hosts and may compose Model, Simulation, Persistence, and Presentation.Contracts. No non-App project can reference Godot; no core project can reference App; Foundation cannot reference Model or Simulation; Model cannot reference Simulation, Persistence, Presentation, Analytics, History, App, or Godot. Architecture tests allow-list package/project references and reject frame-driven stepping, reflection discovery, nondeterministic time/ID APIs, parallel scheduling, global current-session state, public mutable collections, save/load, and biological types.
