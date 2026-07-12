# PROJECT EMERGENCE
## Version 1.0 Design Archive

**Archive edition:** 1.0  
**Design status:** Complete baseline for implementation  
**Archive date:** 2026-07-12  
**Creative Director and final acceptance authority:** Timothy Nitz  
**Design, production, architecture, and QA synthesis:** ChatGPT project team  

> This archive is a consolidated authoritative edition of the accepted design. It preserves the locked decisions and implementation requirements from the design conversation in a durable project format. It is not intended as a verbatim chat transcript.

# Design Pass 7 — Ecology, Population Dynamics, Niches, Succession, Migration, Invasion, and Extinction

## Central Principle

Ecology is the macroscopic history produced by individuals, resources, space, chemistry, reproduction, death, dispersal, and interaction. It is not a population manager layered over the cells.

## Locked Decisions

- Populations are analytical groupings; individual organisms remain authoritative in explicit simulation.
- Births and deaths are actual organism events.
- There is no hidden carrying-capacity constant, density penalty, biodiversity target, or ecosystem-health controller.
- Effective population limits emerge from energy input, usable material, waste, space, predation, cooperation, parasitism, and environmental conditions.
- Growth may be exponential, plateauing, cyclic, irregular, overshooting, or collapsing.
- Niches are changing multidimensional opportunities, not predefined slots.
- Fundamental and realized niche are analytical concepts.
- Organisms can create and destroy niches through environmental modification.
- Generalist and specialist status is inferred from tolerance and resource-use breadth.
- Coexistence and competitive exclusion are both valid.
- Food webs are directed, weighted matter and energy flow networks rather than fixed trophic levels.
- Omnivory, scavenging, mixotrophy, cross-feeding, and changing trophic position are allowed.
- Genotype identity, lineage, population, ecotype, functional role, and species concept remain distinct.
- Species is not an authoritative entity in the early implementation.
- Extinction may be local, global, genotype-level, lineage-level, or functional.
- The engine does not protect historically important or rare lineages.
- Invasion is establishment and ecological impact after actual arrival, not a dice roll.
- Succession is historical community assembly, not a fixed pioneer-to-climax ladder.
- Disturbance has magnitude, duration, extent, and rate; it may help some lineages and harm others.
- Resistance, resilience, recovery, constancy, and persistence are separate analytical properties.
- Alternative persistent states and hysteresis are possible.
- Source-sink, refugia, corridors, range fronts, rescue, and metapopulation foundations arise spatially.
- Demographic stochasticity matters at low abundance; there is no hard minimum viable population number.
- Eco-evolutionary feedback is continuous.
- Diversity has genotype, lineage, phenotype, functional, interaction, evenness, and spatial dimensions.
- High diversity is not automatically good.
- Keystone, foundation, redundancy, and uniqueness are inferred through effect and controlled removal.
- The ecosystem need not optimize stability.
- System-level matter and energy budgets remain authoritative.

## Historical and Scientific Requirements

The player must eventually inspect:

- abundance, biomass, births, deaths, immigration, and emigration;
- mortality cause;
- lineage range;
- niche occupancy;
- interaction networks;
- resource flow;
- diversity;
- disturbance response;
- invasion history;
- succession;
- local and global extinction;
- alternative branches after removal or introduction.

Causal explanations must remain cautious and identify uncertainty.

## Validation Scenarios

- unconstrained temporary expansion;
- emergent resource-limited plateau;
- overshoot and crash;
- predator-prey cycles;
- mutualistic dependency and collapse;
- spatial coexistence versus well-mixed exclusion;
- source-sink persistence;
- invasion success and failure;
- priority effects;
- disturbance and regime shift;
- local and global extinction;
- repeatable demographic stochasticity by seed;
- conservation;
- no ghost populations;
- stable lineage clustering;
- analytics-disabled equivalence;
- storage permutation invariance.

## Deferred Decisions

Exact lineage clustering, operational species concepts, diversity equations, disturbance catalogue, dormancy, sex, HGT, disease, migration across regions, ecological causal inference, and coarse population models remain deferred.

## Acceptance Statement

Ecology is accepted only when large-scale patterns remain traceable to local biological events and when no analytical classification feeds back as an invisible rule.
