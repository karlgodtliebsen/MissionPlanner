# Network MAVLink transports

## Implemented behavior

The Connect dialog retains its approved layout and original connect/disconnect images.
UDP Listen supports a local port and optional bind IP. UDP Client resolves the remote
hostname, uses one socket for both directions, supports a free local port (0), and
restricts incoming and outgoing datagrams to that peer. Large datagrams are preserved
across read buffers rather than truncated. IPv4 is preferred when no bind family is
specified; an explicit IPv6 bind selects matching addresses.

WebSocket supports ws:// and wss:// with the full path and query sent to the server.
Only binary raw MAVLink is accepted. MAVLink packets can cross WebSocket message
boundaries; messages can contain multiple packets and can be fragmented. Text, JSON
and Socket.IO are rejected with an explanation. Sends are serialized without an
application command queue. Disconnect aborts pending I/O; new sessions reset the parser.
WSS retains platform certificate validation; there is no certificate bypass option.
Diagnostics display only the WebSocket authority, omitting path/query and credentials.

A session-owned GCS heartbeat (configured GCS system ID, component 190) starts immediately
for UDP Client and WebSocket. It uses the existing signed/policy-controlled MAVLink send
path. Opening is followed by waiting for vehicle heartbeat, then the existing vehicle,
parameter preload, command, active/pinned context, and liveness monitoring services.
Socket openness never changes arming readiness. The shared Inspector layouts are unchanged.
Existing traffic inspection, last-packet/heartbeat ages and stale/disconnected monitoring
continue to operate through the shared pipeline. Reconnect captures the complete network
configuration and does not retry queued commands.

Endpoint drafts are persisted independently in Planner connection settings when connecting
or closing with the check button. Legacy UDPCl/WS channel keys are retained; UDPCL is
normalized for old uppercase preferences. URLs with queries are retained only in memory
because query values can be credentials; re-enter them after restarting. Embedded URL
credentials and fragments are rejected.

The application connection service still owns one active user connection. Multiple
vehicles on that link retain distinct IDs and command targets. Independent simultaneous
user links are not introduced by this change; the existing simulation channel ownership
model is unchanged. Use separate app instances or a MAVLink router for the two-endpoint
manual demonstration below.

## Implementation map

- Transport: UdpMavLinkTransport, WebSocketMavLinkTransport, TransportEndpoint.
- MAVLink: MavLinkConnectionSessionFactory.Network starts the existing client/parser
  pipeline plus the owned heartbeat. MavLinkConnectionSession cancels and joins it.
- Core: NetworkConnectionSettings, VehicleConnectionSession.Network,
  VehicleConnectionService.Network, and captured Network settings in VehicleReconnectTarget.
- UI logic: ConnectPopupViewModel.Network; persistence uses PlannerConnectionSettings.NetworkDrafts.
- Regression fixtures: NetworkTransportTests, NetworkSitlTests, ConnectPopupTests.
- Launch script: scripts/network/start-network-sitl.sh.

The original audit found dropdown-only UDPCl/WS/WSS paths and an unreachable case-sensitive
UDPCl comparison. The old UDP transport accepted any sender and truncated datagrams.
No periodic startup GCS heartbeat existed in the inspected connection path. These gaps
are addressed without adding a parallel MAVLink parser or vehicle service.

