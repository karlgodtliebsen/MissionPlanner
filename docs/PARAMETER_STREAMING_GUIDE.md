# Parameter Streaming Guide

This guide shows you how to retrieve **all** parameters from a vehicle in a streaming fashion with progress tracking.

## The Problem

When you call `RequestParameterListAsync()`, the vehicle responds with **hundreds of individual PARAM_VALUE messages** (one per parameter) that arrive asynchronously over several seconds. You were only seeing 1 parameter because you weren't waiting for all of them to arrive.

## The Solution: Parameter Streaming Service

The `IVehicleParameterStreamService` handles the complete streaming process:
- ✅ Sends the request
- ✅ Waits for all parameters to arrive
- ✅ Reports progress (percentage complete)
- ✅ Handles timeouts and errors
- ✅ Supports retry logic

## Basic Usage

```csharp
public class ParameterService
{
    private readonly IVehicleParameterStreamService _streamService;

    public async Task GetAllParametersAsync(VehicleId vehicleId)
    {
        // Simple: Get all parameters with default 60-second timeout
        var result = await _streamService.StreamAllParametersAsync(vehicleId);

        if (result.Success)
        {
            Console.WriteLine($"Received {result.TotalCount} parameters in {result.Duration.TotalSeconds:F1}s");

            foreach (var (name, param) in result.Parameters)
            {
                Console.WriteLine($"  {name} = {param.Value}");
            }
        }
        else
        {
            Console.WriteLine($"Failed: {result.ErrorMessage}");
        }
    }
}
```

## With Progress Reporting

```csharp
public async Task GetParametersWithProgressAsync(VehicleId vehicleId)
{
    var progress = new Progress<ParameterStreamProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.ReceivedCount}/{p.TotalCount} ({p.PercentComplete}%)");

        if (p.IsComplete)
        {
            Console.WriteLine("✓ All parameters received!");
        }
    });

    var result = await _streamService.StreamAllParametersAsync(
        vehicleId,
        progress: progress,
        timeout: TimeSpan.FromSeconds(60));

    if (result.Success)
    {
        Console.WriteLine($"Complete! Received {result.TotalCount} parameters");
    }
}

// Output:
// Progress: 0/0 (0%)
// Progress: 50/500 (10%)
// Progress: 100/500 (20%)
// ...
// Progress: 500/500 (100%)
// ✓ All parameters received!
// Complete! Received 500 parameters
```

## With Real-Time Callback

```csharp
public async Task ProcessParametersAsTheyArriveAsync(VehicleId vehicleId)
{
    var parameters = new List<VehicleParameter>();

    var result = await _streamService.StreamAllParametersAsync(
        vehicleId,
        onParameterReceived: param =>
        {
            // This callback is invoked for EACH parameter as it arrives
            Console.WriteLine($"Received: {param.Name} = {param.Value}");
            parameters.Add(param);

            // You can update UI, validate, or process each parameter immediately
            UpdateUIWithParameter(param);
        });

    if (result.Success)
    {
        Console.WriteLine($"Processed {parameters.Count} parameters in real-time");
    }
}
```

## With Retry Logic (Recommended)

```csharp
public async Task GetParametersWithRetryAsync(VehicleId vehicleId)
{
    var progress = new Progress<ParameterStreamProgress>(p =>
    {
        Console.WriteLine($"{p.PercentComplete}%");
    });

    // Automatically retries up to 3 times if parameters are incomplete or missing
    var result = await _streamService.StreamAllParametersWithRetryAsync(
        vehicleId,
        progress: progress,
        maxRetries: 3,
        timeout: TimeSpan.FromSeconds(60));

    if (result.Success)
    {
        Console.WriteLine($"Success! {result.TotalCount} parameters in {result.Duration.TotalSeconds:F1}s");
    }
    else
    {
        Console.WriteLine($"Failed after retries: {result.ErrorMessage}");
    }
}
```

## Complete Example for Your Test

