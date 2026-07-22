# Known test failures

This document records the 12 known failures observed on 2026-07-22 when running:

```powershell
dotnet test .\Tests\MissionPlanner.Core.Tests\MissionPlanner.Core.Tests.csproj --no-build --no-restore --verbosity minimal
```

The run completed with 279 passing and 12 failing tests. These failures are not a general
allow-list: a failure is considered known only when both the test name and failure signature
match an entry below. New failure messages, additional failing tests, or failures in focused
tests must still be investigated.

## Serial hardware is not attached

These integration tests require a physical ArduPilot vehicle on an available serial port.
On a machine without that hardware they fail with `Connect a ArduPilot Vehicle` and report
`Found 0 serial ports`.

1. `MissionPlanner.Core.Tests.IntegrationTests.VehicleSerialCommunicationTests.Should_Start_Complete_Domain_Vehicle_Service_Setup`
2. `MissionPlanner.Core.Tests.IntegrationTests.VehicleSerialCommunicationTests.Should_Handle_Connected_And_Registered`
3. `MissionPlanner.Core.Tests.IntegrationTests.VehicleSerialCommunicationTests.Should_Establish_LowLevel_Serial_Communication_With_Vehicle`
4. `MissionPlanner.Core.Tests.IntegrationTests.VehicleSerialCommunicationTests.Should_Retrieve_Parameters_Using_Streaming`
5. `MissionPlanner.Core.Tests.IntegrationTests.VehicleSerialCommunicationTests.Should_Retrieve_Parameters`

Run these tests only when the expected flight controller is attached and no other process owns
its COM port. Their current dependence on ordinary test discovery should eventually be replaced
with an explicit hardware-test trait or opt-in configuration.

## Fixed UDP port contention

These simulator tests currently contend for the same fixed local UDP endpoint. When another test
or process owns the endpoint, construction of `FakeMavLinkVehicle2` fails with
`SocketException: Only one usage of each socket address (protocol/network address/port) is normally permitted.`

6. `MissionPlanner.Core.Tests.MavLinkMessageDecoderHandlerTests.Should_Decode_Heartbeat_Message_From_Fake_Vehicle`
7. `MissionPlanner.Core.Tests.MavLinkTests.Should_Parse_Heartbeat_Frame_From_Fake_Vehicle`
8. `MissionPlanner.Core.Tests.VehicleTests.Should_Register_Vehicle_When_Message_Pump_Receives_Heartbeat`
9. `MissionPlanner.Core.Tests.VehicleTests.Should_Receive_CommandAck_When_Arm_Command_Is_Sent`

The durable correction is to allocate isolated ephemeral ports per test, or to serialize only the
tests that must share a fixed endpoint. A different socket error is not covered by this entry.

## Incomplete test dependency registration

These tests build a service provider that does not register
`MissionPlanner.Core.Vehicles.Handlers.Abstractions.IHeartbeatVehicleHandler`. They fail with
`InvalidOperationException: No service for type ... IHeartbeatVehicleHandler has been registered.`

10. `MissionPlanner.Core.Tests.VehicleTests.Should_Register_Vehicle_From_Received_Heartbeat_MessageAsync`
11. `MissionPlanner.Core.Tests.VehicleTests.Should_Update_Armed_State_From_Heartbeat_BaseModeAsync`

The test host should use the current vehicle-domain registration path or explicitly register the
handler required by the test.

## Overly strict HUD numeric assertion

The following integration test compares a decoded floating-point pitch value using exact equality.
The observed expected value was `-177.6`, while the decoded value was
`-177.6169164905552`.

12. `MissionPlanner.Core.Tests.IntegrationTests.VehicleHudDataIntegrationTests.Should_Get_Current_HudData_From_RegistryAsync`

The assertion should use a documented tolerance that reflects MAVLink quantization and conversion
precision. Other value differences are not automatically considered the same known failure.

## Verification guidance

For ordinary changes, run the focused tests for the affected subsystem first. A focused test failure
is actionable even if a full test run also contains entries from this document. When reporting the
full suite, state the number of passed and failed tests and identify which failures exactly matched
this snapshot.
