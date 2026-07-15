# Rulesets

Phase 0.3 tracks exactly one nonbiological validation artifact: `foundation-reference.ruleset.json`. It binds the Phase 0.3 algorithm catalog, the three foundation-only RNG domains, and immutable scalar configuration to redundant canonical digests.

Ruleset files are untrusted data. The Persistence loader accepts only top-level `*.ruleset.json` files, applies count/size/depth/UTF-8/path/reparse limits, rejects duplicate/unknown/missing properties, and returns no partial registry. Rulesets cannot contain executable code, includes, inheritance, migrations, compatibility ranges, plugins, or network references.