```csharp
[Fact]
public async Task Should_Retrieve_All_Parameters_From_Vehicle()
{
    // Arrange
    var connectionString = "serial:COM10:115200";
    var ct = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;

    // Connect and wait for heartbeat
    await _connectionService.ConnectAsync(connectionString, ct);
    var vehicleId = await WaitForHeartbeatAsync(_vehicleRegistry, TimeSpan.FromSeconds(5));

    // IMPORTANT: Wait for vehicle to be ready
    await Task.Delay(1500);

    // Act - Stream all parameters with progress
    var progressReports = new List<ParameterStreamProgress>();
    var progress = new Progress<ParameterStreamProgress>(p => progressReports.Add(p));

    var result = await _streamService.StreamAllParametersWithRetryAsync(
        vehicleId,
        progress: progress,
        maxRetries: 3,
        timeout: TimeSpan.FromSeconds(60),
        ct);

    // Assert
    Assert.True(result.Success, $"Failed to get parameters: {result.ErrorMessage}");
    Assert.NotEmpty(result.Parameters);
    Assert.True(result.TotalCount > 0);
    Assert.Equal(result.TotalCount, result.Parameters.Count);

    _testOutputHelper.WriteLine($"✓ Received {result.TotalCount} parameters in {result.Duration.TotalSeconds:F1}s");

    // Verify progress reporting
    Assert.NotEmpty(progressReports);
    var finalProgress = progressReports.Last();
    Assert.True(finalProgress.IsComplete);
    Assert.Equal(100, finalProgress.PercentComplete);

    // Display first 10 parameters
    foreach (var (name, param) in result.Parameters.Take(10))
    {
        _testOutputHelper.WriteLine($"  {name} = {param.Value} ({param.Type})");
    }
}
```

## UI Integration Example (WPF/MAUI)

```csharp
public class ParameterViewModel : ObservableObject
{
    private readonly IVehicleParameterStreamService _streamService;
    private int _progressPercent;
    private string _status = "Ready";

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ObservableCollection<ParameterItem> Parameters { get; } = new();

    public async Task LoadParametersAsync(VehicleId vehicleId)
    {
        try
        {
            Status = "Requesting parameters...";
            Parameters.Clear();

            var progress = new Progress<ParameterStreamProgress>(p =>
            {
                ProgressPercent = p.PercentComplete;
                Status = $"Loading: {p.ReceivedCount}/{p.TotalCount}";
            });

            var result = await _streamService.StreamAllParametersAsync(
                vehicleId,
                progress: progress,
                onParameterReceived: param =>
                {
                    // Update UI in real-time as parameters arrive
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Parameters.Add(new ParameterItem(param));
                    });
                },
                timeout: TimeSpan.FromSeconds(60));

            if (result.Success)
            {
                Status = $"Loaded {result.TotalCount} parameters";
                ProgressPercent = 100;
            }
            else
            {
                Status = $"Error: {result.ErrorMessage}";
                ShowErrorDialog(result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            ShowErrorDialog(ex.Message);
        }
    }
}
```

## Advanced: Custom Timeout and Cancellation

```csharp
public async Task GetParametersWithCustomTimeoutAsync(VehicleId vehicleId)
{
    using var cts = new CancellationTokenSource();

    // Cancel after user clicks button
    CancelButton.Click += (s, e) => cts.Cancel();

    try
    {
        var result = await _streamService.StreamAllParametersAsync(
            vehicleId,
            timeout: TimeSpan.FromSeconds(120), // 2 minutes
            cancellationToken: cts.Token);

        if (result.Success)
        {
            ProcessParameters(result.Parameters);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("User cancelled parameter download");
    }
}
```

## Error Handling

```csharp
public async Task<IReadOnlyDictionary<string, VehicleParameter>?> SafeGetParametersAsync(
    VehicleId vehicleId)
{
    try
    {
        var result = await _streamService.StreamAllParametersWithRetryAsync(
            vehicleId,
            maxRetries: 3);

        if (result.Success)
        {
            return result.Parameters;
        }
        else
        {
            _logger.LogError("Parameter stream failed: {Error}", result.ErrorMessage);

            // Check specific errors
            if (result.ErrorMessage?.Contains("Timeout") == true)
            {
                ShowWarning("Vehicle is not responding. Check connection.");
            }
            else if (result.ErrorMessage?.Contains("cancelled") == true)
            {
                ShowInfo("Parameter download was cancelled.");
            }

            return null;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during parameter stream");
        ShowError($"Unexpected error: {ex.Message}");
        return null;
    }
}
```

