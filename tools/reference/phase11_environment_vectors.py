#!/usr/bin/env python3
"""Independent Phase 1.1 environment vectors using only Python's standard library."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import struct
import zipfile


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
    "fieldChannelCatalogDigest": "c9fa1bc20193b72fcbbc7780776018a81d599716fd6673bc71d266d416393429",
    "regionDefinitionDigest": "07b963faec60e3b43b97bea182a4770ce079738a987413b1042c1ed103ebffc1",
    "regionStateDigest": "c22b643d840dc32d6f22e5a6281396292cabb0ebd5b370773f7309efa89da5ca",
    "environmentDefinitionDigest": "04fb13424920862b4be724befadccd8754ed21ff3ef0cc6c887f671ffa8c8e08",
    "environmentStateDigest": "cb98e417570c1b46073170128eebfc7b5b84e38bb4a1a1eac622ceb8d1578466",
    "algorithmCatalogDigest": "b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65",
    "sessionDefinitionDigest": "3b3cc11fd0c728ee2d18f2f59406ec3b144c258423bdaae719634d735dd048ac",
    "sessionStateDigest": "ed67529eb33daa70db0ff52ff5d50071aae193222c6c98f26f73839286c827bc",
    "snapshotDigest": "710653573b0f996970ea3cd5e9b5632dd822bbae4946702b3624bd84b9c18543",
    "packageIdentityDigest": "a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685",
    "manifestDigest": "b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee",
}

CHANNELS = ["matter.energy-substrate", "matter.structural-precursor", "matter.waste"]
REGION = "00000000000000000000000000000064"
WORLD = "0000000000000000000000000000002a"
BRANCH = "00000000000000000000000000000007"
RULESET_KEY = "00000000000000000000000000000001@1.0.0"
RULESET_DESCRIPTOR = "365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086"
RULESET_REGISTRY = "0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d"
SCHEDULER = "3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a"
COMMANDS = "e2555f63b5b4c9644229336da1856f35c8dabf3cf54765e224d3c51e19a3d8f6"
SEED = bytes(range(32))
CHUNK_LOCKS = [
    (0, 0, "regions/00000000000000000000000000000064/fields/0000-0000.bin", 1720, "e9c9f690eb5d36b9c2532e898dcf04307bfb30c107e61299402af7e64c6ea158"),
    (1, 0, "regions/00000000000000000000000000000064/fields/0000-0001.bin", 1720, "7aa20e39a5b11dbd6b66c0a63d626e9d7e6315f7f048e2574061faf0a0034767"),
    (0, 1, "regions/00000000000000000000000000000064/fields/0001-0000.bin", 952, "eb9f89e0e1e9c9e2f78ac60db42e78d3d53a6d8c38c0971c2dc9c899996731bd"),
    (1, 1, "regions/00000000000000000000000000000064/fields/0001-0001.bin", 952, "74426508ec8e95f63a073abdf9a78cfb0e1ddb234a13e9ecb1aa86e5f2c2b427"),
]


def volumes() -> list[int]:
    result = []
    for y in range(12):
        for x in range(16):
            solid = x in (0, 15) or y in (0, 11) or (x == 8 and 2 <= y <= 9 and y != 6)
            result.append(0 if solid else 1024)
    return result


def amounts(channel: int, cell_volumes: list[int]) -> list[int]:
    result = []
    for y in range(12):
        for x in range(16):
            if cell_volumes[y * 16 + x] == 0:
                result.append(0)
            elif channel == 0:
                result.append(1000 + 37 * x + 19 * y)
            elif channel == 1:
                result.append(700 + 11 * (15 - x) + 23 * y)
            else:
                result.append((17 * x + 29 * y) % 97)
    return result


def catalog_digest() -> str:
    writer = Canonical().string("ProjectEmergence.FieldChannelCatalog.v1").u64(3)
    for channel in CHANNELS:
        writer.string(channel).string("ConservedMaterial")
    return writer.finish()


def region_definition_digest(catalog: str, cell_volumes: list[int]) -> str:
    writer = (Canonical().string("ProjectEmergence.RegionLatticeDefinition.v1").string("1.0.0")
              .string(REGION).u64(16).u64(12).u64(8).u64(1024).digest(catalog).u64(192))
    for volume in cell_volumes:
        writer.u64(volume)
    return writer.finish()


def region_state_digest(region_definition: str, fields: list[list[int]]) -> str:
    writer = (Canonical().string("ProjectEmergence.RegionFieldState.v1").string("1.0.0")
              .digest(region_definition).u64(3))
    for channel, values in zip(CHANNELS, fields):
        writer.string(channel).u128(sum(values)).u64(192)
        for value in values:
            writer.u64(value)
    return writer.finish()


def environment_definition_digest(catalog: str, region_definition: str) -> str:
    return (Canonical().string("ProjectEmergence.EnvironmentDefinition.v1").string("1.0.0")
            .digest(catalog).u64(1).string(REGION).digest(region_definition).finish())


def environment_state_digest(environment_definition: str, region_state: str) -> str:
    return (Canonical().string("ProjectEmergence.WorldEnvironmentState.v1").digest(environment_definition)
            .u64(1).string(REGION).digest(region_state).finish())


def algorithm_digest() -> str:
    algorithms = sorted([
        "foundation.canonical-hash@1.0.0", "foundation.stable-id@1.0.0", "foundation.logical-time@1.0.0",
        "foundation.exact-quantity@1.0.0", "foundation.immutable-configuration@1.0.0", "foundation.rng-seed@1.0.0",
        "foundation.rng-addressed-sha256@1.0.0", "foundation.rng-bounded-uint64@1.0.0", "foundation.rng-domain-catalog@1.0.0",
        "foundation.ruleset-manifest@1.0.0", "foundation.ruleset-registry@1.0.0", "simulation.world-session@1.0.0",
        "simulation.phase-graph@1.0.0", "simulation.command-pipeline@1.0.0", "simulation.event-id@1.0.0",
        "simulation.event-commit@1.0.0", "presentation.session-snapshot@1.0.0", "persistence.atomic-replace@1.0.0",
        "persistence.compatibility-check@1.0.0", "persistence.package-manifest@1.0.0", "persistence.recovery@1.0.0",
        "persistence.session-snapshot@1.0.0", "persistence.world-package@1.0.0", "simulation.session-restore@1.0.0",
        "environment.field-channel-catalog@1.0.0", "environment.region-lattice-definition@1.0.0",
        "environment.region-field-state@1.0.0", "environment.world-definition@1.0.0", "environment.world-state@1.0.0",
        "environment.field-chunk-binary@1.0.0", "simulation.environment-session-state@1.0.0",
        "persistence.environment-world-package@1.0.0", "presentation.field-surface@1.0.0",
    ])
    writer = Canonical().string("ProjectEmergence.AlgorithmCatalog.v1").u64(len(algorithms))
    for reference in algorithms:
        algorithm_id, version = reference.split("@")
        writer.string(algorithm_id).string(version)
    return writer.finish()


def session_definition_digest(algorithm: str, environment_definition: str) -> str:
    return (Canonical().string("ProjectEmergence.WorldSessionDefinition.v3").string("3.0.0")
            .string(WORLD).string(BRANCH).string(RULESET_KEY).digest(RULESET_DESCRIPTOR).digest(RULESET_REGISTRY)
            .bytes(SEED).digest(algorithm).digest(SCHEDULER).digest(COMMANDS).digest(environment_definition).finish())


def session_state_digest(definition: str, environment_state: str) -> str:
    return (Canonical().string("ProjectEmergence.WorldSessionState.v2").digest(definition).u128(0).string("Paused")
            .u128(0).u128(0).u64(0).boolean(False).digest(environment_state).finish())


def snapshot_digest(definition: str, state: str, environment_state: str) -> str:
    return (Canonical().string("ProjectEmergence.WorldSessionSnapshot.v2").string("2.0.0").digest(definition)
            .u128(0).string("Paused").u128(0).u128(0).u64(0).u64(0).digest(environment_state).digest(state).finish())


def chunk_bytes(chunk_x: int, chunk_y: int, region_definition: str, catalog: str,
                fields: list[list[int]]) -> bytes:
    start_x, start_y = chunk_x * 8, chunk_y * 8
    width, height = min(8, 16 - start_x), min(8, 12 - start_y)
    result = bytearray(b"PE-FIELD-CHUNK1\0")
    result += bytes.fromhex(REGION)
    result += struct.pack("<IIHHHH", chunk_x, chunk_y, width, height, 3, 0)
    result += bytes.fromhex(region_definition) + bytes.fromhex(catalog) + struct.pack("<I", width * height)
    for channel, values in zip(CHANNELS, fields):
        encoded = channel.encode("utf-8")
        result += struct.pack("<H", len(encoded)) + encoded
        for local_y in range(height):
            for local_x in range(width):
                result += struct.pack("<Q", values[(start_y + local_y) * 16 + start_x + local_x])
    return bytes(result)


def package_identity(algorithm: str, definition: str, snapshot: str, state: str,
                     environment_definition: str, environment_state: str) -> str:
    paths = ["definition.json", "snapshot.json", *[item[2] for item in CHUNK_LOCKS]]
    writer = (Canonical().string("ProjectEmergence.WorldPackageIdentity.v2").string("2.0.0").string(WORLD).string(BRANCH)
              .digest(definition).digest(snapshot).digest(state).digest(RULESET_REGISTRY).digest(algorithm)
              .digest(environment_definition).digest(environment_state).u64(len(paths)))
    for path in paths:
        writer.string(path)
    return writer.finish()


def verify_package(path: pathlib.Path, package_identity_digest: str) -> str:
    with zipfile.ZipFile(path, "r") as archive:
        manifest = json.loads(archive.read("package-manifest.json").decode("utf-8", "strict"))
        entries = manifest["entries"]
        for entry in entries:
            raw = archive.read(entry["path"])
            assert len(raw) == int(entry["uncompressedByteLength"])
            assert hashlib.sha256(raw).hexdigest() == entry["sha256"]
        assert manifest["packageIdentityDigest"] == package_identity_digest
        writer = (Canonical().string("ProjectEmergence.WorldPackageManifest.v2").string("2.0.0")
                  .digest(package_identity_digest).u64(len(entries)))
        for entry in entries:
            writer.string(entry["path"]).u64(int(entry["uncompressedByteLength"])).digest(entry["sha256"])
        digest = writer.finish()
        assert digest == manifest["digest"] == EXPECTED["manifestDigest"]
        return digest


def calculate(chunks_dir: pathlib.Path | None, package: pathlib.Path | None) -> dict[str, object]:
    cell_volumes = volumes()
    fields = [amounts(index, cell_volumes) for index in range(3)]
    values: dict[str, object] = {}
    values["fieldChannelCatalogDigest"] = catalog_digest()
    values["regionDefinitionDigest"] = region_definition_digest(str(values["fieldChannelCatalogDigest"]), cell_volumes)
    values["regionStateDigest"] = region_state_digest(str(values["regionDefinitionDigest"]), fields)
    values["environmentDefinitionDigest"] = environment_definition_digest(str(values["fieldChannelCatalogDigest"]), str(values["regionDefinitionDigest"]))
    values["environmentStateDigest"] = environment_state_digest(str(values["environmentDefinitionDigest"]), str(values["regionStateDigest"]))
    values["algorithmCatalogDigest"] = algorithm_digest()
    values["sessionDefinitionDigest"] = session_definition_digest(str(values["algorithmCatalogDigest"]), str(values["environmentDefinitionDigest"]))
    values["sessionStateDigest"] = session_state_digest(str(values["sessionDefinitionDigest"]), str(values["environmentStateDigest"]))
    values["snapshotDigest"] = snapshot_digest(str(values["sessionDefinitionDigest"]), str(values["sessionStateDigest"]), str(values["environmentStateDigest"]))
    values["packageIdentityDigest"] = package_identity(str(values["algorithmCatalogDigest"]), str(values["sessionDefinitionDigest"]), str(values["snapshotDigest"]), str(values["sessionStateDigest"]), str(values["environmentDefinitionDigest"]), str(values["environmentStateDigest"]))
    values["solidCellCount"] = cell_volumes.count(0)
    values["fluidCellCount"] = len(cell_volumes) - cell_volumes.count(0)
    values["channelTotals"] = dict(zip(CHANNELS, map(sum, fields)))
    values["probes"] = [
        {"x": x, "y": y, "volume": cell_volumes[y * 16 + x], "amounts": dict(zip(CHANNELS, [field[y * 16 + x] for field in fields]))}
        for x, y in ((1, 1), (8, 5), (8, 6), (14, 10))
    ]
    chunk_reports = []
    for chunk_x, chunk_y, path, length, expected_hash in CHUNK_LOCKS:
        raw = chunk_bytes(chunk_x, chunk_y, str(values["regionDefinitionDigest"]), str(values["fieldChannelCatalogDigest"]), fields)
        digest = hashlib.sha256(raw).hexdigest()
        assert len(raw) == length and digest == expected_hash
        chunk_reports.append({"path": path, "uncompressedByteLength": len(raw), "sha256": digest})
        if chunks_dir is not None:
            target = chunks_dir / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
    values["chunkEntries"] = chunk_reports
    for key, expected in EXPECTED.items():
        if key == "manifestDigest":
            continue
        assert values[key] == expected, f"{key}: expected {expected}, got {values[key]}"
    values["manifestDigest"] = verify_package(package, str(values["packageIdentityDigest"])) if package else EXPECTED["manifestDigest"]
    values["success"] = True
    return values


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", type=pathlib.Path)
    parser.add_argument("--chunks-dir", type=pathlib.Path)
    parser.add_argument("--package", type=pathlib.Path)
    args = parser.parse_args()
    output = json.dumps(calculate(args.chunks_dir, args.package), indent=2, ensure_ascii=False) + "\n"
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(output, encoding="utf-8", newline="\n")
    else:
        print(output, end="")


if __name__ == "__main__":
    main()
