#!/usr/bin/env python3
"""Independent Phase 0.5 vectors using only explicit constants and stdlib SHA-256."""

from __future__ import annotations

import hashlib
import struct


class Canonical:
    def __init__(self) -> None:
        self.data = bytearray(b"PE-CANONICAL/1\0")

    def string(self, value: str) -> "Canonical":
        raw = value.encode("utf-8", "strict")
        self.data += b"\x01" + struct.pack("<Q", len(raw)) + raw
        return self

    def bytes(self, value: bytes) -> "Canonical":
        self.data += b"\x02" + struct.pack("<Q", len(value)) + value
        return self

    def u64(self, value: int) -> "Canonical":
        self.data += b"\x03" + struct.pack("<Q", value)
        return self

    def u128(self, value: int) -> "Canonical":
        self.data += b"\x04" + value.to_bytes(16, "little", signed=False)
        return self

    def boolean(self, value: bool) -> "Canonical":
        self.data += b"\x05" + bytes((1 if value else 0,))
        return self

    def digest(self, value: str) -> "Canonical":
        self.data += b"\x06" + bytes.fromhex(value)
        return self

    def finish(self) -> str:
        return hashlib.sha256(self.data).hexdigest()


EXPECTED = {
    "algorithmCatalogDigest": "78818c4c6a6a4aeb498a634e4cd77e5854c3fa35be2d075aabb888cb0fe7d9a1",
    "commandProcessorCatalogDigest": "e2555f63b5b4c9644229336da1856f35c8dabf3cf54765e224d3c51e19a3d8f6",
    "definitionDigest": "ca024a17b1e0ee02b57d639bea1f57d0f04154e6c3da501fd24af0ebe9798e0e",
    "preSaveStateDigest": "9c309262449fa1590750b9c320e853306fa516925bc2e05da606ff8c8e86e6cc",
    "snapshotDigest": "33427d66eb92322396cd632ad3971407441e1ca09a72e7136549624213655893",
    "packageIdentityDigest": "fcfab8b4e95de5f578330eb0d599e8759ebb62ca6fc37210f36197a88927c3d1",
    "finalStateDigest": "fb303204175f2ed6186755e9d8ff8877bcc60892554e4765f52a4224f9f706dd",
    "persistenceTraceDigest": "b527e3355bc94f2eef586214f7ecf841b968c380b7427250c7fa06216aae8d0e",
}

ALGORITHMS = sorted(
    [
        "foundation.canonical-hash@1.0.0", "foundation.stable-id@1.0.0",
        "foundation.logical-time@1.0.0", "foundation.exact-quantity@1.0.0",
        "foundation.immutable-configuration@1.0.0", "foundation.rng-seed@1.0.0",
        "foundation.rng-addressed-sha256@1.0.0", "foundation.rng-bounded-uint64@1.0.0",
        "foundation.rng-domain-catalog@1.0.0", "foundation.ruleset-manifest@1.0.0",
        "foundation.ruleset-registry@1.0.0", "simulation.world-session@1.0.0",
        "simulation.phase-graph@1.0.0", "simulation.command-pipeline@1.0.0",
        "simulation.event-id@1.0.0", "simulation.event-commit@1.0.0",
        "presentation.session-snapshot@1.0.0", "persistence.atomic-replace@1.0.0",
        "persistence.compatibility-check@1.0.0", "persistence.package-manifest@1.0.0",
        "persistence.recovery@1.0.0", "persistence.session-snapshot@1.0.0",
        "persistence.world-package@1.0.0", "simulation.session-restore@1.0.0",
    ]
)

WORLD = "0000000000000000000000000000002a"
BRANCH = "00000000000000000000000000000007"
RULESET_KEY = "00000000000000000000000000000001@1.0.0"
RULESET_DESCRIPTOR = "365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086"
RULESET_REGISTRY = "0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d"
SCHEDULER = "3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a"
SEED = bytes.fromhex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")
EVENT_IDS = [
    "8adf4015e21a6e9b4d67bf735ca95840", "eaf3454d0b583165c89d3d785a483e7b",
    "3ca4b0b1f20eab439cca3a7d874531ef", "521e2a0fa467efc0f2fac2601f1194f3",
]


