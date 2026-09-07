"""Generate a public test-key interoperability fixture using pymavlink 2.4.49.

Run with pymavlink==2.4.49 installed. No production key or vehicle is accessed.
"""
import json
from pathlib import Path
from pymavlink.dialects.v20 import common as mavlink

sender = mavlink.MAVLink(None, srcSystem=1, srcComponent=1)
message = mavlink.MAVLink_heartbeat_message(2, 3, 0, 0, 3, 3)
unsigned = message.pack(sender)
sender.signing.secret_key = bytes(range(32))
sender.signing.link_id = 7
sender.signing.timestamp = 1234567890123
sender.signing.sign_outgoing = True
signed = message.pack(sender)
output = Path(__file__).resolve().parents[1] / "src/Tests/MissionPlanner.Core.Tests/Fixtures/mavlink-signing.json"
output.write_text(json.dumps({"generator": "pymavlink 2.4.49", "testKey": bytes(range(32)).hex(),
    "linkId": 7, "timestamp": 1234567890123, "unsigned": unsigned.hex(), "signed": signed.hex()}, indent=2) + "\n")
