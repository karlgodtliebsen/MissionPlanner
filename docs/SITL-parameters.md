sim_vehicle.py -v ArduCopter --console --map --out=udp:127.0.0.1:14550


------------------
Combined
tcp connection from shell


sim_vehicle.py -v ArduCopter -f quad --no-mavproxy

shell:
mavproxy.py --master=tcp:127.0.0.1:5760 --out=udpin:127.0.0.1:14560
------------------



Combined
wsl connection from shell

sim_vehicle.py -v ArduCopter -f quad --no-mavproxy

websockify 127.0.0.1:8765 127.0.0.1:5760


------------------



One small clarification: the label is **UDPCl**, meaning **UDP Client**—the last character is a lowercase “L”. Classic Mission Planner distinguishes UDP, UDPCl and WebSocket transports. We still need to inspect NextGen to establish which implementations are complete and which options are only present in the dropdown. ([GitHub][1])

**What the options mean**

| Option                     | What NextGen does                                                 | What you configure                               |
| -------------------------- | ----------------------------------------------------------------- | ------------------------------------------------ |
| **UDP — Listen**           | Waits for telemetry sent to a local port.                         | Local listening port, optionally a bind address. |
| **UDPCl — Client**         | Sends to a specified remote UDP endpoint and receives replies.    | Remote hostname/IP and port.                     |
| **WS — WebSocket**         | Opens a bidirectional WebSocket connection to a server or bridge. | A URL such as `ws://localhost:8765/`.            |
| **WSS — Secure WebSocket** | Uses WebSocket with TLS encryption.                               | A `wss://` URL and a trusted server certificate. |

UDP has no connection handshake. Opening a UDP socket does **not** establish that a vehicle is present. NextGen should say **“Waiting for vehicle heartbeat”** until it actually receives one.

For UDP Client, the remote listener may first need a packet from NextGen to learn where to send replies. Sending a normal **MAVLink ground-station heartbeat** avoids a situation where both ends wait silently.

WebSocket requires agreement about its payload. For this implementation, I suggest **binary WebSocket messages containing raw MAVLink bytes**. A WebSocket service carrying JSON telemetry would require a different adapter.

Neither option requires a baud rate.

**How to simulate vehicles**

Use **ArduPilot SITL** to simulate the aircraft. The transport changes how NextGen reaches it; it does not change the simulated flight dynamics.

SITL normally exposes a TCP MAVLink endpoint on port `5760`. Its documented serial connection options include TCP and UDP-client connections; a WebSocket bridge provides the WS endpoint. ([Dev documentation][2])

The examples below assume an existing ArduPilot SITL installation and that all processes share the same host/network environment. They are setup recipes, **not a claim that your current NextGen implementation has passed these tests**.

**A. Test UDP Client using MAVProxy**

From your ArduPilot checkout, start SITL without its automatically launched MAVProxy:

```bash
Tools/autotest/sim_vehicle.py -v ArduCopter -f quad --no-mavproxy
```

In another terminal, connect MAVProxy to SITL and expose a UDP listener:

```bash
mavproxy.py --master=tcp:127.0.0.1:5760 --out=udpin:127.0.0.1:14560
```

ArduPilot documents using MAVProxy’s `--out=udpin:` configuration to provide a UDP listening endpoint. ([Dev documentation][3])

In NextGen select:

| Setting     | Value              |
| ----------- | ------------------ |
| Transport   | UDP Client / UDPCl |
| Remote host | `127.0.0.1`        |
| Remote port | `14560`            |

NextGen must send an initial MAVLink packet so the listener learns its return address. If no heartbeat arrives, this startup behavior is one of the first things to inspect.

**B. Test WebSocket using websockify**

For a separate test, stop MAVProxy and restart SITL with the same command above.

Install **websockify** in your Python environment:

```bash
python -m pip install websockify
```

Start a WebSocket-to-TCP bridge:

```bash
websockify 127.0.0.1:8765 127.0.0.1:5760
```

In NextGen select:

| Setting   | Value                  |
| --------- | ---------------------- |
| Transport | WebSocket / WS         |
| URL       | `ws://127.0.0.1:8765/` |

Websockify forwards traffic bidirectionally between a WebSocket client and a TCP endpoint. This is suitable for testing the proposed raw-binary MAVLink contract; compatibility with NextGen’s existing WS code must still be checked. ([GitHub][4])

**If NextGen runs on Windows and SITL runs in WSL:** do not assume these loopback addresses work across that boundary, particularly for UDP. Bind the bridge/listener to a reachable interface and configure NextGen with that environment’s reachable address. Task 6 should provide an explicit recipe for your actual setup.

For the first test, success means **vehicle identity, live attitude and a completed parameter download**. That verifies substantially more than simply seeing “Connected.”

[1]: https://github.com/ArduPilot/MissionPlanner/blob/master/MainV2.cs?utm_source=chatgpt.com "MissionPlanner/MainV2.cs at master · ArduPilot/MissionPlanner"
[2]: https://ardupilot.org/dev/docs/learning-ardupilot-uarts-and-the-console.html?utm_source=chatgpt.com "UARTs and the Console"
[3]: https://ardupilot.org/dev/docs/making-a-mavlink-wifi-bridge-using-the-raspberry-pi.html?utm_source=chatgpt.com "Making a MAVLink WiFi bridge using the Raspberry Pi"
[4]: https://github.com/novnc/websockify?utm_source=chatgpt.com "GitHub - novnc/websockify: Websockify is a WebSocket to TCP proxy/bridge. This allows a browser to connect to any application/server/service."







The next useful checks are:

Disconnect/reconnect: parameters and MAVFTP still work after reconnecting.
Connection interruption: stop the simulator or bridge; confirm telemetry becomes stale, then recovers when restarted.
Two vehicles: confirm their telemetry stays separate and commands reach the selected vehicle.

Those checks would cover the main remaining risks beyond the successful initial connection.



