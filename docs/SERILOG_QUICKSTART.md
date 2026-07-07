# Serilog Integration Quick-Start Guide

## Overview

This guide shows how to add Serilog file-based logging to Mission Planner to debug the zero/null HUD data issue.

## Why Serilog?

- ✅ **Structured logging** - Filter by vehicle ID, message type, component
- ✅ **File persistence** - Analyze logs after disconnection
- ✅ **Multiple outputs** - Console + File + Debug window simultaneously
- ✅ **Async writing** - Won't slow down real-time telemetry
- ✅ **Production-ready** - Used by thousands of .NET applications

## Installation

### 1. Add NuGet Packages

Add to `src/UI/MissionPlanner.App/MissionPlanner.App.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Serilog" Version="4.2.0" />
  <PackageReference Include="Serilog.Extensions.Logging" Version="9.0.0" />
  <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
  <PackageReference Include="Serilog.Sinks.Debug" Version="3.0.0" />
  <PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
</ItemGroup>
```

### 2. Configure in MauiProgram.cs

**File:** `src/UI/MissionPlanner.App/MauiProgram.cs`

```csharp
using Serilog;
using Serilog.Events;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		// ✅ STEP 1: Configure Serilog before anything else
		ConfigureSerilog();

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		// ✅ STEP 2: Replace Microsoft.Extensions.Logging with Serilog
		builder.Logging.ClearProviders();
		builder.Logging.AddSerilog(dispose: true);

		// ... rest of your configuration (DI, etc.)

		return builder.Build();
	}

	private static void ConfigureSerilog()
	{
		// Log files will go to platform-specific app data folder
		var logFilePath = Path.Combine(
			FileSystem.AppDataDirectory,
			"Logs",
			"missionplanner-.log"  // Date appended automatically
		);

		// Ensure log directory exists
		var logDir = Path.GetDirectoryName(logFilePath);
		if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
		{
			Directory.CreateDirectory(logDir);
		}

		Log.Logger = new LoggerConfiguration()
			// Minimum log level
			.MinimumLevel.Debug()

			// Reduce noise from framework
			.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
			.MinimumLevel.Override("System", LogEventLevel.Warning)

			// Add context enrichers
			.Enrich.FromLogContext()
			.Enrich.WithThreadId()

			// Console output (for debugging in IDE)
			.WriteTo.Console(
				outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")

			// Debug output (Visual Studio Output window)
			.WriteTo.Debug(
				outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")

			// File output (persistent logs)
			.WriteTo.File(
				logFilePath,
				rollingInterval: RollingInterval.Day,     // New file each day
				retainedFileCountLimit: 7,                // Keep last 7 days
				outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
				shared: true,                             // Allow multiple processes
				flushToDiskInterval: TimeSpan.FromSeconds(1))  // Write every second

			.CreateLogger();

		Log.Information("🚀 Mission Planner starting...");
		Log.Information("📁 Log file: {LogFile}", logFilePath);
	}
}
```

### 3. Find Your Log Files

**Log file location by platform:**

| Platform | Path |
|----------|------|
| **Windows** | `C:\Users\{username}\AppData\Local\Packages\{AppPackageId}\LocalState\Logs\` |
| **Android** | `/data/user/0/{package.name}/files/Logs/` |
| **iOS** | `{AppContainer}/Documents/Logs/` |
| **macOS** | `~/Library/Containers/{bundle.id}/Data/Documents/Logs/` |

**Helper method to open log folder (Windows):**

```csharp
public static void OpenLogsFolder()
{
	var logPath = Path.Combine(FileSystem.AppDataDirectory, "Logs");

	#if WINDOWS
	if (Directory.Exists(logPath))
	{
		Process.Start("explorer.exe", logPath);
	}
	#endif

	Log.Information("📁 Logs folder: {LogPath}", logPath);
}
```

## Adding Diagnostic Logging

### Message Reception Logging

**File:** `src/Core/MissionPlanner.MavLink/Client/MavLinkClient.cs` (or similar)

```csharp
public class MavLinkClient : IMavLinkClient
{
	private readonly ILogger<MavLinkClient> logger;

