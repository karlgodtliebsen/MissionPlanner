# Parameter Streaming Timeout Fix

## Problem

When streaming parameters from a vehicle, the operation would slow down after receiving approximately 100 parameters and eventually timeout before completing. This issue occurred because:

1. **Incorrect Event Counting**: The service was counting events instead of tracking unique parameter indices
2. **Duplicate Parameters**: If duplicate PARAM_VALUE messages arrived, they were counted multiple times
3. **Packet Loss**: MAVLink packet loss over serial connections meant some parameters never arrived
4. **No Recovery**: The service had no mechanism to request missing parameters individually

## Root Cause

In `VehicleParameterStreamService.cs`, the original code tracked progress like this:

```csharp
var receivedCount = 0;

subscription = eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(async (@event, ct) =>
{
    receivedCount++;  // ❌ Counts events, not unique parameters
    var isComplete = receivedCount >= totalCount;
});
```

### Issues with Event Counting

1. **Duplicates**: If parameter index 50 arrives twice, it counts as 2 instead of 1
2. **Missing Parameters**: If parameter index 100 never arrives due to packet loss, the service waits forever
3. **No Diagnostics**: When timeout occurs, there's no information about which parameters are missing

## Solution

The fix includes three improvements:

### 1. Track Parameter Indices Instead of Event Count

Changed from counting events to tracking which specific parameter indices have been received:

```csharp
var receivedIndices = new HashSet<ushort>();

subscription = eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(async (@event, ct) =>
{
    receivedIndices.Add(param.Index);  // ✅ Tracks unique indices
    var receivedCount = receivedIndices.Count;
    var isComplete = receivedCount >= totalCount;
});
```

**Benefits:**
- Handles duplicates correctly (HashSet automatically ignores duplicates)
- Accurate progress reporting
- Can identify which parameters are missing

### 2. Add Diagnostic Logging

When timeout occurs, the service now logs which parameter indices are missing:

```csharp
var missingIndices = Enumerable.Range(0, totalCount)
    .Select(i => (ushort)i)
    .Where(i => !receivedIndices.Contains(i))
    .Take(10)
    .ToList();

logger.LogWarning("Missing parameter indices: {MissingIndices}", string.Join(", ", missingIndices));
```

**Example Output:**
```
Parameter stream timeout after 60s. Received 498/500
Missing parameter indices: 127, 243
```

### 3. Smart Retry with Individual Parameter Requests

Added intelligent retry logic that requests missing parameters individually:

```csharp
// After first attempt times out, analyze what's missing
var currentParams = parameterRegistry.GetAllParameters(vehicleId);
var receivedIndices = currentParams.Values.Select(p => p.Index).ToHashSet();

var missingIndices = Enumerable.Range(0, totalCount)
    .Select(i => (ushort)i)
    .Where(i => !receivedIndices.Contains(i))
    .ToList();

// Request each missing parameter individually
foreach (var missingIndex in missingIndices)
{
    await parameterService.RequestParameterByIndexAsync(vehicleId, missingIndex, cancellationToken);
    await Task.Delay(50, cancellationToken);
}
```

**Workflow:**
1. First attempt: Request all parameters with `PARAM_REQUEST_LIST`
2. If timeout occurs with partial data (e.g., 498/500 received)
3. Identify missing indices (e.g., 127, 243)
4. Request each missing parameter with `PARAM_REQUEST_READ` by index
5. Retry the full stream (now with missing parameters filling in)

### 4. New Method: RequestParameterByIndexAsync

Added support for requesting parameters by index (not just by name):

**Interface:**
```csharp
public interface IVehicleParameterService
{
    Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex,
        CancellationToken cancellationToken = default);
}
```

**Implementation:**
```csharp
public async Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex,
    CancellationToken cancellationToken = default)
{
    // Use empty string for paramId and provide the index
    var packet = encoder.EncodeParamRequestRead(vehicleId.SystemId, vehicleId.ComponentId,
        string.Empty, (short)parameterIndex);

    await client.SendAsync(packet, endpoint, cancellationToken);
    return true;
}
```

## Results

