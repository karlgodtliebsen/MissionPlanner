# Vehicle Connection and Testing Best Practices

This guide covers best practices for establishing reliable connections with ArduPilot vehicles and writing robust tests.

## Issue 1: Serial Port Access Denied Between Tests

### Problem
```
System.UnauthorizedAccessException: Access to the path 'COM10' is denied.
```

### Cause
Windows doesn't immediately release serial ports after closing. The next test tries to open the port before the OS fully releases it.

### Solution (Already Implemented)
The `SerialMavLinkTransport` now:
1. Discards input/output buffers before closing
2. Waits 100ms after closing to let the OS release the port
3. Uses retry logic with exponential backoff

### Best Practices for Tests

```csharp
[Fact]
public async Task Test1()
{
    // Your test code

    // Ensure proper cleanup
    await vehicleConnectionService.DisconnectAsync(vehicleId);

    // Give extra time between tests if needed
    await Task.Delay(200);
}

// Alternative: Use IAsyncLifetime for test setup/teardown
public class MyTests : IAsyncLifetime
{
    private IVehicleConnectionService _connectionService;

    public async Task InitializeAsync()
    {
        // Setup
    }

    public async Task DisposeAsync()
    {
        // Cleanup - automatically called after each test
        if (_connectionService != null)
        {
            await _connectionService.DisconnectAllAsync();
            await Task.Delay(200); // Extra safety margin
        }
    }
}
```

## Issue 2: Receiving Messages Out of Order (Attitude Before Heartbeat)

### Problem
Sometimes you receive `AttitudeMessage` but no `HeartbeatMessage`, or they arrive out of order.

### Cause
This is **normal MAVLink behavior**:
- The vehicle continuously broadcasts telemetry (attitude, position, etc.) at high rates
- Heartbeat is sent at a lower rate (typically 1 Hz)
- Depending on when you connect, you might receive other messages first

### Understanding Message Rates

Typical ArduPilot message rates:
- **HEARTBEAT**: 1 Hz (every 1 second)
- **ATTITUDE**: 10-50 Hz (10-50 times per second)
- **GLOBAL_POSITION_INT**: 5-10 Hz
- **SYS_STATUS**: 1-2 Hz
- **PARAM_VALUE**: On-demand (only when requested)

### Best Practice

**Don't assume heartbeat arrives first.** Instead, wait for heartbeat with a timeout:

```csharp
public async Task<VehicleId?> WaitForHeartbeatAsync(
    IVehicleRegistry vehicleRegistry,
    TimeSpan timeout)
{
    var cts = new CancellationTokenSource(timeout);

    while (!cts.Token.IsCancellationRequested)
    {
        var vehicles = vehicleRegistry.GetAll();
        if (vehicles.Any())
        {
            return vehicles.First().Id;
        }

        await Task.Delay(100, cts.Token);
    }

    throw new TimeoutException("No heartbeat received within timeout period");
}

// Usage in test
await connectionService.ConnectAsync(connectionString, ct);

// Wait up to 5 seconds for heartbeat
var vehicleId = await WaitForHeartbeatAsync(vehicleRegistry, TimeSpan.FromSeconds(5));
```

## Issue 3: Parameters Not Arriving After Connection

### Problem
After heartbeat and vehicle registration, parameter requests don't always return parameters.

### Causes

1. **Request sent too early**: The vehicle might not be ready to respond to parameter requests immediately after connection
2. **No retry mechanism**: If the first request is lost or ignored, it's never retried
3. **Buffer not cleared**: Old data in buffers from previous connection
4. **Vehicle state**: Some ArduPilot modes might not respond to parameter requests

### Solutions

#### Solution 1: Wait After Heartbeat Before Requesting Parameters

```csharp
// Connect and wait for heartbeat
await connectionService.ConnectAsync(connectionString, ct);
var vehicleId = await WaitForHeartbeatAsync(vehicleRegistry, TimeSpan.FromSeconds(5));

// IMPORTANT: Wait for vehicle to fully initialize
// ArduPilot needs time after heartbeat to be ready for parameter requests
await Task.Delay(1000); // 1 second delay

// Now request parameters
await parameterService.RequestParameterListAsync(vehicleId, ct);
```

#### Solution 2: Wait for Multiple Heartbeats

```csharp
public async Task<VehicleId> WaitForStableConnection(
    IVehicleRegistry vehicleRegistry,
    int requiredHeartbeats = 3,
    TimeSpan timeout = default)
{
    timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;
    var cts = new CancellationTokenSource(timeout);

    VehicleId? vehicleId = null;
    int heartbeatCount = 0;
    DateTimeOffset? lastHeartbeat = null;

    while (heartbeatCount < requiredHeartbeats && !cts.Token.IsCancellationRequested)
    {
        var vehicles = vehicleRegistry.GetAll();
        if (vehicles.Any())
        {
            var vehicle = vehicles.First();
            vehicleId = vehicle.Id;

            // Check if we received a new heartbeat
            if (lastHeartbeat == null || vehicle.State.LastHeartbeatAt > lastHeartbeat)
            {
                heartbeatCount++;
                lastHeartbeat = vehicle.State.LastHeartbeatAt;
            }
        }

        await Task.Delay(100, cts.Token);
    }

    if (vehicleId == null)
    {
        throw new TimeoutException("No heartbeat received");
    }

    return vehicleId.Value;
}

// Usage
var vehicleId = await WaitForStableConnection(vehicleRegistry, requiredHeartbeats: 3);
// Now the vehicle is stable and ready for parameter requests
await parameterService.RequestParameterListAsync(vehicleId, ct);
```

#### Solution 3: Implement Parameter Request with Retry and Timeout

