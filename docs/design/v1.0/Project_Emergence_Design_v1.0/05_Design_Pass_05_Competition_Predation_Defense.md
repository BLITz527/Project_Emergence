# PROJECT EMERGENCE
## Version 1.0 Design Archive

**Archive edition:** 1.0  
**Design status:** Complete baseline for implementation  
**Archive date:** 2026-07-12  
**Creative Director and final acceptance authority:** Timothy Nitz  
**Design, production, architecture, and QA synthesis:** ChatGPT project team  

> This archive is a consolidated authoritative edition of the accepted design. It preserves the locked decisions and implementation requirements from the design conversation in a durable project format. It is not intended as a verbatim chat transcript.

# Design Pass 5 — Competition, Consumption, Predation, and Defense

## Purpose

Permit organisms to obtain advantage from one another, damage one another, consume biomass, defend themselves, and create ecological arms races without introducing a combat game.

## Predation Is an Emergent Relationship

Predation is inferred when an organism repeatedly:

- encounters another organism;
- damages, breaches, captures, or processes it;
- obtains useful matter or energy;
- survives the costs;
- gains reproductive opportunity.

There is no predator class, attack command, kill reward, combat stat, or fixed prey role.

## Competition

Competition exists on a spectrum:

- exploitative resource competition;
- access blocking;
- chemical interference;
- contact disruption;
- direct biomass acquisition.

The simulator does not require all competition to involve damage.

## Dead Biomass Continuum

Material progresses through states such as:

```text
living body
→ dead biomass
→ particulate material
→ dissolved compounds
```

Scavenging is a foundational ecological behavior and should be implemented before or alongside complex predation.

## Damage

Damage affects real structures:

- membrane;
- transport;
- regulation;
- movement;
- internal chemistry;
- later tissue.

Integrity summarizes physiological coherence but is not a separate combat health bar.

## Offensive Mechanisms

Early offense may use:

- contact disruption;
- enzymes;
- toxins;
- adhesion and capture;
- secretion;
- mechanical pressure;
- later piercing or engulfment.

All mechanisms have physical machinery and costs.

## Toxins

A toxin is an environmental substance. Toxicity is target-relative. A compound may:

- damage one lineage;
- be harmless to another;
- become a resource for a third;
- be detoxified or transformed.

There is no universal poison tag that bypasses chemistry.

## Capture and Feeding

Biomass acquisition may be partial. Grazing, parasitism, scavenging, leakage feeding, and lethal predation form a continuum.

An attacker cannot obtain more matter than exists in the prey or environment.

## Defense

Defense may arise from:

- membrane resilience;
- detoxification;
- movement;
- camouflage or marker similarity;
- adhesion;
- spatial refuge;
- repair;
- sacrifice;
- chemical countermeasures.

Every defense has costs and tradeoffs.

## Failure and Ambiguity

Allowed outcomes include:

- failed attack;
- mutual injury;
- friendly fire;
- cannibalism;
- prey escape;
- predator starvation;
- prey toxicity;
- partial feeding;
- attacker death.

The engine does not protect kin unless an evolved recognition system exists.

## Ecological Consequences

The same substrate must permit:

- predator-prey cycles;
- overshoot and collapse;
- coexistence;
- extinction;
- trophic cascades;
- arms races;
- spatial refuge;
- role reversal.

No system forces stability.

## Scientific Tools

Required analysis includes:

- damage source;
- mortality cause;
- biomass flow;
- feeding relationship;
- toxin field;
- defense cost;
- predation confidence;
- food-web and interaction history.

## Validation Requirements

Controlled scenarios include:

- fair shared-resource competition;
- scavenging;
- contact damage;
- toxin production and detoxification;
- failed attack cost;
- partial consumption;
- predator-prey cycling;
- spatial refuge;
- cannibalism;
- simultaneous damage;
- prey biomass conservation;
- permutation invariance;
- no ghost food or kill reward.

## Failure Modes to Avoid

- hit points and attack power;
- turn-based or cooldown combat;
- target selection using global identity;
- fixed trophic levels;
- despawning bodies;
- toxins as free damage zones;
- automatic prey recognition;
- friendly-fire immunity;
- hidden balance intervention.

## Acceptance Statement

Pass 5 is satisfied when predation, defense, parasitism, scavenging, and ecological conflict can emerge from the same physical interactions used by all other life.
