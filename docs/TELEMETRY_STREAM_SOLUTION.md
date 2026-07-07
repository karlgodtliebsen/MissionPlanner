# Telemetry Stream Request Implementation - Summary

## Problem

Your logs showed only HEARTBEAT messages (MessageId=0) were received:
```
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
```

Even when you moved the vehicle physically, **no ATTITUDE, GPS, or BATTERY telemetry appeared**.

**Root Cause:** ArduPilot does **not** stream telemetry by default—only HEARTBEAT is automatic. All other message types (ATTITUDE, GPS, BATTERY) must be explicitly requested via MAVLink `REQUEST_DATA_STREAM` commands.

---

## Solution Implemented ✅

### 1. Created MAVLink Packet Builder
**File:** `src/Core/MissionPlanner.MavLink/Encoding/MavLinkPacketBuilder.cs`
- Builds MAVLink v1 packets with CRC-16/X.25 checksums
- Implements `BuildRequestDataStreamPacket()` for REQUEST_DATA_STREAM commands
- Handles CRC_EXTRA seeds per message type

### 2. Created MAVLink Command Service
**Files:** 
- `src/Core/MissionPlanner.Core/Services/Abstractions/IMavLinkCommandService.cs` (interface)
- `src/Core/MissionPlanner.Core/Services/MavLinkCommandService.cs` (implementation)

**API:**
```csharp
Task<bool> RequestDataStreamAsync(
	VehicleId vehicleId,
	MavDataStream streamId,  // EXTRA1, POSITION, EXTENDED_STATUS, etc.
	int rateHz,
	bool start = true,
	CancellationToken cancellationToken = default);
```

### 3. Integrated into Connection Flow
**File:** `src/Core/MissionPlanner.Core/Services/VehicleConnectionService.cs`

Added `RequestTelemetryStreamsAsync()` method that's called **automatically** after heartbeat in:
- `ConnectSerialAsync()`
- `ConnectTcpAsync()`
- `ConnectUdpAsync()`

**Requests these streams:**
- **EXTRA1** @ 10 Hz → ATTITUDE (pitch, roll, yaw)
- **POSITION** @ 5 Hz → GPS (lat/lon/alt)
- **EXTENDED_STATUS** @ 2 Hz → BATTERY (voltage, remaining %)
- **RAW_SENSORS** @ 5 Hz → GPS_RAW_INT, IMU

### 4. Registered in DI Container
**File:** `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`
- Added `services.TryAddTransient<IMavLinkCommandService, MavLinkCommandService>()`
- Added `domainFactory.Add<IMavLinkCommandService, MavLinkCommandService>()`

---

## Expected Results

### Logs After Connecting

**Before:**
```
[INF] Successfully connected to vehicle 1:1 via serial port COM10
[DBG] VehicleHudDataService-Vehicle state updated for 1:1: VehicleState { 
	Pitch = , Roll = , Altitude = , BatteryVoltage =  
}
```

**After (with telemetry streams):**
```
[INF] Successfully connected to vehicle 1:1 via serial port COM10
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=Extra1(10) rate=10Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=Position(6) rate=5Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=ExtendedStatus(2) rate=2Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=RawSensors(1) rate=5Hz START
[INF] ✅ Telemetry streams requested for vehicle 1:1

[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=30 as AttitudeMessage  ⬅️ NEW!
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=33 as GlobalPositionIntMessage  ⬅️ NEW!
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=1 as SysStatusMessage  ⬅️ NEW!

[DBG] VehicleHudDataService-Vehicle state updated for 1:1: VehicleState { 
	Pitch = 2.34, Roll = -1.2, Altitude = 125.7, BatteryVoltage = 12.4  ⬅️ NOW HAS DATA!
}
```

### HUD Display

Your HUD should now show:
- ✅ **Pitch/Roll** updating in real-time (from ATTITUDE)
- ✅ **Altitude** showing GPS altitude (from GLOBAL_POSITION_INT)
- ✅ **Battery voltage** and remaining % (from SYS_STATUS)
- ✅ **GPS coordinates** (from GLOBAL_POSITION_INT)
- ✅ **Mode/Armed status** (from HEARTBEAT, already working)

---

## Testing Steps

1. **Build solution** (already successful ✅)
2. **Run Mission Planner app**
3. **Connect** → Serial → COM10 @ 115200
4. **Wait 3-5 seconds** for streams to start
5. **Move the vehicle** (tilt, rotate)
6. **Watch HUD update** with real telemetry
7. **Check logs** for 📤 and MessageId=30/33/1

---

## Troubleshooting

### If you still see only HEARTBEAT:

**Check 1: Verify telemetry requests were sent**
```powershell
# Search logs for:
Select-String -Path .\logs\*.log -Pattern "📤 Sent REQUEST_DATA_STREAM"
```

**Check 2: ArduPilot firmware version**
- Very old firmware may not support REQUEST_DATA_STREAM
- Try setting parameters manually:
  ```
  param set SR0_EXTRA1 10
  param set SR0_POSITION 5
  param write
  ```

**Check 3: Message handler registration**
- Verify `AttitudeVehicleHandler`, `PositionVehicleHandler`, `BatteryVehicleHandler` are registered
- Already done in `DomainConfigurator.cs` ✅

### If HUD doesn't update (but logs show data):

**UI Thread Issue:**
Add to `HudViewModel.SubscribeToVehicleData()`:
```csharp
var observable = hudDataService
	.ObservePrimaryVehicleHudData()
	.ObserveOn(SynchronizationContext.Current);  // ⬅️ Add this line
```

---

## Files Modified/Created

### New Files Created:
1. `src/Core/MissionPlanner.MavLink/Encoding/MavLinkPacketBuilder.cs`
2. `src/Core/MissionPlanner.Core/Services/Abstractions/IMavLinkCommandService.cs`
3. `src/Core/MissionPlanner.Core/Services/MavLinkCommandService.cs`

### Files Modified:
1. `src/Core/MissionPlanner.Core/Services/VehicleConnectionService.cs`
   - Added `RequestTelemetryStreamsAsync()` method
   - Calls after heartbeat in all Connect methods
2. `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`
   - Registered `IMavLinkCommandService` in DI + domain factory
3. `docs/ANALYSIS_HUD_VEHICLE_INTEGRATION.md`
   - Added "Solution Implemented" section
   - Updated conclusion with testing steps

### Documentation:
- `docs/SERILOG_QUICKSTART.md` (created earlier, still useful for file logging)

---

## Summary

✅ **Solution is complete and ready to test!**

The system will now:
1. Connect to vehicle
2. Wait for HEARTBEAT
3. **Automatically request telemetry streams** (NEW!)
4. Receive ATTITUDE/GPS/BATTERY messages
5. Update `VehicleState` via handlers
6. Publish `VehicleStateUpdated` events
7. Transform to `VehicleHudData`
8. Display in HUD UI

**Next:** Run the app and connect to your vehicle!
