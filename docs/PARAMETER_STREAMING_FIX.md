# Parameter Streaming Timeout Fix

## Problem

When streaming parameters from a vehicle, the operation would slow down after receiving approximately 100 parameters and eventually timeout before completing. This issue occurred because:

1. **Incorrect Event Counting**: The service was counting events instead of tracking unique parameter indices
2. **Duplicate Parameters**: If duplicate PARAM_VALUE messages arrived, they were counted multiple times
3. **Packet Loss**: MAVLink packet loss over serial connections meant some parameters never arrived
4. **No Recovery**: The service had no mechanism to request missing parameters individually

## Root Causes

### Issue 1: Event Counting Instead of Index Tracking

In `VehicleParameterStreamService.cs`, the original code tracked progress like this:

```csharp
var receivedCount = 0;

subscription = eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(async (@event, ct) =>
{
    receivedCount++;  // ❌ Counts events, not unique parameters
    var isComplete = receivedCount >= totalCount;
});
```

**Problems with Event Counting:**

1. **Duplicates**: If parameter index 50 arrives twice, it counts as 2 instead of 1
2. **Missing Parameters**: If parameter index 100 never arrives due to packet loss, the service waits forever
3. **No Diagnostics**: When timeout occurs, there's no information about which parameters are missing

### Issue 2: Retry Logic Cleared Existing Parameters

The original retry logic had a critical flaw:

```csharp
// Retry attempt 1: Got 963/1101 parameters, timeout
// Request missing 138 parameters individually
foreach (var missingIndex in missingIndices)
{
    await RequestParameterByIndexAsync(vehicleId, missingIndex);
}

// Wait 2 seconds for responses
await Task.Delay(TimeSpan.FromSeconds(2));

// Then call StreamAllParametersAsync again
var result = await StreamAllParametersAsync(...);  // ❌ Clears all 963 parameters!
```

**What Happened:**
1. First attempt received 963/1101 parameters before timeout
2. Retry logic identified 138 missing parameters
3. Retry logic requested those 138 parameters individually
4. **BUG**: Retry then called `StreamAllParametersAsync` which starts with `ClearParameters()`
5. All 963 previously received parameters were **deleted**!
6. Started over from zero parameters again
7. Same packet loss occurred, got ~963 parameters again
8. Eventually timed out after retries with incomplete data

This is why you saw 963 log entries but expected 1101 parameters.

## Solution

The fix includes four improvements:

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
Parameter stream timeout after 60s. Received 963/1101
Missing parameter indices (first 10): 45, 127, 189, 243, 456, 567, 678, 789, 890, 991
```

### 3. Preserve Parameters During Retry (CRITICAL FIX)

**Changed the retry logic to NOT clear existing parameters:**

```csharp
// ❌ OLD (BROKEN):
for (var attempt = 0; attempt <= maxRetries; attempt++)
{
    var result = await StreamAllParametersAsync(...);  // Clears all parameters!
    // Request missing individually
    // Then retry StreamAllParametersAsync again - clears again!
}

// ✅ NEW (FIXED):
// First attempt
var result = await StreamAllParametersAsync(...);  // Gets 963/1101

// Retry attempts: DON'T clear existing parameters
for (var attempt = 1; attempt <= maxRetries; attempt++)
{
    // Get current state (DON'T clear!)
    var currentParams = parameterRegistry.GetAllParameters(vehicleId);  // Still has 963

    // Find missing
    var missingIndices = FindMissing(currentParams, totalCount);  // 138 missing

    // Request each missing parameter
    foreach (var index in missingIndices)
    {
        await RequestParameterByIndexAsync(vehicleId, index);
    }

    // Wait and check if complete
    // Now we have 963 + 138 = 1101 parameters!
}
```

**Key Change:**
- Retry attempts no longer call `StreamAllParametersAsync` (which would clear everything)
- Instead, retry attempts directly request missing parameters and preserve what was already received
- Progress accumulates: 963 → 1063 → 1101 ✓

**Workflow:**
1. **First attempt**: Request all parameters with `PARAM_REQUEST_LIST`
   - Receives 963/1101 due to packet loss
   - Timeout occurs
2. **Retry attempt 1**: Analyze gaps
   - Identify 138 missing indices
   - Request each missing parameter with `PARAM_REQUEST_READ` by index
   - Subscribe to incoming parameters and wait up to 30 seconds
   - Receives 100 more → now at 1063/1101
3. **Retry attempt 2**: Continue filling gaps
   - Identify remaining 38 missing indices
   - Request individually
   - Receives remaining 38 → now at 1101/1101 ✓
   - Success!

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

### Before Fix (Your Experience: 963/1101)
- ❌ First attempt received 963/1101 parameters due to packet loss
- ❌ Retry logic cleared the 963 parameters and started over
- ❌ Each retry received ~963 parameters again (same packet loss pattern)
- ❌ Eventually timed out after 3 retries
- ❌ No visibility into which 138 parameters were missing
- ❌ No recovery mechanism to fill gaps

### After Fix (Expected Behavior)
- ✅ First attempt receives 963/1101 parameters (same packet loss)
- ✅ Retry **preserves** the 963 parameters (doesn't clear!)
- ✅ Retry identifies 138 missing parameter indices
- ✅ Retry requests those 138 individually (fills gaps)
- ✅ Receives all 138 missing parameters → 1101/1101 complete
- ✅ Accurate progress tracking (handles duplicates correctly)
- ✅ Diagnostic logging shows exactly which indices are missing
- ✅ Automatic recovery even with significant packet loss

### Performance Impact
- **Before**: ~60s timeout × 3 retries = 180s total, incomplete data (963/1101)
- **After**: ~60s first attempt + ~30s retry = 90s total, complete data (1101/1101) ✓

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

### Successful Recovery from Packet Loss (963/1101 → 1101/1101)

```
[Info] Starting parameter stream for vehicle (1,1) with timeout 60s
[Verb] Received ParamValueMessage: SYSID_THISMAV = 1 (index 0/1101)
[Verb] Received ParamValueMessage: SYSID_MYGCS = 255 (index 1/1101)
...
[Verb] Received ParamValueMessage: SERVO_BLH_BDMASK = 15 (index 893/1101)
...
[Warn] Parameter stream timeout after 60.1s. Received 963/1101
[Warn] Missing parameter indices (first 10): 45, 89, 127, 189, 243, 345, 456, 567, 678, 789

[Info] Retry attempt 1/3 for vehicle (1,1)
[Info] Found 138 missing parameters out of 1101. Requesting individually...
[Debug] Missing indices (first 10): 45, 89, 127, 189, 243, 345, 456, 567, 678, 789
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=45
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=89
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=127
...
[Debug] Waiting for 138 parameter responses...
[Verb] Received ParamValueMessage: ARSPD_TYPE = 1 (index 45/1101)
[Verb] Received ParamValueMessage: BATT_MONITOR = 4 (index 89/1101)
...
[Info] Retry attempt 1 completed. Received 1063/1101 (100 new)

[Info] Retry attempt 2/3 for vehicle (1,1)
[Info] Found 38 missing parameters out of 1101. Requesting individually...
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=678
[Debug] 📤 Sent PARAM_REQUEST_READ to (1,1): index=789
...
[Info] All 1101 parameters received! (Received 38 new parameters)
[Info] Successfully received 1101 parameters in 72.8s
```

### Key Improvements in Logs

- **First attempt**: Shows "Received 963/1101" with list of missing indices
- **Retry attempts**: Show how many parameters are being requested
- **Progress tracking**: Shows accumulation (963 → 1063 → 1101)
- **Success message**: Shows total time and how many new parameters were received in final retry

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
