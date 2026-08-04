# ADR 0019: Smooth presentation versus authoritative samples

Status: Accepted for Milestone 1 Phase 1.1

## Decision

Normal view bilinearly interpolates an immutable normalized surface and hides grid lines. Solid masks remain separate and render as cohesive boundaries. An optional raw-grid mode is labeled `DEBUG / AUTHORITATIVE SAMPLES`. Click selection maps pixels to region coordinates and returns the exact raw cell probe.

## Consequences

Interpolated pixels are presentation only. Channel selection, resize, hover, debug overlay, and rendering frequency cannot change ticks, sequences, RNG inputs, fields, totals, or saves. The App uses one custom draw surface and zero nodes per cell.