```csharp
public async Task<bool> RequestParametersWithRetry(
    IVehicleParameterService parameterService,
    IVehicleParameterRegistry parameterRegistry,
    IDomainEventHub eventHub,
    VehicleId vehicleId,
    TimeSpan timeout,
    CancellationToken ct)
{
    var receivedCount = 0;
    var totalCount = 0;
    var completed = false;

    // Subscribe to parameter events
    var subscription = await eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(
        async (@event, cancellationToken) =>
        {
            if (@event.VehicleId == vehicleId)
            {
                receivedCount++;
                totalCount = @event.Parameter.Count;

                // Check if we've received all parameters
                if (receivedCount >= totalCount && totalCount > 0)
                {
                    completed = true;
                }
            }
        });

    try
    {
        var cts = new CancellationTokenSource(timeout);
        var retryDelay = TimeSpan.FromSeconds(2);
        var lastRequestTime = DateTimeOffset.MinValue;

        while (!completed && !cts.Token.IsCancellationRequested)
        {
            // Send request every 2 seconds if we haven't received all parameters
            if (DateTimeOffset.UtcNow - lastRequestTime > retryDelay)
            {
                await parameterService.RequestParameterListAsync(vehicleId, ct);
                lastRequestTime = DateTimeOffset.UtcNow;
            }

            await Task.Delay(100, cts.Token);
        }

        return completed;
    }
    finally
    {
        // Cleanup subscription
        subscription?.Dispose();
    }
}

// Usage
var success = await RequestParametersWithRetry(
    parameterService,
    parameterRegistry,
    eventHub,
    vehicleId,
    timeout: TimeSpan.FromSeconds(30),
    ct);

if (success)
{
    var allParams = parameterRegistry.GetAllParameters(vehicleId);
    Console.WriteLine($"Received {allParams.Count} parameters");
}
```

## Recommended Test Pattern

Here's a complete, robust test pattern:

```csharp
public class VehicleParameterTests : IAsyncLifetime
{
    private readonly IVehicleConnectionService _connectionService;
    private readonly IVehicleParameterService _parameterService;
    private readonly IVehicleParameterRegistry _parameterRegistry;
    private readonly IVehicleRegistry _vehicleRegistry;
    private readonly IDomainEventHub _eventHub;

    private VehicleId? _connectedVehicleId;

    public VehicleParameterTests(/* inject services */)
    {
        // Constructor injection
    }

    public async Task InitializeAsync()
    {
        // Setup - runs before each test
    }

    public async Task DisposeAsync()
    {
        // Cleanup - runs after each test
        if (_connectedVehicleId.HasValue)
        {
            await _connectionService.DisconnectAsync(_connectedVehicleId.Value);
        }

        // Extra delay to ensure port is released
        await Task.Delay(200);
    }

    [Fact]
    public async Task Should_Connect_And_Retrieve_Parameters()
    {
        // Arrange
        var connectionString = "serial:COM10:115200";
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;

        // Act - Connect
        await _connectionService.ConnectAsync(connectionString, ct);

        // Wait for stable connection (3 heartbeats)
        _connectedVehicleId = await WaitForStableConnection(
            _vehicleRegistry,
            requiredHeartbeats: 3,
            timeout: TimeSpan.FromSeconds(10));

        // Additional delay for vehicle readiness
        await Task.Delay(1000);

        // Request parameters with retry
        var success = await RequestParametersWithRetry(
            _parameterService,
            _parameterRegistry,
            _eventHub,
            _connectedVehicleId.Value,
            timeout: TimeSpan.FromSeconds(30),
            ct);

        // Assert
        Assert.True(success, "Failed to receive all parameters");

        var allParams = _parameterRegistry.GetAllParameters(_connectedVehicleId.Value);
        Assert.NotEmpty(allParams);

        _testOutputHelper.WriteLine($"Received {allParams.Count} parameters");
        foreach (var param in allParams.Take(10))
        {
            _testOutputHelper.WriteLine($"  {param.Key} = {param.Value.Value}");
        }
    }

    // Helper methods
    private async Task<VehicleId> WaitForStableConnection(...)
    {
        // Implementation from Solution 2 above
    }

    private async Task<bool> RequestParametersWithRetry(...)
    {
        // Implementation from Solution 3 above
    }
}
```

## Additional Best Practices

### 1. Use Proper Logging

```csharp
_logger.LogInformation("Connecting to {ConnectionString}", connectionString);
_logger.LogInformation("Waiting for heartbeat...");
_logger.LogInformation("Received heartbeat from {VehicleId}", vehicleId);
_logger.LogInformation("Requesting parameters...");
_logger.LogInformation("Received {Count} parameters", receivedCount);
```

### 2. Always Use Timeouts

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await SomeOperationAsync(cts.Token);
```

### 3. Clean Up Resources

```csharp
// Unsubscribe from events
subscription?.Dispose();

// Disconnect
await connectionService.DisconnectAsync(vehicleId);

// Clear registries if needed
parameterRegistry.ClearParameters(vehicleId);
```

### 4. Handle Connection Failures Gracefully

```csharp
try
{
    await connectionService.ConnectAsync(connectionString, ct);
}
catch (UnauthorizedAccessException)
{
    _logger.LogWarning("Port is busy. Waiting and retrying...");
    await Task.Delay(500);
    await connectionService.ConnectAsync(connectionString, ct);
}
```

## Summary

1. **Serial Port Issues**: Fixed with buffer flushing and 100ms delay after close
2. **Message Order**: Don't assume heartbeat comes first - wait for it explicitly
3. **Parameter Requests**: Wait 1-2 seconds after heartbeat, use retry logic, wait for all parameters before proceeding
4. **Testing**: Use proper cleanup, timeouts, and helper methods for stable connections

Following these patterns will give you reliable, repeatable tests with your ArduPilot vehicle.
