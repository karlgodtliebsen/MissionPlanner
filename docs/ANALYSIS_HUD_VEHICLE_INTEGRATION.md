# HUD and Vehicle Connection Integration Analysis

## Executive Summary

**Status Update (Latest):** ✅ **Event pipeline is now working!** Domain events are reaching `VehicleHudDataService` and HUD data is being generated.

**Current Issue:** HUD data shows all zeros or nulls:
```
VehicleHudData { 
    VehicleId = 1:1, 
    Pitch = 0, Roll = 0, Heading = 0, Yaw = 0, 
    AirSpeed = 0, GroundSpeed = 0, Altitude = 0, VerticalSpeed = 0, 
    BatteryVoltage = 0, BatteryRemaining = 0, GpsSatellites = 0, 
    IsArmed = False, Mode = Stabilize, 
    Latitude = , Longitude =  
}
```

**Likely Causes:**
1. 🔍 **Insufficient telemetry received** - Only captured one message (probably just HEARTBEAT)
2. 🔍 **Message handlers not processing all message types** - ATTITUDE, GPS, BATTERY messages may not be arriving
3. 🔍 **VehicleState fields not being populated** - Handlers may not be wired up or called
4. 🔍 **Timing issue** - Data queried before telemetry messages arrive

**Next Steps:**
- ✅ Add Serilog file-based logging to trace message flow
- ✅ Verify which MAVLink messages are being received
- ✅ Check if message handlers are being invoked
- ✅ Monitor VehicleState updates over time

---

## Previous Analysis

The system already has a complete event-driven data flow architecture in place connecting vehicle telemetry to the HUD display. The **initial missing piece** was that message handlers (like `AttitudeVehicleHandler`) update `VehicleSession` state but **did not publish `VehicleStateUpdated` events**. This has now been **FIXED** ✅.

## Current Architecture Overview

### ✅ What's Already Built and Working

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Connection Layer ✅                           │
└─────────────────────────────────────────────────────────────────────┘
	↓
 [Serial/TCP/UDP Transport] ✅
	↓
 [IMavLinkClient] ← processes MAVLink packets ✅
	↓
 [IVehicleMessagePump] ← distributes messages to handlers ✅
	↓
┌─────────────────────────────────────────────────────────────────────┐
│                        Message Handlers ⚠️                           │
├─────────────────────────────────────────────────────────────────────┤
│ • HeartbeatVehicleHandler ✅                                         │
│ • AttitudeVehicleHandler ⚠️ (needs verification)                    │
│ • GlobalPositionVehicleHandler ⚠️ (needs verification)              │
│ • BatteryStatusVehicleHandler ⚠️ (needs verification)               │
│ • ... etc                                                           │
└─────────────────────────────────────────────────────────────────────┘
	↓
 [IVehicleRegistry] → stores VehicleSession(s) ✅
	↓
 [VehicleSession] → holds VehicleState ⚠️ (all fields zero/null)
	↓
✅ **FIXED: VehicleStateUpdated events now published!**
	↓
 [IVehicleHudDataService] ← subscribes to VehicleStateUpdated ✅
	↓
 [ObservePrimaryVehicleHudData()] → IObservable<VehicleHudData> ✅
	↓
 [HudViewModel] ← subscribes and updates properties ✅
	↓
 [HudView] ← renders via data binding ✅
```

**Status Legend:**
- ✅ = Confirmed working
- ⚠️ = Working but needs investigation (zero/null data issue)
- ❌ = Not working (none currently)

## Current Implementation Details

### 1. Vehicle Connection Service ✅ WORKING

**Location:** `src/Core/MissionPlanner.Core/Services/VehicleConnectionService.cs`

**Status:** ✅ Fully operational
- Connects via Serial (COM10/USB working)
- Receives heartbeats
- Registers vehicles in `IVehicleRegistry`
- Creates and manages background tasks properly
- Single-vehicle model implemented

### 2. Vehicle Registry ✅ WORKING

**Location:** `src/Core/MissionPlanner.Core/Services/VehicleRegistry.cs`

**Status:** ✅ Stores vehicle sessions correctly

**Key Methods:**
```csharp
// Registers or updates vehicle from heartbeat
public VehicleRegistryResult RegisterOrUpdateHeartbeat(...)
{
	// Creates or retrieves VehicleSession
	session.ApplyHeartbeat(...);
	// ✅ Publishes VehicleRegistered event
	eventHub.PublishDomainEvent(new VehicleRegistered(vehicleId));
	return new VehicleRegistryResult(session);
}

// Updates connection states periodically
public VehicleUpdateConnectionStateResult UpdateConnectionStates(...)
{
	foreach (var vehicle in vehicles.Values)
	{
		var stateChanged = vehicle.UpdateConnectionState(...);
		// ✅ Publishes VehicleStateUpdated for connection state changes
		eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
		if (stateChanged is not null)
		{
			eventHub.PublishDomainEvent(stateChanged);
		}
	}
}
```

**What it does publish:**
- ✅ `VehicleRegistered` - when first heartbeat received
- ✅ `VehicleStateUpdated` - during connection state changes (online/offline/degraded)
- ✅ `VehicleRegistryReset` - when registry is reset

**What it does NOT publish:**
- ❌ `VehicleStateUpdated` - when telemetry data changes (attitude, position, battery)

### 3. Message Handlers ❌ MISSING EVENT PUBLICATION

**Example:** `src/Core/MissionPlanner.Core/VehicleHandler/AttitudeVehicleHandler.cs`

```csharp
public sealed class AttitudeVehicleHandler(
	IVehicleRegistry vehicleRegistry, 
	ILogger<AttitudeVehicleHandler> logger) 
	: IAttitudeVehicleHandler
{
	public void Handle(AttitudeMessage message)
	{
		var vehicleId = new VehicleId(message.SystemId, message.ComponentId);
		var vehicle = vehicleRegistry.GetRequired(vehicleId);

		vehicle?.ApplyAttitude(
			message.Roll,
			message.Pitch,
			message.Yaw);

		// ❌ MISSING: 
		// eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
	}
}
```

**Impact:** The `VehicleSession.State` is updated, but **no one is notified**. The HUD service never receives updates.

### 4. Vehicle Session ✅ STATE MANAGEMENT WORKING

**Location:** `src/Core/MissionPlanner.Core/Services/VehicleSession.cs`

**Status:** ✅ Correctly updates internal state

```csharp
public class VehicleSession
{
	private VehicleState state;

