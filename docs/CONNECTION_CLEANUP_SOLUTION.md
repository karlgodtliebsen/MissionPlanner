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
- Storage of `IVehicleConnectionService` reference in `CreateWindow`
- `Window.Destroying` event handler to call `DisposeAsync()` on the connection service
- Exception handling to prevent crashes during cleanup

```csharp
protected override Window CreateWindow(IActivationState? activationState)
{
	var topbarView = activationState?.Context.Services.GetRequiredService<TopBarView>()!;

	// Store connection service reference for cleanup
	connectionService = activationState?.Context.Services.GetRequiredService<IVehicleConnectionService>();

	var window = new Window(new AppShell(topbarView));

	// Handle window destruction to ensure connection cleanup
	window.Destroying += OnWindowDestroying;

	return window;
}

private async void OnWindowDestroying(object? sender, EventArgs e)
{
	if (connectionService != null)
	{
		try
		{
			await connectionService.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error disposing connection service: {ex.Message}");
		}
	}
}
```

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
   - `OnWindowDestroying` handler calls `connectionService.DisposeAsync()`
   - `DisposeAsync` calls `DisconnectInternalAsync()` to close the connection
   - Serial port/TCP/UDP resources are released
4. **Port is now free:** Subsequent app launches can connect to the same port

## Testing Checklist

- [x] Build successful
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
   - Added `IVehicleConnectionService` field
   - Added `OnWindowDestroying` event handler
   - Injected using statement for `MissionPlanner.Core.Services.Abstractions`

## Related Context

- `IVehicleConnectionService` already extends `IAsyncDisposable` (implemented earlier)
- `ConnectPopupViewModel` already implements `IAsyncDisposable` and calls `connectionService.DisposeAsync()`
- The connection service uses `SemaphoreSlim` to ensure thread-safe disposal
- `DisconnectInternalAsync` properly closes MAVLink clients and cancels background tasks

## Future Enhancements (Optional)

- Add telemetry/logging to track disposal timing
- Add timeout to `DisposeAsync` to prevent hanging on close
- Consider adding a "Disconnect on Exit" setting if immediate disconnect is not always desired
