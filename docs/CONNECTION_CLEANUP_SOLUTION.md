# Connection Cleanup on App Close

## Problem
When closing the MissionPlanner app using the window close button (or any exit method), the serial port (COM10, USB, etc.) remained occupied/locked, causing "access denied" errors on subsequent connection attempts.

## Root Cause
1. `VehicleConnectionService` was registered as **Transient**, meaning instances were not tracked by the DI container for proper disposal
2. No cleanup logic existed in the app lifecycle to dispose the connection service when the window closed
3. Serial port resources were not being released properly on app shutdown

## Solution Implemented

### 1. Changed Service Lifetimes to Singleton

**File:** `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`

```csharp
// Changed from Transient to Singleton
services.TryAddSingleton<IVehicleConnectionService, VehicleConnectionService>();
services.TryAddSingleton<IVehicleHudDataService, VehicleHudDataService>();
```

**Rationale:**
- `VehicleConnectionService` should be a singleton since only one active connection is supported
- Singleton lifetime ensures the DI container tracks it and allows proper disposal
- Matches the semantic intent: one connection per application instance

**File:** `src/UI/MissionPlanner.App/Configuration/ApplicationConfigurator.cs`

```csharp
// Changed from Transient to Singleton
services.TryAddSingleton<ConnectPopupView>();
services.TryAddSingleton<ConnectPopupViewModel>();
```

**Rationale:**
- `ConnectPopupViewModel` already implements `IAsyncDisposable` and calls `connectionService.DisposeAsync()`
- Singleton ensures it's properly disposed when the app shuts down
- Matches other view models which are already singletons

### 2. Added Window Cleanup Handler

**File:** `src/UI/MissionPlanner.App/App.xaml.cs`

Added:
- Storage of `IServiceProvider` reference in `CreateWindow` (instead of direct service reference)
- `Window.Destroying` event handler to resolve and dispose the connection service
- Exception handling to prevent crashes during cleanup
- Proper null checking and error handling

```csharp
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library;

namespace MissionPlanner.App;

public partial class App : Application
{
	private IServiceProvider serviceProvider;

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (activationState is not null)
		{
			var topbarView = activationState.Context.Services.GetRequiredService<TopBarView>()!;
			// Store service provider reference for cleanup
			serviceProvider = activationState.Context.Services;
			var window = new Window(new AppShell(topbarView));
			// Handle window destruction to ensure connection cleanup
			window.Destroying += OnWindowDestroying;
			return window;
		}
		else
		{
			throw new DomainException("Error Initializing Application");
		}
	}

	private async void OnWindowDestroying(object? sender, EventArgs e)
	{
		// Ensure the connection is properly closed when the app is closing
		try
		{
			var connectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
			await connectionService.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error disposing connection service: {ex.Message}");
		}
	}
}
```

**Key Implementation Details:**
- Stores `IServiceProvider` instead of direct service reference (cleaner, more flexible)
- Resolves `IVehicleConnectionService` at disposal time
- Includes proper exception handling and debug output
- Uses `ConfigureAwait(false)` for async disposal

### 3. Existing Disposal Contract

`VehicleConnectionService` already implemented `IAsyncDisposable` (added previously):

```csharp
public async ValueTask DisposeAsync()
{
	if (disposed) return;
	disposed = true;

	await semaphore.WaitAsync().ConfigureAwait(false);
	try
	{
		await DisconnectInternalAsync(CancellationToken.None).ConfigureAwait(false);
	}
	finally
	{
		semaphore.Release();
		semaphore.Dispose();
	}
}
```

## How It Works

1. **App starts:** `VehicleConnectionService` is registered as singleton and created on first use
2. **User connects:** Serial/TCP/UDP connection is established
3. **User closes window:** 
   - MAUI fires `Window.Destroying` event
   - `OnWindowDestroying` handler resolves `IVehicleConnectionService` from DI
   - Handler calls `connectionService.DisposeAsync()`
   - `DisposeAsync` calls `DisconnectInternalAsync()` to close the connection
   - Serial port/TCP/UDP resources are released
4. **Port is now free:** Subsequent app launches can connect to the same port

## Testing Checklist

- [x] Build successful
- [x] Singleton lifetime applied to connection service
- [x] Window cleanup handler implemented
- [ ] Connect to serial port (e.g., COM10)
- [ ] Close app using window X button
- [ ] Verify port is released (no "access denied")
- [ ] Restart app and reconnect to same port successfully
- [ ] Test with TCP connection
- [ ] Test with UDP connection
- [ ] Verify HUD still works correctly after changes