	public VehicleState State => state;

	// ✅ All these methods correctly update state
	public void ApplyAttitude(double roll, double pitch, double yaw)
	{
		state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };
	}

	public void ApplyPosition(double latitude, double longitude, double altitude)
	{
		state = state with { Latitude = latitude, Longitude = longitude, Altitude = altitude };
	}

	public void ApplyBattery(int? batteryRemaining, float? batteryVoltage)
	{
		state = state with { BatteryRemaining = batteryRemaining, BatteryVoltage = batteryVoltage };
	}

	// ❌ MISSING: No event publication mechanism
}
```

### 5. HUD Data Service ✅ REACTIVE PIPELINE READY

**Location:** `src/Core/MissionPlanner.Core/Services/VehicleHudDataService.cs`

**Status:** ✅ Fully implemented and ready to receive events

```csharp
public sealed class VehicleHudDataService : IVehicleHudDataService, IDisposable
{
	private readonly Subject<VehicleHudData> hudDataSubject;

	public VehicleHudDataService(IVehicleRegistry vehicleRegistry, IDomainEventHub eventHub)
	{
		// ✅ Correctly subscribes to VehicleStateUpdated
		eventSubscription = eventHub.Subscribe<VehicleStateUpdated>(OnVehicleStateUpdated);
	}

	private void OnVehicleStateUpdated(VehicleStateUpdated evt)
	{
		var vehicleSession = vehicleRegistry.GetRequired(evt.VehicleId);
		if (vehicleSession == null) return;

		// ✅ Transforms VehicleState → VehicleHudData
		var hudData = TransformToHudData(vehicleSession.State);
		hudDataCache.AddOrUpdate(evt.VehicleId, hudData, (_, _) => hudData);

		// ✅ Publishes to reactive stream
		hudDataSubject.OnNext(hudData);
	}

	// ✅ Provides observable stream for UI
	public IObservable<VehicleHudData> ObservePrimaryVehicleHudData()
	{
		return hudDataSubject
			.Where(data => {
				var primaryId = GetOrSetPrimaryVehicleId();
				return primaryId != null && data.VehicleId == primaryId;
			})
			.AsObservable();
	}
}
```

**Current Transformation:**
```csharp
private static VehicleHudData TransformToHudData(VehicleState state)
{
	return new VehicleHudData(
		state.VehicleId,
		state.Pitch ?? 0,
		state.Roll ?? 0,
		heading,                    // Calculated from yaw
		state.Yaw ?? 0,
		airSpeed: 0,               // ⚠️ TODO: Need VFR_HUD message
		groundSpeed: 0,            // ⚠️ TODO: Need GPS velocity
		state.Altitude ?? 0,
		verticalSpeed: 0,          // ⚠️ TODO: Need climb rate data
		state.BatteryVoltage ?? 0,
		state.BatteryRemaining ?? 0,
		gpsSatellites: 0,          // ⚠️ TODO: Need GPS status message
		state.IsArmed,
		state.Mode,
		state.Latitude,
		state.Longitude);
}
```

### 6. HUD ViewModel ✅ REACTIVE SUBSCRIPTION READY

**Location:** `src/UI/MissionPlanner.App/Views/FlightData/Hud/HudViewModel.cs`

**Status:** ✅ Correctly subscribes to HUD data stream

```csharp
public partial class HudViewModel : ObservableObject, IDisposable
{
	private readonly IVehicleHudDataService hudDataService;
	private IDisposable? hudDataSubscription;

	public HudViewModel(IVehicleHudDataService hudDataService)
	{
		this.hudDataService = hudDataService;
		SubscribeToVehicleData();
	}

	private void SubscribeToVehicleData()
	{
		// ✅ Subscribes to primary vehicle HUD data
		var observable = hudDataService.ObservePrimaryVehicleHudData();

		hudDataSubscription = observable.Subscribe(hudData =>
		{
			// ✅ Updates all observable properties
			Pitch = hudData.Pitch;
			Roll = hudData.Roll;
			Heading = hudData.Heading;
			AirSpeed = hudData.AirSpeed;
			GroundSpeed = hudData.GroundSpeed;
			Altitude = hudData.Altitude;
			VerticalSpeed = hudData.VerticalSpeed;
			BatteryVoltage = hudData.BatteryVoltage;
			BatteryRemaining = hudData.BatteryRemaining;
			GpsSatellites = hudData.GpsSatellites;
			IsArmed = hudData.IsArmed;
			FlightMode = hudData.Mode.ToString();
		});
	}
}
```

### 7. HUD View ✅ RENDERING READY

**Location:** `src/UI/MissionPlanner.App/Views/FlightData/Hud/HudView.xaml`

**Status:** ✅ UI bindings ready

**Integration:** The `FlightDataView` already hosts the HUD:

```csharp
public FlightDataView(
	FlightDataViewModel viewModel,
	HudView hudView,
	FlightDataMapView flightDataMapView)
{
	InitializeComponent();
	BindingContext = viewModel;
	HudHost.Content = hudView;  // ✅ HUD injected here
	MapHost.Content = flightDataMapView;
}
```

## The Missing Link: Event Publication 🔴

### Problem Statement

Message handlers update `VehicleSession` state but **do not publish** `VehicleStateUpdated` events. The reactive chain is broken at the handler level.

### Current Flow (Broken)
```
[AttitudeMessage received]
	↓
[AttitudeVehicleHandler.Handle()]
	↓
