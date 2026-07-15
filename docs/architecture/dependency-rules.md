# Dependency rules

The permitted source graph is:

```text
Foundation
├─ Model ─┬─ Simulation
│         ├─ Analytics
│         └─ History
├─ Persistence
├─ Presentation.Contracts ─ App (Godot host)
└─ Cli
```

Simulation references Foundation and Model. Analytics and History reference Foundation and Model. Persistence references Foundation only and owns untrusted ruleset filesystem loading. CLI may reference Persistence for validation; App may reference Persistence as the outer host. No non-App project can reference Godot, no core project can reference App, and package references are allow-listed by architecture tests.
