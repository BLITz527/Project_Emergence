# State ownership foundation

Phase 0.1 owns only immutable build-information records, structured diagnostic results, presentation-only shell status, and ephemeral runtime check results. The App does not create authoritative world state. There is no mutable simulation singleton, service locator, update loop, cell/world/region type, or internal collection exposed to presentation code.

Future authoritative state must remain in headless libraries and cross the presentation boundary through deliberate contracts. This document does not define that future state model.