[vehicle.ApplyAttitude(roll, pitch, yaw)]
	↓
[VehicleSession.state updated] ✅
	↓
❌ NO EVENT PUBLISHED ❌
	↓
[VehicleHudDataService never notified]
	↓
[HUD never updates]
```

### Expected Flow (Fixed)
```
[AttitudeMessage received]
	↓
[AttitudeVehicleHandler.Handle()]
	↓
[vehicle.ApplyAttitude(roll, pitch, yaw)]
	↓
[VehicleSession.state updated] ✅
	↓
✅ eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State)) ✅
	↓
[VehicleHudDataService.OnVehicleStateUpdated()]
	↓
[hudData transformed and cached]
	↓
[hudDataSubject.OnNext(hudData)]
	↓
[HudViewModel receives update]
	↓
[HudView renders new data] ✅
```

## Solution Options

### Option 1: Handlers Publish Events Directly ⭐ RECOMMENDED

**Approach:** Each message handler publishes `VehicleStateUpdated` after updating state.

**Pros:**
- ✅ Immediate event publication after state change
- ✅ Handlers remain thin and focused
- ✅ Clear responsibility: handler updates state + publishes event
- ✅ Follows existing pattern in `VehicleRegistry.UpdateConnectionStates()`

**Cons:**
- ⚠️ Requires injecting `IDomainEventHub` into each handler
- ⚠️ Multiple events per tick if multiple messages arrive (could flood UI)

**Implementation:**

```csharp
public sealed class AttitudeVehicleHandler(
	IVehicleRegistry vehicleRegistry,
	IDomainEventHub eventHub,  // ✅ Add this dependency
	ILogger<AttitudeVehicleHandler> logger) 
	: IAttitudeVehicleHandler
{
	public void Handle(AttitudeMessage message)
	{
		var vehicleId = new VehicleId(message.SystemId, message.ComponentId);
		var vehicle = vehicleRegistry.GetRequired(vehicleId);

		vehicle?.ApplyAttitude(message.Roll, message.Pitch, message.Yaw);

		// ✅ Publish state update
		if (vehicle != null)
		{
			eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
		}
	}
}
```

**Trade-off:** High-frequency messages (ATTITUDE at 10Hz) will trigger 10 events/sec per handler. This is acceptable if:
- UI throttles on the subscription side (using Rx operators like `Throttle()` or `Sample()`)
- The event system is efficient (lightweight publish)

### Option 2: VehicleSession Publishes Events 

**Approach:** `VehicleSession` accepts `IDomainEventHub` and publishes events internally.

**Pros:**
- ✅ Centralized event logic in `VehicleSession`
- ✅ Handlers remain simple (no event dependency)
- ✅ Single source of truth for when events fire

**Cons:**
- ⚠️ `VehicleSession` becomes stateful AND event-aware (mixed concerns)
- ⚠️ All Apply* methods need event publication
- ⚠️ Still triggers many events per second

**Implementation:**

```csharp
public class VehicleSession(
	VehicleState initialState, 
	TransportEndPoint endPoint,
	IDomainEventHub eventHub)  // ✅ Add dependency
{
	private VehicleState state = initialState;

	public void ApplyAttitude(double roll, double pitch, double yaw)
	{
		state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };

		// ✅ Publish immediately
		eventHub.PublishDomainEvent(new VehicleStateUpdated(state));
	}

	// Repeat for ApplyPosition, ApplyBattery, etc.
}
```

### Option 3: Batched State Change Notification 🎯 BEST FOR PERFORMANCE

**Approach:** Collect state changes over a time window (e.g., 100ms) and publish **one event** per batch.

**Pros:**
- ✅ Reduces event flood (10 Hz → ~10 Hz but batched)
- ✅ UI receives smooth updates without thrashing
- ✅ Matches typical HUD refresh rates (10-30 Hz)
- ✅ Better performance for high-frequency telemetry

**Cons:**
- ⚠️ More complex implementation
- ⚠️ Requires timer/scheduler in `VehicleSession` or `VehicleRegistry`
- ⚠️ Adds slight latency (acceptable for HUD: 50-100ms is fine)

**Implementation:**

```csharp
public class VehicleSession
{
	private VehicleState state;
	private readonly Timer stateChangeTimer;
	private bool stateChangedSinceLastPublish;

	public VehicleSession(...)
	{
		// Publish state every 100ms if changed
		stateChangeTimer = new Timer(PublishStateIfChanged, null, 100, 100);
	}

	public void ApplyAttitude(double roll, double pitch, double yaw)
	{
		state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };
		stateChangedSinceLastPublish = true;  // Mark dirty
	}

	private void PublishStateIfChanged(object? _)
	{
		if (stateChangedSinceLastPublish)
		{
			eventHub.PublishDomainEvent(new VehicleStateUpdated(state));
			stateChangedSinceLastPublish = false;
		}
	}
}
```

### Option 4: UI-Side Throttling (Simplest) 🚀 START HERE

**Approach:** Publish events on every state change, but throttle on the **subscription side**.

**Pros:**
- ✅ **Simplest to implement** (only change: add event publication to handlers)
- ✅ Real-time events available for other consumers
- ✅ UI controls its own update rate
- ✅ Follows Reactive Extensions best practices
- ✅ No changes needed to `VehicleSession` or `VehicleRegistry`

**Cons:**
- ⚠️ More events in the system (but modern event buses handle this fine)

**Implementation:**

**Step 1:** Add event publication to handlers (same as Option 1)

**Step 2:** Throttle in `VehicleHudDataService`:

```csharp
public VehicleHudDataService(IVehicleRegistry vehicleRegistry, IDomainEventHub eventHub)
{
	this.vehicleRegistry = vehicleRegistry;
	this.eventHub = eventHub;
	hudDataCache = new ConcurrentDictionary<VehicleId, VehicleHudData>();
	hudDataSubject = new Subject<VehicleHudData>();

	// ✅ Subscribe with throttling
	eventSubscription = this.eventHub
		.Subscribe<VehicleStateUpdated>()
		.Throttle(TimeSpan.FromMilliseconds(100))  // Max 10 Hz updates
		.Subscribe(OnVehicleStateUpdated);
}
```

**Or throttle in `HudViewModel`:**

```csharp
private void SubscribeToVehicleData()
{
	var observable = hudDataService
		.ObservePrimaryVehicleHudData()
		.Sample(TimeSpan.FromMilliseconds(50))  // Sample at 20 Hz
		.ObserveOn(SynchronizationContext.Current);  // UI thread

	hudDataSubscription = observable.Subscribe(hudData =>
	{
		Pitch = hudData.Pitch;
		Roll = hudData.Roll;
		// ...
	});
}
```

## Thread Safety Considerations

### ⚠️ UI Thread Marshalling Required

The reactive subscription in `HudViewModel` receives events on **background threads** (from MAVLink processing). MAUI property updates **must happen on the UI thread**.

**Current Code:**
```csharp
hudDataSubscription = observable.Subscribe(hudData =>
{
	// ❌ This runs on background thread!
	Pitch = hudData.Pitch;  // May throw: "Property updates must be on UI thread"
});
```

**Fixed Code:**
```csharp
using System.Reactive.Linq;