	private void OnPacketReceived(MavLinkPacket packet)
	{
		// ✅ Log every received message
		logger.LogTrace("📬 RX: MsgId={MessageId} Sys={SystemId} Comp={ComponentId} Seq={Sequence}", 
			packet.MessageId, 
			packet.SystemId, 
			packet.ComponentId,
			packet.Sequence);

		// Decode and dispatch
		var message = DecodeMessage(packet);
		messageHub.Publish(message);
	}
}
```

### Handler Invocation Logging

**Example:** `src/Core/MissionPlanner.Core/VehicleHandler/AttitudeVehicleHandler.cs`

```csharp
public sealed class AttitudeVehicleHandler(
	IVehicleRegistry vehicleRegistry,
	IDomainEventHub eventHub,
	ILogger<AttitudeVehicleHandler> logger) 
	: IAttitudeVehicleHandler
{
	public void Handle(AttitudeMessage message)
	{
		var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

		// ✅ Log handler invocation with data
		logger.LogInformation("🎯 ATTITUDE: Roll={Roll:F2}° Pitch={Pitch:F2}° Yaw={Yaw:F2}° from {VehicleId}", 
			RadiansToDegrees(message.Roll),
			RadiansToDegrees(message.Pitch),
			RadiansToDegrees(message.Yaw),
			vehicleId);

		var vehicle = vehicleRegistry.GetRequired(vehicleId);

		if (vehicle == null)
		{
			logger.LogWarning("🎯 ATTITUDE: Vehicle {VehicleId} not found in registry", vehicleId);
			return;
		}

		vehicle.ApplyAttitude(message.Roll, message.Pitch, message.Yaw);

		// ✅ Log event publication
		eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
		logger.LogDebug("📤 Published VehicleStateUpdated for {VehicleId}", vehicleId);
	}

	private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
```

**Repeat similar logging for:**
- `HeartbeatVehicleHandler.cs`
- `GlobalPositionVehicleHandler.cs`
- `BatteryStatusVehicleHandler.cs`
- `SysStatusVehicleHandler.cs`

### State Update Logging

**File:** `src/Core/MissionPlanner.Core/Services/VehicleSession.cs`

```csharp
public class VehicleSession
{
	private VehicleState state;
	private readonly ILogger<VehicleSession> logger;

	public void ApplyAttitude(double roll, double pitch, double yaw)
	{
		logger.LogTrace("📊 Applying attitude to {VehicleId}: R={Roll:F2} P={Pitch:F2} Y={Yaw:F2}", 
			state.VehicleId, roll, pitch, yaw);

		state = state with { Roll = roll, Pitch = pitch, Yaw = yaw };

		logger.LogTrace("📊 State updated: R={Roll} P={Pitch} Y={Yaw}", 
			state.Roll, state.Pitch, state.Yaw);
	}

	public void ApplyPosition(double latitude, double longitude, double altitude)
	{
		logger.LogTrace("📊 Applying position to {VehicleId}: Lat={Lat:F6} Lon={Lon:F6} Alt={Alt:F1}", 
			state.VehicleId, latitude, longitude, altitude);

		state = state with { Latitude = latitude, Longitude = longitude, Altitude = altitude };
	}

	public void ApplyBattery(int? batteryRemaining, float? batteryVoltage)
	{
		logger.LogTrace("📊 Applying battery to {VehicleId}: {Voltage:F2}V {Remaining}%", 
			state.VehicleId, batteryVoltage, batteryRemaining);

		state = state with { BatteryRemaining = batteryRemaining, BatteryVoltage = batteryVoltage };
	}
}
```

### HUD Service Logging

**File:** `src/Core/MissionPlanner.Core/Services/VehicleHudDataService.cs`

```csharp
public sealed class VehicleHudDataService : IVehicleHudDataService, IDisposable
{
	private readonly ILogger<VehicleHudDataService> logger;

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

		// ✅ Log transformed HUD data
		logger.LogDebug("🎨 HUD: Transformed data for {VehicleId}: Pitch={Pitch:F1}° Roll={Roll:F1}° Alt={Alt:F0}m Battery={Voltage:F1}V", 
			hudData.VehicleId,
			hudData.Pitch, 
			hudData.Roll, 
			hudData.Altitude,
			hudData.BatteryVoltage);

		hudDataCache.AddOrUpdate(evt.VehicleId, hudData, (_, _) => hudData);
		hudDataSubject.OnNext(hudData);
	}
}
```

### HUD ViewModel Logging

**File:** `src/UI/MissionPlanner.App/Views/FlightData/Hud/HudViewModel.cs`

```csharp
public partial class HudViewModel : ObservableObject, IDisposable
{
	private readonly ILogger<HudViewModel> logger;