Classic references:
[transport selection](https://github.com/ArduPilot/MissionPlanner/blob/master/MainV2.cs),
[UDP Client](https://github.com/ArduPilot/MissionPlanner/blob/master/ExtLibs/Comms/CommsUDPSerialConnect.cs),
[WebSocket](https://github.com/ArduPilot/MissionPlanner/blob/master/ExtLibs/Comms/CommsWebSocket.cs).
Classic's WebSocket includes Socket.IO probing; NextGen intentionally implements the
raw-binary contract instead.

## Reproducible SITL setup

Prerequisites: built ArduCopter SITL, MAVProxy and websockify in an activated Python
environment. The script does not build ArduPilot, install packages, kill existing
simulators, or wipe their parameters. Each run gets its own working directory and
explicit nonzero instance ID. Ports must be unused.

Tested on 2026-09-29:
- Windows .NET 10, Debian WSL, Python 3.13.5.
- ArduPilot checkout e9fc86200f, existing locally built ArduCopter binary.
- MAVProxy 1.8.74, pymavlink 2.4.49, websockify 0.13.0.
- websockify installed only in .tmp/network-python for this validation.

For a normal Python environment, install the pinned bridge with:
    python -m pip install websockify==0.13.0

From the MissionPlanner checkout in Linux/WSL, with the ArduPilot Python environment active:

    bash scripts/network/start-network-sitl.sh udp ~/ardupilot 20 41 14590
    bash scripts/network/start-network-sitl.sh ws  ~/ardupilot 21 42 18765

Run these in separate terminals. Defaults bind only loopback. The script launches
SITL TCP ports 5960 and 5970 (5760 + 10 * instance), with system IDs 41 and 42.
It then runs the equivalent bridge commands:

    mavproxy.py --master=tcp:127.0.0.1:5960 --out=udpin:127.0.0.1:14590 --daemon
    python -m websockify 127.0.0.1:18765 127.0.0.1:5970

Connect to UDP Client host 127.0.0.1, remote port 14590, local port 0; or WebSocket
URL ws://127.0.0.1:18765/. Stop each launcher with Ctrl+C or create the STOP file
shown in its output. It stops only its own child processes; logs and parameter state
remain in the printed run directory.

For WSS, use a WebSocket bridge configured with a server certificate trusted by the
client platform and a matching hostname. A self-signed untrusted certificate must fail.
Do not bypass trust checks to make a test pass.

### Windows and WSL addressing

The tested Windows/WSL environment worked with loopback-bound bridges and 127.0.0.1
in the Windows client for both UDP and WebSocket. Using the address returned by
hostname -I (192.168.1.175 on this host) failed. Do not hard-code that address.

Other WSL configurations differ. For a NAT-mode WSL installation where loopback does
not route UDP, obtain its reachable interface address with wsl -d Debian -- hostname -I,
pass that address as argument 6 to the script, and use it as the remote host in NextGen.
The optional seventh argument selects a run directory. Bind to the intended reachable
interface rather than changing global firewall settings. Verify routing/firewall rules
for that specific port if no packets arrive. Windows TCP localhost forwarding does not
establish that UDP follows the same route.

## Automated validation

Deterministic tests use loopback fixtures, with no SITL or hardware dependency:

    dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~NetworkTransportTests
    dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter FullyQualifiedName~ConnectPopupTests

Opt-in SITL tests require the two isolated endpoints above. In PowerShell:

    $env:MP_NETWORK_SITL = '1'
    $env:MP_NETWORK_SITL_HOST = '127.0.0.1'
    dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~NetworkSitlTests

These are read-only against simulated vehicle configuration: heartbeat, attitude,
complete parameter download, REQUEST_MESSAGE(AUTOPILOT_VERSION) acknowledgement, and
two connect/disconnect cycles per transport. They verify distinct system IDs 41 and 42.
Both real SITL test cases passed. Temporary test processes were stopped afterward;
the user's existing instance was left running.

Deterministic coverage also includes two vehicles on one link and target-specific
command acknowledgements, malformed bytes, fragmented/coalesced/split WebSocket
messages, UDP foreign-peer rejection, datagram remainder/duplicates, cancelled reads,
silent-endpoint timeout, text rejection, and TLS rejection with URL redaction.
Positive trusted-certificate WSS integration and manual GUI operation remain unverified.

The affected app and test projects built successfully. The final regression run passed
46 Core tests and 3 UI tests, in addition to the 2 opt-in real SITL cases above.

Expected Inspector output: transport UDPCl or WS/WSS, the configured endpoint (URL
authority only), distinct vehicle identity, updating attitude, and packet/heartbeat ages.
A timeout means no qualifying vehicle heartbeat arrived, not that a UDP handshake failed.
Text-payload errors usually mean a JSON/Socket.IO bridge. Handshake failures can mean
wrong URL/path, inaccessible listener, or certificate trust failure. Arming readiness
is assessed separately from any of these transport states.

Reference setup documentation:
[ArduPilot SITL](https://ardupilot.org/dev/docs/using-sitl-for-ardupilot-testing.html),
[MAVProxy UDP listening output](https://ardupilot.org/dev/docs/making-a-mavlink-wifi-bridge-using-the-raspberry-pi.html),
[websockify](https://github.com/novnc/websockify).