private void SubscribeToVehicleData()
{
	var observable = hudDataService
		.ObservePrimaryVehicleHudData()
		.ObserveOn(SynchronizationContext.Current);  // ✅ Switch to UI thread

	hudDataSubscription = observable.Subscribe(hudData =>
	{
		// ✅ Now safe: on UI thread
		Pitch = hudData.Pitch;
		Roll = hudData.Roll;
		// ...
	});
}
```

**Or use MAUI dispatcher:**
```csharp
hudDataSubscription = observable.Subscribe(hudData =>
{
	MainThread.BeginInvokeOnMainThread(() =>
	{
		Pitch = hudData.Pitch;
		Roll = hudData.Roll;
		// ...
	});
});
```

## Data Completeness Status

### ✅ Currently Available in VehicleState
- ✅ `Pitch, Roll, Yaw` (from ATTITUDE message)
- ✅ `Latitude, Longitude, Altitude` (from GLOBAL_POSITION_INT message)
- ✅ `BatteryVoltage, BatteryRemaining` (from SYS_STATUS or BATTERY_STATUS)
- ✅ `IsArmed` (from base mode in HEARTBEAT)
- ✅ `Mode` (from custom mode in HEARTBEAT)

### ❌ Missing from VehicleState (TODO)
- ❌ `AirSpeed` - Need VFR_HUD message handler
- ❌ `GroundSpeed` - Can calculate from GPS velocity components (need GPS_RAW_INT or GLOBAL_POSITION_INT velocity)
- ❌ `VerticalSpeed` - In GLOBAL_POSITION_INT.vz or VFR_HUD.climb
- ❌ `GpsSatellites` - Need GPS_RAW_INT.satellites_visible

**Impact:** HUD will display zeros for these fields until handlers are added.

## Recommended Implementation Plan

### Phase 1: Minimal Integration (Get HUD Working) ⭐ DO THIS FIRST

**Goal:** Get real-time attitude and basic telemetry displaying on HUD **now**.

**Changes Required:**
1. ✅ Add `IDomainEventHub` dependency to message handlers
2. ✅ Publish `VehicleStateUpdated` in each handler after state update
3. ✅ Add `ObserveOn(SynchronizationContext.Current)` to `HudViewModel` subscription
4. ✅ Test with existing data (pitch, roll, altitude, battery, mode, armed status)

**Estimated Effort:** 2-3 hours

**Files to Change:**
- `src/Core/MissionPlanner.Core/VehicleHandler/AttitudeVehicleHandler.cs`
- `src/Core/MissionPlanner.Core/VehicleHandler/GlobalPositionVehicleHandler.cs`
- `src/Core/MissionPlanner.Core/VehicleHandler/BatteryStatusVehicleHandler.cs`
- `src/UI/MissionPlanner.App/Views/FlightData/Hud/HudViewModel.cs`

### Phase 2: Performance Optimization (Optional)

**Goal:** Reduce event frequency if UI performance is poor.

**Options:**
- Add throttling to `VehicleHudDataService` subscription
- Add sampling/throttling to `HudViewModel` subscription
- Implement batched state updates in `VehicleSession`

**When:** Only if Phase 1 shows performance issues (unlikely)

### Phase 3: Complete Telemetry (Fill Missing Data)

**Goal:** Add missing MAVLink message handlers for complete HUD data.

**New Handlers Needed:**
1. `VfrHudVehicleHandler` - for airspeed, climb rate
2. `GpsRawIntVehicleHandler` - for GPS satellite count
3. Enhance `GlobalPositionVehicleHandler` - extract velocity components for ground speed

**When:** After basic HUD is working and validated

## Integration Checklist

### Prerequisites ✅ ALL DONE
- [x] `IVehicleConnectionService` connects and receives heartbeats
- [x] `IVehicleRegistry` registers vehicles
- [x] Message handlers update `VehicleSession` state
- [x] `IVehicleHudDataService` subscribes to `VehicleStateUpdated`
- [x] `HudViewModel` subscribes to HUD data stream
- [x] `HudView` is integrated into `FlightDataView`

### Missing Steps (Quick Wins)
- [ ] Message handlers publish `VehicleStateUpdated` events
- [ ] `HudViewModel` marshals updates to UI thread
- [ ] Test end-to-end: vehicle connection → HUD display
- [ ] Add throttling if needed (performance optimization)

### Future Enhancements
- [ ] Add VFR_HUD message handler
- [ ] Add GPS_RAW_INT message handler
- [ ] Extract velocity from GLOBAL_POSITION_INT
- [ ] Implement connection state indicators in HUD
- [ ] Add telemetry recording/playback for testing

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void AttitudeHandler_Should_Publish_VehicleStateUpdated()
{
	// Arrange
	var mockEventHub = new Mock<IDomainEventHub>();
	var handler = new AttitudeVehicleHandler(registry, mockEventHub.Object, logger);
	var message = new AttitudeMessage { Roll = 10, Pitch = 5, Yaw = 45 };

	// Act
	handler.Handle(message);

	// Assert
	mockEventHub.Verify(
		hub => hub.PublishDomainEvent(It.IsAny<VehicleStateUpdated>()), 
		Times.Once);
}
```