	private void SubscribeToVehicleData()
	{
		var observable = hudDataService
			.ObservePrimaryVehicleHudData()
			.ObserveOn(SynchronizationContext.Current);

		hudDataSubscription = observable.Subscribe(
			onNext: hudData =>
			{
				logger.LogTrace("🖥️ HUD UI: Updating display - Pitch={Pitch:F1}° Roll={Roll:F1}° Alt={Alt:F0}m", 
					hudData.Pitch, hudData.Roll, hudData.Altitude);

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
			},
			onError: error =>
			{
				logger.LogError(error, "🖥️ HUD UI: Error in data stream");
			},
			onCompleted: () =>
			{
				logger.LogWarning("🖥️ HUD UI: Data stream completed unexpectedly");
			});
	}
}
```

## Debugging Workflow

### 1. Run Application with Logging

```bash
# Build and run
dotnet build
dotnet run --project src/UI/MissionPlanner.App/MissionPlanner.App.csproj
```

### 2. Connect to Vehicle

- Open Mission Planner
- Go to Connect page
- Select COM10 (or appropriate port)
- Baud rate: 57600 (or as configured)
- Click Connect
- Wait 30 seconds
- Disconnect

### 3. Analyze Log File

**Find the log file:**

```powershell
# PowerShell
$logPath = "$env:LOCALAPPDATA\Packages\{YourAppPackage}\LocalState\Logs"
explorer $logPath

# Or in C#
var logPath = Path.Combine(FileSystem.AppDataDirectory, "Logs");
```

**Check what messages were received:**

```bash
# PowerShell
Select-String -Path .\missionplanner-20250101.log -Pattern "📬 RX:" | Select-Object -First 100

# Bash
grep "📬 RX:" missionplanner-20250101.log | head -100
```

**Count message types:**

```powershell
# PowerShell
Select-String -Path .\missionplanner-20250101.log -Pattern "📬 RX:" | 
	ForEach-Object { $_.Line -match "MsgId=(\d+)" | Out-Null; $matches[1] } | 
	Group-Object | 
	Sort-Object Count -Descending

