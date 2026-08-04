# Milestone 1 Phase 1.1 implementation traceability

| Requirement | Implementation | Evidence |
|---|---|---|
| Exact volume/channel primitives | `VolumeAmount`, `FieldChannelId`, `FieldChannelRole` in Foundation | Foundation strict JSON/arithmetic/lexical tests |
| Immutable topology and definitions | Model environment catalog, region, environment, V3 session | Model strictness, permutation, mask, limit, digest tests |
| Dense authoritative fields | Simulation `RegionFieldStore`/`WorldEnvironmentStore` | every-cell values, defensive copy, no-buffer-escape tests |
| Static session integration | V2 state/snapshot digests, capture/restore, immutable callback view | successful/failed tick and continuation tests |
| Scientific tools | exact field probes and nonrepairing conservation audit | four locked probes, totals, zero-solid-violation tests |
| Durable chunks | Persistence `FieldChunkCodec` | exact bytes/hashes, truncation/metadata/solid/trailing rejection |
| Environment package V2 | dual-version manifest/writer/reader | seven entries, V1 regression, corrupt/missing/swapped/order tests |
| Presentation | immutable normalized surface and solid mask | dimensions, totals, min/max, defensive-copy/no-Godot tests |
| Godot host | one smooth custom viewport plus raw grid and exact clicks | source/App doctor, normal/raw screenshots, zero cell nodes |
| Independent evidence | `tools/reference/phase11_environment_vectors.py` | stdlib reproduction of definitions, fields, chunks, digests, totals, probes |

## Locked semantic vectors

- Field-channel catalog: `c9fa1bc20193b72fcbbc7780776018a81d599716fd6673bc71d266d416393429`
- Region definition: `07b963faec60e3b43b97bea182a4770ce079738a987413b1042c1ed103ebffc1`
- Region state: `c22b643d840dc32d6f22e5a6281396292cabb0ebd5b370773f7309efa89da5ca`
- Environment definition: `04fb13424920862b4be724befadccd8754ed21ff3ef0cc6c887f671ffa8c8e08`
- Environment state: `cb98e417570c1b46073170128eebfc7b5b84e38bb4a1a1eac622ceb8d1578466`
- Phase11 algorithms: `b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65`
- V3 session definition: `3b3cc11fd0c728ee2d18f2f59406ec3b144c258423bdaae719634d735dd048ac`
- V2 session state: `ed67529eb33daa70db0ff52ff5d50071aae193222c6c98f26f73839286c827bc`
- V2 snapshot: `710653573b0f996970ea3cd5e9b5632dd822bbae4946702b3624bd84b9c18543`
- V2 package identity: `a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685`
- V2 manifest: `b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee`

## Geometry and state

The only region is `00000000000000000000000000000064`, 16×12, chunk edge 8, base volume 1024. The outer border and the X=8 barrier from Y=2 through Y=9 except Y=6 are solid. Solids have volume and matter zero. There are 59 solid and 133 fluid cells. Totals are energy 183686, structural precursor 120947, and waste 6310.

Locked probes `(energy, structural, waste; volume)` are `(1,1)=(1056,877,46;1024)`, `(8,5)=(0,0,0;0)`, `(8,6)=(1410,915,19;1024)`, and `(14,10)=(1708,941,43;1024)`.

## Chunk order and vectors

1. `regions/00000000000000000000000000000064/fields/0000-0000.bin` — 1720 — `e9c9f690eb5d36b9c2532e898dcf04307bfb30c107e61299402af7e64c6ea158`
2. `regions/00000000000000000000000000000064/fields/0000-0001.bin` — 1720 — `7aa20e39a5b11dbd6b66c0a63d626e9d7e6315f7f048e2574061faf0a0034767`
3. `regions/00000000000000000000000000000064/fields/0001-0000.bin` — 952 — `eb9f89e0e1e9c9e2f78ac60db42e78d3d53a6d8c38c0971c2dc9c899996731bd`
4. `regions/00000000000000000000000000000064/fields/0001-0001.bin` — 952 — `74426508ec8e95f63a073abdf9a78cfb0e1ddb234a13e9ecb1aa86e5f2c2b427`

The filename fields are chunk Y then chunk X. Within a chunk, channel order is ordinal ID and cell order is local row-major Y/X.

## Explicit phase boundary

The normal view hides the grid; raw debug reveals exact samples. Concentration is derived and not stored. No field-update algorithm, diffusion, flow/advection, reaction, organisms, cells, or biological state exists. Phase 1.2 must introduce any dynamics explicitly.
