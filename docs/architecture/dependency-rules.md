# Dependency rules

The permitted core graph is:

```text
Foundation
|- Model
|  |- Simulation
|  |- Analytics
|  `- History
|- Persistence (Foundation + Model)
`- Presentation.Contracts (Foundation + Model)

CLI/App compose the core projects; App alone references Godot.
```

Model owns immutable definitions, snapshots, commands, events, scheduler contracts, and receipts and references only Foundation. Simulation owns mutable `WorldSession` execution, compatibility, capture, and restore and references Foundation, Model, and the immutable presentation conversion contract. Persistence references Foundation and Model: it parses/writes data documents but never depends on Simulation, callbacks, hosts, or Godot. Presentation.Contracts references Foundation and Model but never Simulation or Godot.

For Phase 1.1, Foundation contains only generic `VolumeAmount`, `FieldChannelId`, and `FieldChannelRole` primitives. Model owns field catalogs, region/environment definitions, and immutable environment state captures. Simulation alone owns dense authoritative field arrays. Persistence alone owns chunk bytes, ZIP ordering, and V2 package metadata. Presentation.Contracts owns normalized scalar surfaces and solid masks. App may create textures, but it may not own authoritative arrays or create a node per cell.

CLI and App are outer composition hosts. No non-App project may reference Godot; no core project may reference App; Foundation may not reference higher layers; Model may not reference Simulation or Persistence. Public Persistence APIs expose typed documents/results rather than arbitrary streams, byte arrays, runtime types, or serializers.

Architecture tests allow-list references and reject frame-driven stepping, reflection discovery, nondeterministic time/ID APIs, parallel scheduling, global current-session state, public mutable collections, arbitrary object deserializers, unsafe polymorphic metadata, and biological types.
