"""Generate deterministic independent MAVLink decoder fixtures with pymavlink.

Install pymavlink into the active Python environment, then run this script from any
directory. The generated JSON is consumed by .NET tests and has no Python runtime
dependency.
"""

from __future__ import annotations

import argparse
import importlib.metadata
import importlib.util
import io
import json
import re
import tempfile
from pathlib import Path
from typing import Any
from xml.etree import ElementTree

from pymavlink.generator import mavgen


SYSTEM_ID = 17
COMPONENT_ID = 42


def parse_args() -> argparse.Namespace:
    """Parse command-line arguments."""
    repository_root = Path(__file__).resolve().parents[1]
    default_output = (
        repository_root
        / "src"
        / "Tests"
        / "MissionPlanner.Core.Tests"
        / "Fixtures"
        / "mavlink-conformance.json"
    )
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=repository_root)
    parser.add_argument("--output", type=Path, default=default_output)
    return parser.parse_args()


def load_extension_fields(root_dialect: Path) -> dict[str, set[str]]:
    """Load MAVLink 2 extension field names from the transitive dialect graph."""
    messages: dict[str, set[str]] = {}
    visited: set[Path] = set()

    def visit(path: Path) -> None:
        resolved = path.resolve()
        if resolved in visited:
            return
        visited.add(resolved)
        root = ElementTree.parse(resolved).getroot()
        for include in root.findall("include"):
            if include.text:
                visit(resolved.parent / include.text.strip())
        for message in root.findall("./messages/message"):
            extension = False
            names: set[str] = set()
            for child in message:
                if child.tag == "extensions":
                    extension = True
                elif child.tag == "field" and extension:
                    names.add(child.attrib["name"])
            messages[message.attrib["name"]] = names

    visit(root_dialect)
    return messages


def load_generated_names(decoder_source: Path) -> list[str]:
    """Read the exact generated-decoder surface from the committed source artifact."""
    pattern = re.compile(r"Decodes MAVLink ([A-Z0-9_]+) wire messages\.")
    names = pattern.findall(decoder_source.read_text(encoding="utf-8"))
    if len(names) != len(set(names)):
        raise RuntimeError("Generated decoder source contains duplicate message names.")
    return names


def import_generated_dialect(module_path: Path) -> Any:
    """Import a temporary pymavlink dialect module."""
    specification = importlib.util.spec_from_file_location("missionplanner_fixture_dialect", module_path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"Could not load generated dialect module {module_path}.")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def scalar_value(field_type: str, seed: int) -> int | float:
    """Return a small, exactly representable non-zero value for a MAVLink scalar."""
    if field_type in ("float", "double"):
        return float((seed % 47) + 1) + 0.25
    if field_type == "int8_t":
        return -((seed % 47) + 1)
    if field_type == "int16_t":
        return -0x1234 + (seed % 47)
    if field_type == "int32_t":
        return -0x1234567 + (seed % 47)
    if field_type == "int64_t":
        return -0x010203040506070 + (seed % 47)
    if field_type in ("char", "uint8_t", "uint8_t_mavlink_version"):
        return (seed % 47) + 1
    if field_type == "uint16_t":
        return 0x1234 + (seed % 47)
    if field_type == "uint32_t":
        return 0x12345678 + (seed % 47)
    if field_type == "uint64_t":
        return 0x0102030405060708 + (seed % 47)
    raise ValueError(f"Unsupported MAVLink field type {field_type}.")


def field_value(field_type: str, array_length: int, seed: int, zero: bool) -> Any:
    """Build a deterministic scalar, numeric array, or fixed character value."""
    if array_length:
        if field_type == "char":
            if zero:
                return b""
            return bytes(ord("A") + ((seed + index) % 26) for index in range(array_length))
        if zero:
            return [0] * array_length
        return [scalar_value(field_type, seed + index) for index in range(array_length)]
    if zero:
        return 0.0 if field_type in ("float", "double") else 0
    return scalar_value(field_type, seed)


def encode_message(dialect: Any, message_type: Any, values: dict[str, Any], sequence: int, mavlink1: bool) -> bytes:
    """Encode one complete unsigned frame through pymavlink."""
    message = message_type(**values)
    mavlink = dialect.MAVLink(io.BytesIO(), srcSystem=SYSTEM_ID, srcComponent=COMPONENT_ID)
    mavlink.seq = sequence
    return bytes(message.pack(mavlink, force_mavlink1=mavlink1))


def expected_fields(message_type: Any, values: dict[str, Any]) -> dict[str, Any]:
    """Return pymavlink's decoded field representation for comparison in .NET tests."""
    result = message_type(**values).to_dict()
    result.pop("mavpackettype", None)
    return result