## Files Modified

1. `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`
   - Changed `IVehicleConnectionService` and `IVehicleHudDataService` to Singleton

2. `src/UI/MissionPlanner.App/Configuration/ApplicationConfigurator.cs`
   - Changed `ConnectPopupView` and `ConnectPopupViewModel` to Singleton

3. `src/UI/MissionPlanner.App/App.xaml.cs`
   - Added `IServiceProvider` field for service resolution
   - Added `OnWindowDestroying` event handler
   - Added proper null checking and exception handling
   - Injected using statements for `MissionPlanner.Core.Services.Abstractions` and `MissionPlanner.Library`

## Related Context

- `IVehicleConnectionService` already extends `IAsyncDisposable` (implemented earlier)
- `ConnectPopupViewModel` already implements `IAsyncDisposable` and calls `connectionService.DisposeAsync()`
- The connection service uses `SemaphoreSlim` to ensure thread-safe disposal
- `DisconnectInternalAsync` properly closes MAVLink clients and cancels background tasks

---

## Future Enhancements

### Bluetooth Connection Support (Future Implementation)

**Status:** Planned for future development

#### Overview
Add Bluetooth/BLE support for wireless vehicle connection, particularly useful for:
- Mobile devices (Android/iOS tablets in the field)
- Bluetooth MAVLink telemetry modules (e.g., HC-05, HC-06, RFD900x BT)
- Reducing cable clutter in testing environments

#### Platform Support Matrix

| Platform | Classic Bluetooth | Bluetooth Low Energy (BLE) | Notes |
|----------|-------------------|---------------------------|-------|
| Windows  | ✅ Yes | ✅ Yes | Via Windows.Devices.Bluetooth |
| Android  | ✅ Yes | ✅ Yes | Via Android.Bluetooth |
| iOS      | ❌ Limited | ✅ Yes | Apple restricts classic BT |
| macOS    | ❌ Limited | ✅ Yes | Apple restricts classic BT |

**Recommendation:** Focus on **BLE** for cross-platform compatibility.

#### Architecture Plan

##### 1. New Transport Layer

**File:** `src/Core/MissionPlanner.Transport/BluetoothMavLinkTransport.cs`

```csharp
public interface IBluetoothMavLinkTransport : IMavLinkTransport
{
    Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken);
    Task<IEnumerable<BluetoothDevice>> ScanForDevicesAsync(CancellationToken cancellationToken);
}

public class BluetoothMavLinkTransport : IBluetoothMavLinkTransport
{
    // Platform-specific BLE GATT connection
    // MAVLink data typically sent over custom GATT characteristic
    // UUID: Standard MAVLink BLE service (or vendor-specific)
}
```

##### 2. Service Discovery

**File:** `src/Core/MissionPlanner.Core/Services/BluetoothDiscoveryService.cs`

```csharp
public interface IBluetoothDiscoveryService
{
    Task<IEnumerable<BluetoothDevice>> DiscoverDevicesAsync(
        TimeSpan scanDuration, 
        CancellationToken cancellationToken);

    Task<bool> IsPairedAsync(string deviceId);
}

public record BluetoothDevice(
    string Id,
    string Name,
    string Address,
    bool IsPaired,
    int SignalStrength);
```

##### 3. Connection Service Updates

**File:** `src/Core/MissionPlanner.Core/Services/VehicleConnectionService.cs`

Add method:
```csharp
public async Task<VehicleId> ConnectBluetoothAsync(
    string deviceId, 
    CancellationToken cancellationToken)
{
    // Similar pattern to Serial/TCP/UDP
    // 1. Create transport
    // 2. Wait for heartbeat
    // 3. Request telemetry streams
    // 4. Setup message pump
}
```

##### 4. UI Updates

**File:** `src/UI/MissionPlanner.App/Views/Connect/ConnectPopupViewModel.cs`

Add:
- `ObservableCollection<BluetoothDevice> BluetoothDevices`
- `ScanForBluetoothDevicesCommand`
- `ConnectBluetoothCommand`
- Platform permission checks (Bluetooth, Location on Android)

#### Platform-Specific Implementation

##### Windows (.NET MAUI / WinUI)
```csharp
#if WINDOWS
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

// Use BluetoothLEDevice.FromIdAsync()
// Connect to MAVLink GATT service/characteristic
#endif
```

