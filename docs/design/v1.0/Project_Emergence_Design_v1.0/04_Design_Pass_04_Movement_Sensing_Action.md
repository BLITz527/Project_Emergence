# PROJECT EMERGENCE
## Version 1.0 Design Archive

**Archive edition:** 1.0  
**Design status:** Complete baseline for implementation  
**Archive date:** 2026-07-12  
**Creative Director and final acceptance authority:** Timothy Nitz  
**Design, production, architecture, and QA synthesis:** ChatGPT project team  

> This archive is a consolidated authoritative edition of the accepted design. It preserves the locked decisions and implementation requirements from the design conversation in a durable project format. It is not intended as a verbatim chat transcript.

# Design Pass 4 — Movement, Sensing, Action, and Primitive Behavior

## Purpose

Allow organisms to interact actively with their environment while preserving local information, physical movement, energetic cost, deterministic stochasticity, and behavioral explainability.

## Behavioral Pipeline

The authoritative pipeline is:

```text
local external and internal state
        ↓
sensing
        ↓
regulation and memory
        ↓
action requests
        ↓
physical and energetic resolution
        ↓
realized consequence
```

There is no separate universal AI controller.

## Passive Motion

Flow, collision, and environmental forces move organisms whether or not they possess motility. Passive drift remains a viable ecological strategy.

## Active Motion

Active movement uses:

- orientation;
- thrust;
- torque or turning;
- drag;
- mass;
- body geometry;
- energy.

Cells do not directly set x/y velocity or request a destination coordinate.

## Chemical Sensing

The first external sensors sample local scalar concentration. A single receptor does not reveal direction.

Direction can emerge through:

- temporal comparison while moving;
- separated receptors;
- body orientation;
- later learned associations.

## Temporal Gradient Behavior

A cell may retain a decaying trace of prior concentration and compare it with the present. This can support run-and-turn behavior:

- continue when conditions improve;
- alter turning when they worsen;
- use bounded stochasticity.

This is not pathfinding.

## Spatial Directional Sensing

Later cells may place receptors at different body locations. Directional information then derives from real receptor geometry and local samples.

## Internal Sensing

Cells may sense:

- energy;
- damage;
- waste;
- material stores;
- reproductive readiness;
- internal stress.

Internal sensing is distinct from direct access to global simulation state.

## Physiological Effect Versus Sensing

A toxin may damage an organism whether or not the organism can sense it. Sensing is a biological capability with cost.

## Memory Foundation

Primitive behavior may use:

- decaying traces;
- hysteresis;
- persistent regulatory states;
- limited recent comparison.

These are not intelligence. They are physically stored internal state.

## Stochasticity

Turning noise or exploratory variation uses explicit deterministic RNG domains. Random behavior is reproducible under the same state and seed.

## Maladaptive Behavior

Organisms may:

- move away from resources;
- follow misleading signals;
- waste energy;
- become trapped;
- ignore danger.

The simulator does not correct bad behavior.

## Communication Foundation

Secreted compounds enter real environmental fields. They may become cues or signals depending on how receivers respond and how evolution acts. There is no universal message semantics.

## Founder Behavior

The founder should begin either:

- nonmotile; or
- with weak energy-limited propulsion and random turning.

Sophisticated directional sensing is not a starting requirement.

## Explainability

For a selected action, the system should be able to identify:

- sampled inputs;
- internal state;
- active regulatory pathways;
- requested action;
- energy or force limit;
- realized movement;
- environmental drift.

## Validation Requirements

Tests include:

- passive drift;
- thrust and drag;
- energy-limited movement;
- no-motility viability;
- scalar sensor does not expose direction;
- temporal gradient tracking;
- misleading gradient behavior;
- receptor saturation;
- sensory cost;
- deterministic turning noise;
- barrier interaction;
- save/load of orientation and traces;
- rendering distinguishes active propulsion from drift.

## Failure Modes to Avoid

- nearest-resource queries;
- global pathfinding;
- direct steering vectors;
- target IDs supplied by the engine;
- free sensing;
- universal signal meaning;
- scripted state-machine AI separate from regulation;
- visual propulsion while passively drifting;
- camera or frame-rate effects on behavior.

## Acceptance Statement

Pass 4 is satisfied when movement and behavior arise from local sensing, inherited regulation, bounded memory, physical force, energy, and imperfect consequences.