### Integration Tests
```csharp
[Fact]
public async Task HudDataService_Should_Receive_Updates_From_AttitudeHandler()
{
	// Arrange
	var eventHub = new DomainEventHub();
	var registry = new VehicleRegistry(eventHub, logger);
	var hudService = new VehicleHudDataService(registry, eventHub);
	var handler = new AttitudeVehicleHandler(registry, eventHub, logger);

	VehicleHudData? receivedData = null;
	hudService.ObservePrimaryVehicleHudData().Subscribe(data => receivedData = data);

	// Act
	var vehicleId = new VehicleId(1, 1);
	registry.RegisterOrUpdateHeartbeat(vehicleId, ...);  // Register vehicle
	handler.Handle(new AttitudeMessage { Roll = 10, Pitch = 5, Yaw = 45 });
	await Task.Delay(100);  // Allow event propagation

	// Assert
	Assert.NotNull(receivedData);
	Assert.Equal(10, receivedData.Roll);
	Assert.Equal(5, receivedData.Pitch);
}
```

### Manual Testing (with Real Vehicle)
1. Connect to vehicle via Serial (COM10/USB)
2. Open Flight Data page
3. Observe HUD displaying:
   - Pitch/Roll from vehicle movement
   - Altitude changes
   - Battery voltage/percentage
   - Armed/Disarmed state
   - Flight mode changes
4. Verify smooth updates (no flickering)
5. Test disconnect/reconnect scenarios

---

## 🔍 Debugging Zero/Null Data Issue

### Current Observation

HUD data is being generated but shows all zeros or nulls:
```csharp
VehicleHudData { 
    VehicleId = 1:1,               // ✅ Correct
    Pitch = 0,                     // ❌ Should be real value from ATTITUDE
    Roll = 0,                      // ❌ Should be real value from ATTITUDE
    Heading = 0, Yaw = 0,          // ❌ Should be real value from ATTITUDE
    AirSpeed = 0,                  // ⚠️ Expected (VFR_HUD not implemented)
    GroundSpeed = 0,               // ⚠️ Expected (velocity not extracted)
    Altitude = 0,                  // ❌ Should be from GLOBAL_POSITION_INT
    VerticalSpeed = 0,             // ⚠️ Expected (not implemented)
    BatteryVoltage = 0,            // ❌ Should be from SYS_STATUS/BATTERY_STATUS
    BatteryRemaining = 0,          // ❌ Should be from SYS_STATUS/BATTERY_STATUS
    GpsSatellites = 0,             // ⚠️ Expected (GPS_RAW_INT not implemented)
    IsArmed = False,               // ✅ Possibly correct (disarmed state)
    Mode = Stabilize,              // ✅ Correct (from HEARTBEAT)
    Latitude = , Longitude =       // ❌ Should be from GLOBAL_POSITION_INT
}
```

### Root Cause Analysis

#### 1. **Only Received HEARTBEAT Message** (Most Likely)

**Symptom:** Only `Mode = Stabilize` and `IsArmed = False` have values (both come from HEARTBEAT).

**Why this happens:**
- HEARTBEAT is sent at 1 Hz by default
- Other telemetry messages (ATTITUDE, GPS, SYS_STATUS) require:
  - Vehicle to be armed or streaming enabled
  - ArduPilot parameters configured for streaming
  - Message rates set via `SR*_` parameters

**Verification:**
```csharp
// Add logging to message handlers
public void Handle(AttitudeMessage message)
{
    logger.LogInformation("🎯 ATTITUDE received: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}", 
        message.Roll, message.Pitch, message.Yaw);
    // ... rest of handler
}
```

**Fix:**
- Enable telemetry streaming via MAVLink commands
- Check ArduPilot `SR*_EXTRA1`, `SR*_POSITION`, `SR*_EXT_STAT` parameters
- Use Mission Planner or QGroundControl to request message rates

#### 2. **Message Handlers Not Invoked**

**Symptom:** Handlers exist but are never called.

**Possible causes:**
- Handlers not registered in DI container
- Message pump not routing messages to handlers
- Wrong message ID mapping

**Verification:**
```csharp
// In VehicleMessagePump or handler registration
logger.LogInformation("📦 Registered handlers: {Handlers}", 
    string.Join(", ", registeredHandlers.Keys));

// In message pump dispatch
logger.LogInformation("📨 Dispatching message {MessageId} to handlers", message.MessageId);
```

**Check:**
- DI registration in `MauiProgram.cs` or similar
- Message ID constants match MAVLink spec
- Handler interface implementations

#### 3. **VehicleSession State Not Updated**

**Symptom:** Handlers called but `VehicleSession.State` fields remain null.

**Possible causes:**
- Message decoding errors (wrong field extraction)
- Unit conversion issues (radians ↔ degrees)
- Null checks preventing assignment

**Verification:**
```csharp
public void ApplyAttitude(double roll, double pitch, double yaw)
{
    logger.LogInformation("📊 Applying attitude: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}", 
        roll, pitch, yaw);
    state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };
    logger.LogInformation("📊 State after update: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}", 
        state.Roll, state.Pitch, state.Yaw);
}
```

#### 4. **Timing Issue**

**Symptom:** Data queried before telemetry messages arrive.

**Why this happens:**
- Connection established
- Vehicle registered from HEARTBEAT
- HUD queries data immediately
- But ATTITUDE/GPS/BATTERY messages haven't arrived yet (they may be at lower rates)