##### Android
```csharp
#if ANDROID
using Android.Bluetooth;
using Android.Bluetooth.LE;

// Use BluetoothAdapter and BluetoothGatt
// Request BLUETOOTH_CONNECT, BLUETOOTH_SCAN permissions
// Request FINE_LOCATION for Android 12+ BLE scanning
#endif
```

##### iOS/macOS
```csharp
#if IOS || MACCATALYST
using CoreBluetooth;

// Use CBCentralManager
// Request Bluetooth permissions in Info.plist
// NSBluetoothAlwaysUsageDescription
#endif
```

#### MAVLink over BLE Specification

**Standard UUIDs:**
- Service UUID: `0000ffe0-0000-1000-8000-00805f9b34fb` (common MAVLink BLE)
- TX Characteristic: `0000ffe1-0000-1000-8000-00805f9b34fb`
- RX Characteristic: `0000ffe2-0000-1000-8000-00805f9b34fb`

**Data Flow:**
- App → Vehicle: Write to TX characteristic
- Vehicle → App: Subscribe to RX characteristic notifications
- MTU: Typically 20-244 bytes (negotiate larger if supported)
- Packet reassembly may be needed for large MAVLink messages

#### Permission Requirements

**Android (AndroidManifest.xml):**
```xml
<!-- Android 12+ -->
<uses-permission android:name="android.permission.BLUETOOTH_SCAN" />
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />

<!-- Pre-Android 12 -->
<uses-permission android:name="android.permission.BLUETOOTH" />
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" />
```

**iOS (Info.plist):**
```xml
<key>NSBluetoothAlwaysUsageDescription</key>
<string>Required to connect to MAVLink telemetry devices</string>
```

#### Implementation Steps

1. **Phase 1: Foundation**
   - [ ] Add `Plugin.BLE` NuGet package (cross-platform BLE abstraction)
   - [ ] Implement `IBluetoothMavLinkTransport`
   - [ ] Add platform permission handling
   - [ ] Create `BluetoothDiscoveryService`

2. **Phase 2: Connection**
   - [ ] Add `ConnectBluetoothAsync()` to `VehicleConnectionService`
   - [ ] Implement BLE GATT connection and characteristic discovery
   - [ ] Handle BLE disconnect/reconnect logic
   - [ ] Add BLE-specific telemetry stream requests

3. **Phase 3: UI**
   - [ ] Add Bluetooth tab to `ConnectPopupView`
   - [ ] Implement device scanning UI
   - [ ] Add pairing/bonding UI flow
   - [ ] Handle platform permission prompts

4. **Phase 4: Testing**
   - [ ] Test with real BLE MAVLink module
   - [ ] Verify data throughput (typical: 1-10 KB/s)
   - [ ] Test connection stability
   - [ ] Profile battery usage on mobile

#### Known Challenges

- **MTU Limitations:** BLE has smaller MTU than serial; may need packet fragmentation
- **Throughput:** BLE is slower than serial/TCP; high-rate streams may cause lag
- **Platform Differences:** iOS/Android have different BLE stacks and quirks
- **Connection Stability:** BLE can be less stable than wired connections
- **Battery Impact:** BLE scanning and active connections drain mobile battery
- **Permission Complexity:** Android 12+ has complex runtime permission requirements

#### References

- [MAVLink BLE Protocol](https://mavlink.io/en/guide/bluetooth.html)
- [Plugin.BLE](https://github.com/xabre/xamarin-bluetooth-le) - Cross-platform BLE library
- [.NET MAUI Bluetooth Permissions](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/appmodel/permissions)
- ArduPilot BLE telemetry: Common modules use Nordic UART Service (NUS) or custom GATT

---

## Additional Future Enhancements

### Logging & Telemetry
- Add telemetry/logging to track disposal timing
- Log connection cleanup success/failure metrics
- Track time from `OnWindowDestroying` to port release

### Robustness
- Add timeout to `DisposeAsync` to prevent hanging on close (e.g., 5-second max)
- Implement graceful degradation if disposal fails
- Consider adding a "Force Disconnect" mechanism for stuck connections

### User Preferences
- Consider adding a "Disconnect on Exit" setting if immediate disconnect is not always desired
- Add "Auto-reconnect on startup" option for development workflows

### Multi-Connection Support (Long-term)
If multi-vehicle support is added in the future:
- Track multiple active connections in a dictionary
- Dispose all connections in `OnWindowDestroying`
- Update UI to show all active connections
