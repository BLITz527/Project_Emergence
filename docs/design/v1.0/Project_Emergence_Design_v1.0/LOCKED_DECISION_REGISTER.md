# PROJECT EMERGENCE
## Version 1.0 Design Archive

**Archive edition:** 1.0  
**Design status:** Complete baseline for implementation  
**Archive date:** 2026-07-12  
**Creative Director and final acceptance authority:** Timothy Nitz  
**Design, production, architecture, and QA synthesis:** ChatGPT project team  

> This archive is a consolidated authoritative edition of the accepted design. It preserves the locked decisions and implementation requirements from the design conversation in a durable project format. It is not intended as a verbatim chat transcript.

# Locked Decision Register

The following decisions apply across implementation unless explicitly amended.

## Scientific Core

1. Behavior and physical events precede analytical labels.
2. No fitness currency guides mutation.
3. Matter and energy are explicitly accounted.
4. Organisms use local sensory and internal information only.
5. The environment evolves and is altered by life.
6. Evolution has no predetermined complexity destination.
7. Strange, simple, parasitic, colonial, and non-Earthlike life remain valid.
8. Extinction, collapse, maladaptation, and complete loss of life are valid outcomes.
9. Species, predator, mutualist, cheat, tissue, biome, intelligence, and individuality are analytical classifications.
10. Analysis never grants biological bonuses.

## Environment and Cells

11. Organisms occupy continuous positions inside region-local environments.
12. Environmental fields may use a hidden lattice.
13. Normal presentation must conceal the grid.
14. Fields use explicit source, sink, transport, reaction, and conservation rules.
15. Integrity is physiological coherence, not combat health.
16. Growth requires real material and energy.
17. Reproduction uses actual accumulated resources.
18. Death returns biomass and material.
19. There is no arbitrary lifespan or carrying-capacity constant.

## Genome and Behavior

20. Genomes are modular, bounded, typed, and non-executable.
21. Genotype, compiled phenotype, and runtime state are separate.
22. Mutation is nondirected and uses isolated deterministic randomness.
23. Duplication and deletion permit innovation and simplification.
24. Regulation is the primary behavior controller.
25. Movement uses force, orientation, drag, mass, and energy.
26. Sensors sample real local state.
27. No nearest-resource, target-vector, pathfinding, or global map is supplied.
28. Requested and realized action are distinct.
29. Maladaptive behavior remains possible.

## Ecology

30. Populations are analytical; organism events remain authoritative.
31. No ecosystem manager forces balance, diversity, succession, or stability.
32. Competition, predation, scavenging, parasitism, and defense use actual mechanisms and material.
33. Predation is not a combat class.
34. Signals are physical, costly, receiver-dependent, and exploitable.
35. Cooperation is relational and costly.
36. Resources are never shared through a magical group inventory.
37. Cheating, collapse, reversal, dependence, and conflict remain possible.
38. Niches are changing opportunities rather than slots.
39. Food webs derive from measured matter and mortality flows.
40. Invasion requires actual arrival and establishment.
41. Extinction receives no hidden prevention.

## Higher Organization

42. Adhesion and matrix are physical.
43. Colony shape emerges from local division, attachment, matrix, and mechanics.
44. A colony record cannot control cells.
45. Cell-level conflict remains inside collectives.
46. Multicellularity is a shift in reproductive individuality, not a cell-count unlock.
47. Development reconstructs form through local rules.
48. Cell types derive from regulatory and functional state.
49. Propagules and collective reproduction are required for strong organismal individuality.
50. Cancer-like growth and developmental failure emerge from ordinary mutation and control failure.
51. Cells remain simulated entities inside multicellular bodies.

## Learning

52. Learning changes an individual during life.
53. Memory is physical, bounded, costly, and forgetful.
54. Reinforcement-like learning uses internal physiological consequences.
55. The simulator supplies no external reward function.
56. Learned state never leaks into shared genotype state.
57. Neural-like systems are biological structures, not generic AI controllers.
58. Intelligence is multidimensional and not a progression score.
59. Consciousness or sentience is not automatically claimed.

## Geography

60. Regions connect through explicit physical routes.
61. Organisms and materials cross with finite travel and transfer.
62. No statistical teleportation at full fidelity.
63. Arrival is distinct from establishment.
64. Biome-like labels are derived and grant no bonuses.
65. There is no single global evolutionary age.
66. Camera position does not determine regional biology.

## Player and Visual Experience

67. Observer, scientist, and world-shaper stances share one simulation.
68. Pure observation is complete.
69. There is no required victory condition.
70. The default world view is organic, continuous, and visually polished.
71. Scientific overlays reveal exact or analytical information with legends and uncertainty.
72. Visual effects cannot fabricate biological events.
73. Semantic zoom connects world, region, ecosystem, organism, cell, and genome.
74. Intervention is explicit, branchable, and permanently recorded.
75. Causal explanations are evidence-linked and cautious.
76. Accessibility is foundational.
77. Debug views remain separate from scientific presentation.

## Deep Time and Architecture

78. Current state, execution, history, analysis, and presentation are separate layers.
79. Simulation speed, fidelity, history detail, and rendering detail are independent.
80. Full explicit simulation is the reference.
81. Reduced fidelity requires contracts and differential validation.
82. Rehydration cannot invent adaptation or discarded exact history.
83. Saves use coherent snapshots, checksums, atomic commitment, and recovery.
84. Worlds bind to versioned rulesets and algorithm identities.
85. Software updates do not silently change old-world biology.
86. The simulation core is headless and independent of Godot.
87. Godot is the rendering and UI host.
88. No node per cell.
89. Stable typed IDs are separate from dense runtime slots.
90. Data-oriented storage and deterministic phase scheduling are required.
91. Fair batched resolution prevents iteration-order selection.
92. Simulation, analytics, persistence, and presentation randomness are separated.
93. A single-threaded reference engine remains available.
94. Parallel work commits deterministically.
95. Analytics and rendering read state but do not mutate biology.
96. Technical limits are explicit and never masquerade as biology.

## Implementation Program

97. Implementation uses small accepted vertical slices.
98. Each accepted slice is runnable, saveable, inspectable, and testable.
99. Codex implementation reports are independently reviewed.
100. Human QA remains decisive for interaction and presentation.
101. `main` represents accepted implementation.
102. Significant phases produce self-contained review packs.
103. Packaged-runtime behavior is tested.
104. Design changes use explicit amendments.