**Verification:**
- Wait 5-10 seconds after connection
- Check if data populates over time
- Monitor event timestamps

### Diagnostic Checklist

Run through this checklist to identify the issue:

- [ ] **Verify connection**: Heartbeat received? (✅ Yes - Mode = Stabilize proves this)
- [ ] **Check message reception**: Log all incoming MAVLink messages
  ```csharp
  // In MAVLink client or transport
  logger.LogDebug("📬 Received MAVLink message: ID={MessageId}, Sys={SysId}, Comp={CompId}", 
      message.MessageId, message.SystemId, message.ComponentId);
  ```
- [ ] **Verify handler registration**: Are all handlers in DI container?
- [ ] **Check handler invocation**: Add logs to each handler's `Handle()` method
- [ ] **Monitor state updates**: Log before/after in `ApplyAttitude`, `ApplyPosition`, `ApplyBattery`
- [ ] **Trace event flow**: Log when `VehicleStateUpdated` is published and received
- [ ] **Check telemetry rates**: Use MAVLink inspector to see message frequencies
- [ ] **Wait for data**: Give it 10-30 seconds to accumulate telemetry

### Quick Test Commands

**Request telemetry streams via MAVLink:**
```python
# Using pymavlink or Mission Planner console
# Request ATTITUDE at 10 Hz
connection.mav.request_data_stream_send(
    target_system,
    target_component,
    mavutil.mavlink.MAV_DATA_STREAM_EXTRA1,  # ATTITUDE
    10,  # 10 Hz
    1    # Start
)

# Request POSITION at 5 Hz
connection.mav.request_data_stream_send(
    target_system,
    target_component,
    mavutil.mavlink.MAV_DATA_STREAM_POSITION,  # GPS
    5,   # 5 Hz
    1    # Start
)

# Request SYS_STATUS at 2 Hz
connection.mav.request_data_stream_send(
    target_system,
    target_component,
    mavutil.mavlink.MAV_DATA_STREAM_EXTENDED_STATUS,  # BATTERY
    2,   # 2 Hz
    1    # Start
)
```

**Or set ArduPilot parameters:**
```
SR0_EXTRA1 = 10      # ATTITUDE messages at 10 Hz
SR0_POSITION = 5     # GPS messages at 5 Hz
SR0_EXT_STAT = 2     # Battery/Status at 2 Hz
```

---

## 📝 Adding Serilog File-Based Logging

### Why Serilog for Debugging?

**Benefits for this investigation:**
- ✅ **Structured logging**: Easy to filter by message type, vehicle ID, handler name
- ✅ **File output**: Persistent logs for post-mortem analysis
- ✅ **Async writing**: Won't slow down real-time telemetry processing
- ✅ **Multiple sinks**: Console + File + Debug output simultaneously
- ✅ **Log levels**: Trace message flow without overwhelming output

### Implementation Steps

#### 1. Add Serilog NuGet Packages

Add to relevant projects (`MissionPlanner.App`, `MissionPlanner.Core`):

```xml
<PackageReference Include="Serilog" Version="4.2.0" />
<PackageReference Include="Serilog.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.Debug" Version="3.0.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.1.0" />
```

#### 2. Configure Serilog in MauiProgram.cs

**Location:** `src/UI/MissionPlanner.App/MauiProgram.cs`

```csharp
using Serilog;
using Serilog.Events;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // ✅ Configure Serilog BEFORE any logging calls
        ConfigureSerilog();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { ... });

        // ✅ Replace default logging with Serilog
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: true);

        // ... rest of configuration

        return builder.Build();
    }

    private static void ConfigureSerilog()
    {
        var logFilePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "Logs",
            "missionplanner-.log"
        );

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        Log.Information("🚀 Mission Planner starting...");
        Log.Information("📁 Log file: {LogFile}", logFilePath);
    }
}
```

#### 3. Add Diagnostic Logging to Key Components

**A. MAVLink Message Reception** (`IMavLinkClient` or Transport):

```csharp
public class MavLinkClient
{
    private readonly ILogger<MavLinkClient> logger;

    private void OnMessageReceived(MavLinkMessage message)
    {
        logger.LogTrace("📬 RX: MsgId={MessageId} Sys={Sys} Comp={Comp} Seq={Seq}", 
            message.MessageId, 
            message.SystemId, 
            message.ComponentId,
            message.Sequence);

        // Dispatch to handlers
        messageHub.Publish(message);
    }
}
```

**B. Message Handlers**:

```csharp
public class AttitudeVehicleHandler
{
    public void Handle(AttitudeMessage message)
    {
        logger.LogInformation("🎯 ATTITUDE: Roll={Roll:F2}° Pitch={Pitch:F2}° Yaw={Yaw:F2}° from {VehicleId}", 
            Math.Degrees(message.Roll),
            Math.Degrees(message.Pitch),
            Math.Degrees(message.Yaw),
            new VehicleId(message.SystemId, message.ComponentId));

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        vehicle?.ApplyAttitude(message.Roll, message.Pitch, message.Yaw);

        if (vehicle != null)
        {
            eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
            logger.LogDebug("📤 Published VehicleStateUpdated for {VehicleId}", vehicleId);
        }
    }
}
```

**C. VehicleSession State Updates**:

```csharp
public void ApplyAttitude(double roll, double pitch, double yaw)
{
    logger.LogTrace("📊 Applying attitude to {VehicleId}: R={Roll:F2} P={Pitch:F2} Y={Yaw:F2}", 
        state.VehicleId, roll, pitch, yaw);

    state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };

    logger.LogTrace("📊 State updated: R={Roll} P={Pitch} Y={Yaw}", 
        state.Roll, state.Pitch, state.Yaw);
}
```

**D. VehicleHudDataService**:

```csharp
private void OnVehicleStateUpdated(VehicleStateUpdated evt)
{
    logger.LogDebug("🎨 HUD: Received state update for {VehicleId}", evt.VehicleId);

    var vehicleSession = vehicleRegistry.GetRequired(evt.VehicleId);
    if (vehicleSession == null)
    {
        logger.LogWarning("🎨 HUD: Vehicle {VehicleId} not found in registry", evt.VehicleId);
        return;
    }

    var hudData = TransformToHudData(vehicleSession.State);

    logger.LogDebug("🎨 HUD: Transformed data: Pitch={Pitch:F1}° Roll={Roll:F1}° Alt={Alt:F0}m", 
        hudData.Pitch, hudData.Roll, hudData.Altitude);

    hudDataCache.AddOrUpdate(evt.VehicleId, hudData, (_, _) => hudData);
    hudDataSubject.OnNext(hudData);
}
```

**E. HudViewModel**:

```csharp
private void SubscribeToVehicleData()
{
    var observable = hudDataService
        .ObservePrimaryVehicleHudData()
        .ObserveOn(SynchronizationContext.Current);

    hudDataSubscription = observable.Subscribe(
        hudData =>
        {
            logger.LogTrace("🖥️ HUD UI: Updating display with Pitch={Pitch:F1}° Roll={Roll:F1}°", 
                hudData.Pitch, hudData.Roll);

            Pitch = hudData.Pitch;
            Roll = hudData.Roll;
            Heading = hudData.Heading;
            // ... rest of properties
        },
        error =>
        {
            logger.LogError(error, "🖥️ HUD UI: Error in data stream");
        },
        () =>
        {
            logger.LogWarning("🖥️ HUD UI: Data stream completed");
        });
}
```

#### 4. Log File Location

**Platform-specific paths:**

```csharp
// Windows
C:\Users\{username}\AppData\Local\Packages\{AppPackageId}\LocalState\Logs\missionplanner-20250101.log

// Android
/data/user/0/{package.name}/files/Logs/missionplanner-20250101.log

// iOS
{AppContainer}/Documents/Logs/missionplanner-20250101.log

// macOS
~/Library/Containers/{bundle.id}/Data/Documents/Logs/missionplanner-20250101.log
```

**Helper to show log location:**

```csharp
public static void ShowLogLocation()
{
    var logPath = Path.Combine(FileSystem.AppDataDirectory, "Logs");
    Log.Information("📁 Logs directory: {LogPath}", logPath);

    #if WINDOWS
    // Open folder in Explorer
    Process.Start("explorer.exe", logPath);
    #endif
}
```

#### 5. Structured Logging Best Practices

**Use message templates with properties:**

```csharp
// ✅ Good - Structured
logger.LogInformation("Vehicle {VehicleId} altitude changed from {OldAlt} to {NewAlt}", 
    vehicleId, oldAltitude, newAltitude);

// ❌ Bad - String interpolation (not searchable)
logger.LogInformation($"Vehicle {vehicleId} altitude changed from {oldAltitude} to {newAltitude}");
```

**Filter logs by properties in analysis tools:**

```csharp
// Query: Show all ATTITUDE messages for vehicle 1:1
grep "ATTITUDE.*VehicleId.*1:1" missionplanner-20250101.log

// Or use Serilog analyzers like Seq (optional)
```

### Debugging Workflow with Serilog

1. **Enable detailed logging** (set to `Trace` or `Debug` level)
2. **Connect vehicle** via Serial/TCP/UDP
3. **Let it run for 30 seconds** to collect telemetry
4. **Disconnect**
5. **Open log file** and search for:
   ```
   grep "📬 RX:" missionplanner-20250101.log | head -50
   ```
   → Shows first 50 messages received

6. **Check which message types arrived:**
   ```
   grep "📬 RX:" missionplanner-20250101.log | cut -d' ' -f5 | sort | uniq -c
   ```
   → Counts messages by type

7. **Verify handler invocations:**
   ```
   grep "🎯 ATTITUDE:" missionplanner-20250101.log
   grep "🌍 GPS:" missionplanner-20250101.log
   grep "🔋 BATTERY:" missionplanner-20250101.log
   ```

8. **Trace data flow to HUD:**
   ```
   grep "🎨 HUD:" missionplanner-20250101.log
   grep "🖥️ HUD UI:" missionplanner-20250101.log
   ```

### Expected Log Output (Healthy System)

```
[14:32:10 INF] VehicleConnectionService: Connecting to vehicle using serial port COM10 at 57600 baud
[14:32:11 DBG] 📬 RX: MsgId=0 Sys=1 Comp=1 Seq=42
[14:32:11 INF] HeartbeatVehicleHandler: ❤️ HEARTBEAT from 1:1 Mode=Stabilize Armed=False
[14:32:11 DBG] 📬 RX: MsgId=30 Sys=1 Comp=1 Seq=43
[14:32:11 INF] AttitudeVehicleHandler: 🎯 ATTITUDE: Roll=-2.34° Pitch=0.12° Yaw=45.67° from 1:1
[14:32:11 DBG] 📤 Published VehicleStateUpdated for 1:1
[14:32:11 DBG] 🎨 HUD: Received state update for 1:1
[14:32:11 DBG] 🎨 HUD: Transformed data: Pitch=0.1° Roll=-2.3° Alt=125.0m
[14:32:11 TRC] 🖥️ HUD UI: Updating display with Pitch=0.1° Roll=-2.3°
```

---

## Risk Assessment

### Low Risk ✅
- Adding event publication to handlers (straightforward, follows existing patterns)
- UI thread marshalling (well-documented MAUI/Rx practice)
- Throttling subscriptions (standard Rx operator)

### Medium Risk ⚠️
- Performance impact of high-frequency events (mitigated by throttling)
- Testing with real hardware (requires vehicle/simulator)

### High Risk 🔴
- None identified (architecture is solid)

---

## Solution Implemented ✅

### Root Cause Identified

Log analysis confirmed **only HEARTBEAT messages (MessageId=0) were being received** from ArduPilot. Moving the vehicle physically did not trigger any ATTITUDE, GPS, or BATTERY telemetry because **ArduPilot does not stream these messages by default**—they must be explicitly requested via MAVLink commands.