### Before Fix
- ❌ Progress slows down after ~100 parameters
- ❌ Eventually times out (60 seconds)
- ❌ No visibility into which parameters are missing
- ❌ No recovery mechanism for packet loss

### After Fix
- ✅ Accurate progress tracking (handles duplicates correctly)
- ✅ Completes successfully even with packet loss
- ✅ Diagnostic logging shows missing parameter indices
- ✅ Automatic recovery by requesting missing parameters individually
- ✅ Retry logic fills in gaps intelligently

## Testing

Run the test to verify the fix:

```bash
dotnet test --filter "Should_Retrieve_Parameters_Using_Streaming"
```

Expected behavior:
1. First attempt may timeout if there's significant packet loss
2. Log shows: "Found X missing parameters out of Y. Requesting individually..."
3. Second attempt succeeds after filling gaps
4. All parameters received successfully

## Example Log Output

```
[Info] Starting parameter stream for vehicle (1,1) with timeout 60s
[Debug] Received parameter 1/500: SYSID_THISMAV = 1
[Debug] Received parameter 2/500: SYSID_MYGCS = 255
...
[Debug] Received parameter 498/500: RCMAP_ROLL = 1
[Warn] Parameter stream timeout after 60.1s. Received 498/500
[Warn] Missing parameter indices: 127, 243
[Info] Retry attempt 1/3 for vehicle (1,1)
[Warn] Parameter stream failed on attempt 1: Timeout after 60s. Analyzing gaps...
[Info] Found 2 missing parameters out of 500. Requesting individually...
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=127
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=243
[Info] Retry attempt 2/3 for vehicle (1,1)
[Debug] Received parameter 127/500: Q_A_RAT_PIT_FLTD = 20
[Debug] Received parameter 243/500: FENCE_MARGIN = 2
[Info] Successfully received 500 parameters in 63.2s
```

## Files Modified

- `src/Core/MissionPlanner.Core/Services/VehicleParameterStreamService.cs`
  - Changed event counting to index tracking (HashSet<ushort>)
  - Added diagnostic logging for missing parameters
  - Enhanced retry logic to request missing parameters individually

- `src/Core/MissionPlanner.Core/Services/Abstractions/IVehicleParameterService.cs`
  - Added `RequestParameterByIndexAsync()` method

- `src/Core/MissionPlanner.Core/Services/VehicleParameterService.cs`
  - Implemented `RequestParameterByIndexAsync()` method

## MAVLink Protocol Details

### PARAM_REQUEST_LIST (Message ID 21)
- Requests all parameters from vehicle
- Vehicle responds with multiple PARAM_VALUE messages (one per parameter)
- Fast but prone to packet loss on lossy connections

### PARAM_REQUEST_READ (Message ID 20)
- Requests a single parameter by name or index
- Vehicle responds with one PARAM_VALUE message
- More reliable for filling in missing parameters

### PARAM_VALUE (Message ID 22)
- Contains: param_name, param_value, param_type, param_index, param_count
- `param_index`: 0-based index of this parameter
- `param_count`: Total number of parameters on vehicle

## Best Practices

1. **Always use retry logic** for production code:
   ```csharp
   var result = await streamService.StreamAllParametersWithRetryAsync(vehicleId,
       progress: progress, maxRetries: 3);
   ```

2. **Handle partial success** gracefully:
   ```csharp
   if (!result.Success)
   {
       logger.LogWarning("Received {Count}/{Total} parameters before timeout",
           result.Parameters.Count, result.TotalCount);
   }
   ```

3. **Monitor logs** during development to identify connection quality issues

4. **Increase timeout** on slow or lossy connections:
   ```csharp
   var result = await streamService.StreamAllParametersWithRetryAsync(vehicleId,
       timeout: TimeSpan.FromSeconds(120));
   ```

## See Also

- [PARAMETER_STREAMING_GUIDE.md](PARAMETER_STREAMING_GUIDE.md) - Complete usage guide
- [PARAMETER_USAGE.md](PARAMETER_USAGE.md) - Parameter service basics
- [VEHICLE_CONNECTION_BEST_PRACTICES.md](VEHICLE_CONNECTION_BEST_PRACTICES.md) - Connection patterns