def algorithm_catalog() -> str:
    writer = Canonical().string("ProjectEmergence.AlgorithmCatalog.v1").u64(len(ALGORITHMS))
    for reference in ALGORITHMS:
        algorithm_id, version = reference.split("@", 1)
        writer.string(algorithm_id).string(version)
    return writer.finish()


def command_catalog() -> str:
    return Canonical().string("ProjectEmergence.CommandProcessorCatalog.v1").u64(1).string("foundation.trace").finish()


def definition(algorithm: str, processors: str) -> str:
    return (
        Canonical().string("ProjectEmergence.WorldSessionDefinition.v2").string("2.0.0")
        .string(WORLD).string(BRANCH).string(RULESET_KEY).digest(RULESET_DESCRIPTOR)
        .digest(RULESET_REGISTRY).bytes(SEED).digest(algorithm).digest(SCHEDULER).digest(processors).finish()
    )


def state(definition_digest: str, tick: int, last_command: int, last_event: int) -> str:
    return (
        Canonical().string("ProjectEmergence.WorldSessionState.v1").digest(definition_digest)
        .u128(tick).string("Paused").u128(last_command).u128(last_event).u64(0).boolean(False).finish()
    )


def snapshot(definition_digest: str, state_digest: str) -> str:
    return (
        Canonical().string("ProjectEmergence.WorldSessionSnapshot.v1").string("1.0.0")
        .digest(definition_digest).u128(2).string("Paused").u128(4).u128(10)
        .u64(0).u64(0).digest(state_digest).finish()
    )


def package_identity(algorithm: str, definition_digest: str, snapshot_digest: str, state_digest: str) -> str:
    return (
        Canonical().string("ProjectEmergence.WorldPackageIdentity.v1").string("1.0.0")
        .string(WORLD).string(BRANCH).digest(definition_digest).digest(snapshot_digest).digest(state_digest)
        .digest(RULESET_REGISTRY).digest(algorithm).u64(2).string("definition.json").string("snapshot.json").finish()
    )


def trace(algorithm: str, processors: str, definition_digest: str, state_digest: str,
          snapshot_digest: str, package_digest: str, final_state: str) -> str:
    writer = (
        Canonical().string("ProjectEmergence.PersistenceTrace.v1").digest(algorithm).digest(processors)
        .digest(definition_digest).digest(state_digest).digest(snapshot_digest).digest(package_digest)
        .digest(state_digest).u128(5).u64(len(EVENT_IDS))
    )
    for event_id in EVENT_IDS:
        writer.string(event_id)
    return writer.digest(final_state).finish()


def main() -> None:
    values: dict[str, str] = {}
    values["algorithmCatalogDigest"] = algorithm_catalog()
    values["commandProcessorCatalogDigest"] = command_catalog()
    values["definitionDigest"] = definition(values["algorithmCatalogDigest"], values["commandProcessorCatalogDigest"])
    values["preSaveStateDigest"] = state(values["definitionDigest"], 2, 4, 10)
    values["snapshotDigest"] = snapshot(values["definitionDigest"], values["preSaveStateDigest"])
    values["packageIdentityDigest"] = package_identity(values["algorithmCatalogDigest"], values["definitionDigest"], values["snapshotDigest"], values["preSaveStateDigest"])
    values["finalStateDigest"] = state(values["definitionDigest"], 3, 5, 14)
    values["persistenceTraceDigest"] = trace(values["algorithmCatalogDigest"], values["commandProcessorCatalogDigest"], values["definitionDigest"], values["preSaveStateDigest"], values["snapshotDigest"], values["packageIdentityDigest"], values["finalStateDigest"])
    for name, value in values.items():
        if value != EXPECTED[name]:
            raise SystemExit(f"FAIL {name}: expected {EXPECTED[name]}, got {value}")
        print(f"PASS {name}={value}")


if __name__ == "__main__":
    main()
