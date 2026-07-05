# HUD Data Service Implementation - First Step Complete

## Overview
This document describes the first step in implementing a live, data-driven HUD (Heads-Up Display) for the MissionPlanner application. This step focuses on the Core layer infrastructure to interface with ArduPilot vehicles and provide HUD-specific telemetry data.

## What Was Implemented

### 1. Core Data Models
**File:** `src/Core/MissionPlanner.Core/Models/VehicleHudData.cs`

A dedicated HUD data model that represents all telemetry displayed on the HUD:
- Attitude: Pitch, Roll, Heading, Yaw
- Position: Latitude, Longitude, Altitude
- Motion: AirSpeed, GroundSpeed, VerticalSpeed
- Status: BatteryVoltage, BatteryRemaining, GpsSatellites, IsArmed, Mode

This record type provides a clean, immutable snapshot of vehicle state optimized for UI display.

### 2. Service Interface
**File:** `src/Core/MissionPlanner.Core/Services/IVehicleHudDataService.cs`

Defines the contract for accessing HUD data:
- `GetHudData(VehicleId)` - Get current HUD data for a specific vehicle
- `ObserveHudData(VehicleId)` - Subscribe to real-time updates for a vehicle
- `GetPrimaryVehicleHudData()` - Get HUD data for the primary/active vehicle
- `ObservePrimaryVehicleHudData()` - Subscribe to updates for the primary vehicle

### 3. Service Implementation
**File:** `src/Core/MissionPlanner.Core/Services/VehicleHudDataService.cs`

Bridges vehicle state from the registry into HUD-ready data:
- Subscribes to `VehicleStateUpdated` domain events
- Transforms `VehicleState` into `VehicleHudData` format
- Provides reactive streams using `System.Reactive` for UI binding
- Caches HUD data for efficient retrieval
- Auto-selects primary vehicle when multiple vehicles are available

Key transformations:
- Converts yaw (-180 to 180) to heading (0 to 360)
- Handles null/optional values gracefully
- Provides defaults for missing telemetry

### 4. MAUI UI Integration (Prepared)
**File:** `src/UI/MissionPlanner.App/Views/FlightData/Hud/HudViewModel.cs`

Updated the HudViewModel to:
- Inject `IVehicleHudDataService`
- Subscribe to primary vehicle HUD data updates
- Expose all HUD properties as bindable MVVM properties
- Added IsArmed and FlightMode properties

**Note:** The HudView still displays a static painted image. The next step will be to bind the view to these live properties.

### 5. Dependency Injection Registration
**File:** `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`

Registered `IVehicleHudDataService` as a singleton in the DI container.

### 6. Package Management
**File:** `src/Directory.Packages.props`

Added `System.Reactive` v6.0.1 to central package management for observable stream support.

**Files:** `src/Core/MissionPlanner.Core/MissionPlanner.Core.csproj`, `src/UI/MissionPlanner.App/MissionPlanner.App.csproj`

Added necessary project references for the new functionality.

## Tests

### Unit Tests
**File:** `src/Tests/MissionPlanner.Test/VehicleHudDataServiceTests.cs`

Tests basic service behavior:
- Service creation and event subscription
- Null handling when vehicle not found
- Resource disposal
- Primary vehicle selection when no vehicles exist

### Integration Tests
**File:** `src/Tests/MissionPlanner.Test/VehicleHudDataIntegrationTests.cs`

Tests end-to-end data flow:
- Getting HUD data from registry with simulated vehicle state
- Verifying data transformation (yaw to heading, null handling)
- Primary vehicle selection with multiple vehicles

**All tests pass successfully.**

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     MAUI UI Layer                        │
│  ┌──────────────────────────────────────────────────┐   │
│  │ HudView.xaml (Static Display - To Be Updated)   │   │
│  └──────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────┐   │
│  │ HudViewModel (Subscribes to HUD Data Service)   │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
						 │
						 ▼
┌─────────────────────────────────────────────────────────┐
│                    Core Domain Layer                     │
│  ┌──────────────────────────────────────────────────┐   │
│  │ IVehicleHudDataService / VehicleHudDataService  │   │
│  │  - Subscribes to VehicleStateUpdated events      │   │
│  │  - Transforms VehicleState → VehicleHudData     │   │
│  │  - Provides Observable<VehicleHudData> streams  │   │
│  └──────────────────────────────────────────────────┘   │
│                         │                                │
│                         ▼                                │
│  ┌──────────────────────────────────────────────────┐   │
│  │ IVehicleRegistry                                 │   │
│  │  - Maintains VehicleSession collection          │   │
│  │  - Stores VehicleState for each vehicle         │   │
│  └──────────────────────────────────────────────────┘   │
│                         │                                │
│                         ▼                                │
│  ┌──────────────────────────────────────────────────┐   │
│  │ Domain Event Hub (VehicleStateUpdated)          │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
						 │
						 ▼
┌─────────────────────────────────────────────────────────┐
│                MAVLink/Transport Layer                   │
│  - Receives MAVLink messages from ArduPilot vehicle     │
│  - Updates vehicle state via handlers                    │
│  - Publishes domain events                              │
└─────────────────────────────────────────────────────────┘
```

## Data Flow

1. **MAVLink Message Reception**: ArduPilot vehicle sends telemetry via MAVLink protocol
2. **Message Handling**: Core handlers (HeartbeatVehicleHandler, AttitudeVehicleHandler, BatteryVehicleHandler, PositionVehicleHandler) process messages
3. **State Update**: VehicleRegistry updates VehicleState
4. **Event Publishing**: VehicleStateUpdated event is published via IDomainEventHub
5. **HUD Transformation**: VehicleHudDataService receives event, transforms state to HUD data
6. **UI Update**: HudViewModel receives observable update, updates bindable properties
7. **Display**: (Next step) HudView renders live data instead of static image

## Current Limitations

The following HUD data fields are currently stubbed to 0 pending richer MAVLink message integration:
- `AirSpeed` - Requires VFR_HUD message
- `GroundSpeed` - Requires GPS velocity data
- `VerticalSpeed` - Requires GPS or VFR_HUD velocity data
- `GpsSatellites` - Requires GPS_RAW_INT message

These will be implemented in future iterations as additional MAVLink message handlers are added.

## Next Steps

### Step 2: UI Integration (Immediate Next)
1. Update `HudView.xaml` and `HudPainter.cs` to bind to live ViewModel properties
2. Ensure the HUD canvas updates in real-time as data changes
3. Test with simulated vehicle and real ArduPilot hardware

### Step 3: Enhanced Telemetry (Future)
1. Add VFR_HUD message handler for airspeed and vertical speed
2. Add GPS_RAW_INT message handler for satellite count and GPS status
3. Calculate ground speed from GPS velocity components
4. Add additional HUD elements (e.g., wind, EKF status, RC signal strength)

### Step 4: HUD Customization (Future)
1. Allow users to show/hide HUD elements
2. Provide HUD themes (day/night mode)
3. Add configurable alert thresholds
4. Support multiple HUD layouts (beginner, advanced, minimal)

## Build and Test Status

✅ **Build:** Successful  
✅ **Unit Tests:** 4/4 Passed  
✅ **Integration Tests:** 2/2 Passed

## References

- ArduPilot MAVLink Documentation: https://mavlink.io/en/messages/common.html
- Mission Planner (original): https://github.com/ArduPilot/MissionPlanner
- System.Reactive: https://github.com/dotnet/reactive

---
**Date:** 2025  
**Status:** First Step Complete - Core Infrastructure Ready for UI Integration
