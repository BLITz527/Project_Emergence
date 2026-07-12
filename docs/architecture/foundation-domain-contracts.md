# Foundation domain contracts

## Identifiers

`StableId128` and all typed wrappers format as 32 lowercase hexadecimal characters. Parsing accepts upper or lowercase hex but no separators. Empty values are detectable; `WorldIdentity` and `BranchIdentity` reject them. IDs never encode slots and this phase supplies no allocator.

## Time and counters

`SimulationTick` is an absolute UInt128 point; `TickSpan` is a UInt128 duration. JSON is canonical decimal text. Arithmetic is checked. `CheckedSequenceCounter` is local, monotonic, serializable, and permanently exhausted after issuing MaxValue.

## Exact quantities

`MatterAmount` and `EnergyAmount` are incompatible UInt64-quanta values. Scale and additional units are deferred. No authoritative floating-point conversion exists.

## Algorithms and configuration

Algorithm and schema IDs use bounded lowercase ASCII dotted segments. Catalogs contain one version per ID and serialize ordinally. Immutable configuration accepts Boolean, Int64, UInt64, Decimal, String, and Digest only; it copies input, sorts keys, includes a redundant digest, and rejects digest mismatch. It cannot execute code.

## Results

`DiagnosticSeverity` remains unchanged. General `FoundationIssue` values use structured code/severity/summary/detail. Information and Warning permit success; Error and Critical force failure. A successful generic result has a value and a failed result cannot expose one. Issue collections are defensive immutable copies.
