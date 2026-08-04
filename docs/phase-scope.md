# Milestone 1 Phase 1.1 scope

Phase 1.1 adds the first authoritative environment without adding organisms, cells, ecology, or environmental dynamics. One V3 session owns one immutable 16×12 region definition and a Simulation-owned dense UInt64 amount array for each of three conserved-material channels. The region uses row-major Y/X indexing, 8×8 chunks, base cell volume 1024, a solid outer boundary, and a solid internal barrier with one opening. Solid cells have effective volume zero and must contain zero matter. Concentration is derived from exact amount and effective volume; it is never stored.

The locked fixture uses `matter.energy-substrate`, `matter.structural-precursor`, and `matter.waste`. Initialization is a closed formula with no RNG. The environment is static: Phase 1.1 defines no diffusion, flow, reaction, transport, update, repair, or hidden evolution algorithm. A session tick may execute the existing technical scheduler but must preserve every field amount, total, and digest. Phase 1.2 owns the future diffusion/flow/reaction boundary.

Definitions and immutable captures live in Model. Dense authoritative arrays live only in Simulation. Persistence owns binary chunk and package encoding. Presentation owns disposable normalized surfaces and solid masks. App owns only channel/debug/selection preferences and a rendered view. No package path, texture, interpolated pixel, or Godot node is authoritative state.

`WorldSessionDefinition` V1 and V2, snapshot V1, and world-package V1 remain exact regression formats. Phase 1.1 adds definition V3, snapshot V2, and world-package/manifest V2. A reference V2 package has seven entries: `definition.json`, `snapshot.json`, four field chunks ordered by RegionId then chunk Y/X, and `package-manifest.json`. Old packages are verified/loaded as their original format and are never silently upgraded.

Normal App view hides the grid and interpolates presentation samples for a continuous surface. The optional raw-grid debug view is explicitly labeled `DEBUG / AUTHORITATIVE SAMPLES`. A click maps to a region coordinate and reports the exact raw sample; interpolated display values are never presented as authoritative.

Technical limits are safety bounds, not biological carrying capacities: one region; 16 field channels; 512×512 cells; 262,144 cells/region; 4,096 chunks/region; 4,194,304 field slots/region; chunk edges 8/16/32/64; 268,435,456 package bytes; 2,097,152 manifest bytes; 16,777,216 bytes each for definition and snapshot JSON; 8,388,608 bytes/chunk; 201,326,592 total chunk bytes; 4,099 package entries; JSON depth 64.

Out of scope: field mutation, diffusion, advection/flow, reaction chemistry, organisms, cells, genomes, metabolism, reproduction, death, population limits, connected regions, migrations, autosave, branching/rollback, cloud/network saves, and event-history replay.