## Performance Characteristics

- **Typical time**: 5-15 seconds for 500 parameters
- **Network overhead**: ~25 KB for 500 parameters
- **Memory**: ~100 KB for 500 parameters in memory
- **Progress updates**: Every 100ms
- **Timeout default**: 60 seconds (configurable)

## Common Issues and Solutions

### Issue: Timeout After 60 Seconds

**Cause**: Vehicle is slow to respond or connection is lossy

**Solution**: Increase timeout or use retry logic
```csharp
var result = await _streamService.StreamAllParametersWithRetryAsync(
    vehicleId,
    maxRetries: 5,
    timeout: TimeSpan.FromSeconds(120)); // 2 minutes
```

### Issue: Missing Parameters (e.g., 498/500)

**Cause**: MAVLink packet loss

**Solution**: Use retry logic (automatically re-requests)
```csharp
var result = await _streamService.StreamAllParametersWithRetryAsync(vehicleId);
// Automatically retries if incomplete
```

### Issue: Only Getting 1 Parameter

**Cause**: Not waiting for streaming to complete

**Solution**: Use `StreamAllParametersAsync` instead of `RequestParameterListAsync`
```csharp
// ❌ Wrong - only sends request
await parameterService.RequestParameterListAsync(vehicleId);

// ✅ Correct - waits for all parameters
var result = await streamService.StreamAllParametersAsync(vehicleId);
```

## Best Practices

1. **Always use retry logic** for production code
   ```csharp
   await _streamService.StreamAllParametersWithRetryAsync(vehicleId);
   ```

2. **Show progress** to users (parameters take 5-15 seconds)
   ```csharp
   var progress = new Progress<ParameterStreamProgress>(...);
   ```

3. **Handle timeout gracefully**
   ```csharp
   if (!result.Success && result.ErrorMessage.Contains("Timeout"))
   {
       // Inform user, offer retry
   }
   ```

4. **Clear old parameters** before requesting new ones
   ```csharp
   _parameterRegistry.ClearParameters(vehicleId); // Done automatically by service
   ```

5. **Wait for vehicle readiness** after heartbeat
   ```csharp
   await WaitForHeartbeatAsync();
   await Task.Delay(1500); // Important!
   await streamService.StreamAllParametersAsync(vehicleId);
   ```

## API Reference

### `StreamAllParametersAsync`
```csharp
Task<ParameterStreamResult> StreamAllParametersAsync(
    VehicleId vehicleId,
    IProgress<ParameterStreamProgress>? progress = null,
    Action<VehicleParameter>? onParameterReceived = null,
    TimeSpan? timeout = null, // default: 60s
    CancellationToken cancellationToken = default)
```

### `StreamAllParametersWithRetryAsync`
```csharp
Task<ParameterStreamResult> StreamAllParametersWithRetryAsync(
    VehicleId vehicleId,
    IProgress<ParameterStreamProgress>? progress = null,
    int maxRetries = 3,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default)
```

### `ParameterStreamProgress`
```csharp
record ParameterStreamProgress(
    int ReceivedCount,
    int TotalCount,
    int PercentComplete,  // 0-100
    bool IsComplete)
```

### `ParameterStreamResult`
```csharp
class ParameterStreamResult
{
    bool Success { get; }
    IReadOnlyDictionary<string, VehicleParameter> Parameters { get; }
    int TotalCount { get; }
    string? ErrorMessage { get; }
    TimeSpan Duration { get; }
}
```

## Summary

**Before** (only 1 parameter):
```csharp
await parameterService.RequestParameterListAsync(vehicleId);
// ❌ Returns immediately, parameters arrive later asynchronously
```

**After** (all parameters):
```csharp
var result = await streamService.StreamAllParametersAsync(vehicleId);
// ✅ Waits for all parameters, returns complete set
```

You now have a robust parameter streaming system with progress tracking, retry logic, and error handling! 🚁