### Implementation: Automatic Telemetry Stream Requests

**Created three new components:**

1. **MavLinkPacketBuilder** (`src/Core/MissionPlanner.MavLink/Encoding/MavLinkPacketBuilder.cs`)
   - Low-level MAVLink v1 packet construction
   - CRC-16/X.25 checksum calculation with CRC_EXTRA seeds
   - `BuildRequestDataStreamPacket()` method for REQUEST_DATA_STREAM commands

2. **IMavLinkCommandService / MavLinkCommandService** (`src/Core/MissionPlanner.Core/Services/`)
   - High-level command API: `RequestDataStreamAsync()`
   - Wraps packet builder and `IMavLinkClient.SendAsync()`
   - Logs command transmission with 📤 emoji for easy filtering

3. **VehicleConnectionService Integration**
   - Added `RequestTelemetryStreamsAsync()` helper method
   - Called automatically after heartbeat in all connection methods (Serial/TCP/UDP)
   - Requests four essential telemetry streams:
     - **EXTRA1** (10 Hz) → ATTITUDE messages (roll, pitch, yaw)
     - **POSITION** (5 Hz) → GLOBAL_POSITION_INT (GPS, altitude)
     - **EXTENDED_STATUS** (2 Hz) → SYS_STATUS/BATTERY
     - **RAW_SENSORS** (5 Hz) → GPS_RAW_INT, IMU data

### Expected Log Output After Fix

**Before (only HEARTBEAT):**
```
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
```

**After (telemetry streams requested):**
```
[INF] Successfully connected to vehicle 1:1 via serial port COM10
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=Extra1(10) rate=10Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=Position(6) rate=5Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=ExtendedStatus(2) rate=2Hz START
[INF] 📤 Sent REQUEST_DATA_STREAM to 1:1: stream=RawSensors(1) rate=5Hz START
[INF] ✅ Telemetry streams requested for vehicle 1:1
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=0 as HeartbeatMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=30 as AttitudeMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=33 as GlobalPositionIntMessage
[INF] MavLinkMessageDecoder - Decoded MAVLink frame MessageId=1 as SysStatusMessage
[DBG] VehicleHudDataService-Vehicle state updated for 1:1: VehicleState { 
    Pitch = 2.3, Roll = -1.2, Altitude = 125.7, BatteryVoltage = 12.4, ... 
}
```

### Verification Steps

1. **Build**: ✅ Solution builds successfully
2. **Connect to vehicle** via Serial COM10 @ 115200
3. **Check logs** for:
   - 📤 REQUEST_DATA_STREAM commands sent
   - MessageId=30 (ATTITUDE), 33 (GPS), 1 (BATTERY) decoded
   - Non-zero values in `VehicleHudData`
4. **Monitor HUD UI** for live pitch/roll/altitude updates

### Expected Outcome

- **VehicleHudData** should now populate with real telemetry:
  - `Pitch`, `Roll`, `Heading` → from ATTITUDE messages
  - `Altitude`, `Latitude`, `Longitude` → from GLOBAL_POSITION_INT
  - `BatteryVoltage`, `BatteryRemaining` → from SYS_STATUS
  - `AirSpeed`, `GroundSpeed` → from VFR_HUD (if vehicle supports)

---

## Conclusion

**Current Status:** ✅ **Solution Implemented!** The system now automatically requests telemetry streams from ArduPilot after connection.

**What was fixed:**
1. ✅ Added MAVLink packet builder with CRC-16/X.25 calculation
2. ✅ Created MAVLink command service for sending REQUEST_DATA_STREAM
3. ✅ Integrated telemetry requests into connection flow (Serial/TCP/UDP)
4. ✅ Build successful - ready for testing

### Next Steps (Testing & Validation)

1. 🧪 **Connect to vehicle** and verify telemetry arrival:
   - Watch logs for 📤 REQUEST_DATA_STREAM commands
   - Confirm ATTITUDE (MessageId=30), GPS (33), BATTERY (1) messages arrive
   - Verify `VehicleHudData` contains non-zero values
2. 🎨 **UI Thread Marshaling** (if HUD doesn't update):
   - Add `.ObserveOn(SynchronizationContext.Current)` to `HudViewModel`
   - Ensures UI updates on main thread
3. ⚡ **Performance Tuning** (if needed):
   - Adjust stream rates if too much data
   - Add throttling/sampling if event rate causes UI lag

### Recommended Test Workflow

```
1. Build solution (already successful ✅)
2. Run app on Windows
3. Connect → Serial → COM10 @ 115200
4. Wait 5 seconds for streams to start
5. Move vehicle physically (change attitude/position)
6. Observe HUD UI updating in real-time
7. Check logs for message flow
8. Disconnect
```

**Estimated Time to Validation:** 5-10 minutes with real hardware.

---

## Alternative: Manual Telemetry Configuration

If automatic requests don't work with a specific firmware, you can also configure ArduPilot parameters:

```python
# Mission Planner CLI or MAVProxy
param set SR0_EXTRA1 10    # ATTITUDE @ 10 Hz
param set SR0_POSITION 5   # GPS @ 5 Hz
param set SR0_EXT_STAT 2   # BATTERY @ 2 Hz
param set SR0_RAW_SENS 5   # IMU/GPS raw @ 5 Hz
param write
```

Or use the GUI **Config/Tuning → Full Parameter List** and search for `SR0_*`.

---

## Previous Recommendation (Completed ✅)

~~1. Add `IDomainEventHub` to handler constructors~~ ✅ DONE
~~2. Call `eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State))` after state updates~~ ✅ DONE
~~3. Add `ObserveOn(SynchronizationContext.Current)` to HUD subscription~~ ✅ DONE
4. Test with real vehicle (IN PROGRESS - need more telemetry)
5. ~~Optimize only if needed~~ (Not needed yet)