def payload_hex(frame: bytes) -> str:
    """Extract a payload from an unsigned MAVLink 1 or MAVLink 2 frame."""
    header_length = 10 if frame[0] == 0xFD else 6
    return frame[header_length : header_length + frame[1]].hex()


def build_fixture(dialect: Any, message_type: Any, extension_fields: set[str], index: int) -> dict[str, Any]:
    """Build minimum and, when applicable, maximum payload variants for one message."""
    sequence = (254 + index) & 0xFF
    minimum_values: dict[str, Any] = {}
    maximum_values: dict[str, Any] = {}
    for field_index, (name, field_type) in enumerate(
        zip(message_type.fieldnames, message_type.fieldtypes, strict=True)
    ):
        array_length = message_type.array_lengths[message_type.orders[field_index]]
        seed = int(message_type.id) + field_index + 1
        minimum_values[name] = field_value(field_type, array_length, seed, name in extension_fields)
        maximum_values[name] = field_value(field_type, array_length, seed, False)

    minimum_v2 = encode_message(dialect, message_type, minimum_values, sequence, False)
    maximum_v2 = encode_message(dialect, message_type, maximum_values, sequence, False)
    minimum_payload = payload_hex(minimum_v2)
    maximum_payload = payload_hex(maximum_v2)
    minimum_v1 = None
    if int(message_type.id) <= 255 and maximum_payload == minimum_payload:
        minimum_v1 = encode_message(dialect, message_type, minimum_values, sequence, True).hex()

    variants: list[dict[str, Any]] = [
        {
            "kind": "minimum",
            "expectedFields": expected_fields(message_type, minimum_values),
            "payloadHex": minimum_payload,
            "mavlink2FrameHex": minimum_v2.hex(),
            "mavlink1FrameHex": minimum_v1,
        }
    ]
    if maximum_payload != minimum_payload:
        variants.append(
            {
                "kind": "maximum",
                "expectedFields": expected_fields(message_type, maximum_values),
                "payloadHex": maximum_payload,
                "mavlink2FrameHex": maximum_v2.hex(),
                "mavlink1FrameHex": None,
            }
        )

    return {
        "name": message_type.msgname,
        "messageId": int(message_type.id),
        "crcExtra": int(message_type.crc_extra),
        "sequence": sequence,
        "systemId": SYSTEM_ID,
        "componentId": COMPONENT_ID,
        "minimumPayloadLength": len(bytes.fromhex(minimum_payload)),
        "maximumPayloadLength": len(bytes.fromhex(maximum_payload)),
        "variants": variants,
    }


def main() -> None:
    """Generate the committed fixture artifact."""
    args = parse_args()
    repository_root = args.repository_root.resolve()
    dialect_root = repository_root / "src" / "Core" / "MissionPlanner.MavLink" / "Dialects"
    manifest = json.loads((dialect_root / "mavlink-generation.json").read_text(encoding="utf-8"))
    root_dialect = dialect_root / manifest["rootDialect"]
    decoder_source = (
        repository_root
        / "src"
        / "Core"
        / "MissionPlanner.MavLink"
        / "Generated"
        / "MavLinkWireDecoders.g.cs"
    )
    generated_names = load_generated_names(decoder_source)
    extension_fields = load_extension_fields(root_dialect)

    with tempfile.TemporaryDirectory(prefix="missionplanner-mavlink-fixtures-") as temporary_directory:
        module_base = Path(temporary_directory) / "ardupilotmega_fixture"
        options = mavgen.Opts(str(module_base), wire_protocol="2.0", language="Python3", validate=False)
        if not mavgen.mavgen(options, [str(root_dialect)]):
            raise RuntimeError("pymavlink failed to generate the temporary dialect module.")
        dialect = import_generated_dialect(module_base.with_suffix(".py"))

        by_name = {message_type.msgname: message_type for message_type in dialect.mavlink_map.values()}
        missing = sorted(set(generated_names) - set(by_name))
        if missing:
            raise RuntimeError(f"pymavlink omitted generated messages: {', '.join(missing)}")
        fixtures = []
        for index, name in enumerate(generated_names):
            try:
                fixtures.append(build_fixture(dialect, by_name[name], extension_fields.get(name, set()), index))
            except Exception as error:
                raise RuntimeError(f"Could not generate fixture for {name}.") from error

    artifact = {
        "schemaVersion": 1,
        "sourceRevision": manifest["sourceRevision"],
        "rootDialect": manifest["rootDialect"],
        "generator": "pymavlink",
        "generatorVersion": importlib.metadata.version("pymavlink"),
        "fixtures": fixtures,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(artifact, ensure_ascii=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote {len(fixtures)} conformance fixtures to {args.output}.")


if __name__ == "__main__":
    main()