# Expected output:
# Count Name
# ----- ----
#   100 0       # HEARTBEAT
#    50 30      # ATTITUDE
#    25 33      # GLOBAL_POSITION_INT
#    10 1       # SYS_STATUS
```

**Check handler invocations:**

```bash
grep "🎯 ATTITUDE:" missionplanner-20250101.log
grep "🌍 GPS:" missionplanner-20250101.log
grep "🔋 BATTERY:" missionplanner-20250101.log
```

**Trace data to HUD:**

```bash
grep "🎨 HUD:" missionplanner-20250101.log | tail -20
grep "🖥️ HUD UI:" missionplanner-20250101.log | tail -20
```

## Expected Log Output (Healthy System)

```
2025-01-01 14:32:10.123 [INF] [1] MissionPlanner.Core.Services.VehicleConnectionService: Connecting to vehicle using serial port COM10 at 57600 baud
2025-01-01 14:32:11.456 [TRC] [8] MissionPlanner.MavLink.Client.MavLinkClient: 📬 RX: MsgId=0 Sys=1 Comp=1 Seq=42
2025-01-01 14:32:11.457 [INF] [8] MissionPlanner.Core.VehicleHandler.HeartbeatVehicleHandler: ❤️ HEARTBEAT from 1:1 Mode=Stabilize Armed=False
2025-01-01 14:32:11.520 [TRC] [8] MissionPlanner.MavLink.Client.MavLinkClient: 📬 RX: MsgId=30 Sys=1 Comp=1 Seq=43
2025-01-01 14:32:11.521 [INF] [8] MissionPlanner.Core.VehicleHandler.AttitudeVehicleHandler: 🎯 ATTITUDE: Roll=-2.34° Pitch=0.12° Yaw=45.67° from 1:1
2025-01-01 14:32:11.522 [TRC] [8] MissionPlanner.Core.Services.VehicleSession: 📊 Applying attitude to 1:1: R=-2.34 P=0.12 Y=45.67
2025-01-01 14:32:11.523 [DBG] [8] MissionPlanner.Core.VehicleHandler.AttitudeVehicleHandler: 📤 Published VehicleStateUpdated for 1:1
2025-01-01 14:32:11.524 [DBG] [12] MissionPlanner.Core.Services.VehicleHudDataService: 🎨 HUD: Received state update for 1:1
2025-01-01 14:32:11.525 [DBG] [12] MissionPlanner.Core.Services.VehicleHudDataService: 🎨 HUD: Transformed data for 1:1: Pitch=0.1° Roll=-2.3° Alt=125.0m Battery=12.4V
2025-01-01 14:32:11.530 [TRC] [1] MissionPlanner.App.Views.FlightData.Hud.HudViewModel: 🖥️ HUD UI: Updating display - Pitch=0.1° Roll=-2.3° Alt=125.0m
```

## Troubleshooting

### Issue: No log file created

**Cause:** Serilog not configured before first log call

**Fix:** Move `ConfigureSerilog()` to **first line** in `CreateMauiApp()`

---

### Issue: Log file empty or missing entries

**Cause:** File sink buffering or app crash before flush

**Fix:** Add `flushToDiskInterval: TimeSpan.FromSeconds(1)` to File sink (already in example above)

---

### Issue: Can't find log folder

**Solution:** Add this helper method:

```csharp
public static void ShowLogPath()
{
	var path = Path.Combine(FileSystem.AppDataDirectory, "Logs");
	Console.WriteLine($"Log path: {path}");

	#if DEBUG
	if (Debugger.IsAttached)
	{
		Debug.WriteLine($"📁 Log path: {path}");
	}
	#endif
}
```

---

### Issue: Too many log messages (file too large)

**Fix:** Increase minimum log level for noisy components:

```csharp
.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
.MinimumLevel.Override("System", LogEventLevel.Warning)
.MinimumLevel.Override("MissionPlanner.MavLink.Client", LogEventLevel.Information)  // Reduce MAVLink client noise
```

## Next Steps After Adding Logging

1. ✅ Install Serilog packages
2. ✅ Configure in `MauiProgram.cs`
3. ✅ Add diagnostic logging to handlers and services
4. 🔍 Connect to vehicle and capture 30+ seconds of telemetry
5. 📊 Analyze log file to identify:
   - Which MAVLink messages are received
   - Are handlers being invoked
   - Is `VehicleState` being updated
   - Are events reaching HUD service
6. 🛠️ Fix identified issue based on log analysis

## Useful Log Analysis Queries

```powershell
# PowerShell commands for log analysis

# Show all unique message IDs received
Select-String -Path .\missionplanner-*.log -Pattern "📬 RX:" | 
	ForEach-Object { if ($_ -match "MsgId=(\d+)") { $matches[1] } } | 
	Sort-Object -Unique

# Count messages per second
Select-String -Path .\missionplanner-*.log -Pattern "📬 RX:" | 
	ForEach-Object { $_.Line.Substring(0, 23) } |  # Extract timestamp
	Group-Object | 
	Measure-Object -Property Count -Average

# Find gaps in message reception (>1 second)
Select-String -Path .\missionplanner-*.log -Pattern "📬 RX:" | 
	Select-Object -First 100 | 
	ForEach-Object { $_.Line.Substring(0, 23) }

# Show all error/warning messages
Select-String -Path .\missionplanner-*.log -Pattern "\[ERR\]|\[WRN\]"

# Trace a specific vehicle's data flow
Select-String -Path .\missionplanner-*.log -Pattern "1:1"
```

---

## Summary

With Serilog logging in place, you'll have complete visibility into:
- ✅ Which MAVLink messages arrive from the vehicle
- ✅ Which handlers are invoked (or not invoked)
- ✅ How `VehicleState` is populated
- ✅ Whether events reach the HUD service
- ✅ Whether the HUD UI receives updates

This will quickly identify why you're seeing zero/null data in the HUD display.

**Most likely root cause:** Only HEARTBEAT messages are being received. You need to request telemetry streams via MAVLink `REQUEST_DATA_STREAM` commands or configure ArduPilot parameters (`SR*_EXTRA1`, `SR*_POSITION`, etc.) to enable streaming of ATTITUDE, GPS, and BATTERY messages.
