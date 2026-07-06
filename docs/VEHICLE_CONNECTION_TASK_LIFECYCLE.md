# VehicleConnectionService Background Task Lifecycle Management

## Problem
The `VehicleConnectionService.ConnectSerialAsync()` method was creating background tasks for `messagePump` and `connection` that would be disposed when the method exited, causing the connection to close prematurely.

## Solution
Implemented proper lifecycle management for background tasks by storing them as instance fields and cleaning them up during disposal.

## Changes Made

### 1. Added Instance Fields (Lines ~30-33)
```csharp
// Background tasks that must live as long as this service instance
private Task? messagePumpTask;
private Task? connectionTask;
private readonly CancellationTokenSource serviceCts = new();
```

**Purpose**:
- `messagePumpTask` and `connectionTask` store the running background tasks
- `serviceCts` provides a service-level cancellation token that lives for the entire service lifetime
- These ensure tasks persist beyond method scope

### 2. Updated ConnectSerialAsync() (~Lines 75-85)
```csharp
messagePump = serviceFactory.Create<IVehicleMessagePump>();
connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

// Store background tasks so they live as long as this service instance
// Link to service-level cancellation token to allow proper cleanup
var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

// Don't await here - these tasks should run in the background for the lifetime of the connection
// They will be cleaned up in DisposeAsync
```

**Key Points**:
- Tasks are stored in instance fields (`messagePumpTask`, `connectionTask`)
- Uses `CreateLinkedTokenSource` to combine method-level and service-level cancellation
- Tasks run in background via `Task.Run()` and are NOT awaited
- Tasks continue running after method returns

### 3. Enhanced DisconnectAsync() (~Lines 217-280)
```csharp
// Stop background tasks gracefully
if (messagePump is not null)
{
	await messagePump.DisposeAsync();
}

if (connection is not null)
{
	await connection.DisposeAsync();
}

// Wait for background tasks to complete (with timeout)
var tasksToWait = new List<Task>();
if (messagePumpTask is not null && !messagePumpTask.IsCompleted)
{
	tasksToWait.Add(messagePumpTask);
}
if (connectionTask is not null && !connectionTask.IsCompleted)
{
	tasksToWait.Add(connectionTask);
}

if (tasksToWait.Any())
{
	using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
	try
	{
		await Task.WhenAll(tasksToWait).WaitAsync(timeoutCts.Token);
	}
	catch (OperationCanceledException)
	{
		logger.LogWarning("Background tasks did not complete within timeout period during disconnect");
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Error waiting for background tasks to complete");
	}
}

// Clean up task references
messagePumpTask = null;
connectionTask = null;
```

**Features**:
- Disposes message pump and connection first (signals them to stop)
- Waits for background tasks with 5-second timeout using `WaitAsync()`
- Handles timeout gracefully with logging
- Nulls out task references after cleanup
- Prevents resource leaks

### 4. Improved DisposeAsync() (~Lines 330-372)
```csharp
public async ValueTask DisposeAsync()
{
	// Cancel the service-level token to signal background tasks to stop
	if (!serviceCts.IsCancellationRequested)
	{
		await serviceCts.CancelAsync();
	}

	// Disconnect all vehicles (this will also stop background tasks)
	await DisconnectAllAsync();

	// Wait for background tasks to complete with timeout
	var tasksToWait = new List<Task>();
	if (messagePumpTask is not null && !messagePumpTask.IsCompleted)
	{
		tasksToWait.Add(messagePumpTask);
	}
	if (connectionTask is not null && !connectionTask.IsCompleted)
	{
		tasksToWait.Add(connectionTask);
	}

	if (tasksToWait.Any())
	{
		try
		{
			// Give tasks 5 seconds to complete gracefully
			await Task.WhenAll(tasksToWait).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
			logger.LogDebug("Background tasks cancelled during disposal");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error waiting for background tasks during disposal");
		}
	}

	// Dispose the cancellation token source
	serviceCts.Dispose();

	GC.SuppressFinalize(this);
}
```

**Features**:
- Cancels service-level token first
- Calls `DisconnectAllAsync()` to clean up all connections
- Waits for any remaining background tasks with timeout
- Disposes `serviceCts` to release resources
- Comprehensive error handling for all cleanup scenarios

## Task Lifecycle Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Service Creation                                             │
│ └─> serviceCts created                                       │
└─────────────────────────────────────────────────────────────┘
							│
							▼
┌─────────────────────────────────────────────────────────────┐
│ ConnectSerialAsync()                                         │
│ ├─> Create messagePump & connection                          │
│ ├─> Create linkedCts (combines method + service tokens)      │
│ ├─> Start messagePumpTask (stored in field)                  │
│ ├─> Start connectionTask (stored in field)                   │
│ └─> Method returns, tasks keep running                       │
└─────────────────────────────────────────────────────────────┘
							│
							▼
┌─────────────────────────────────────────────────────────────┐
│ Tasks run in background for entire connection lifetime       │
│ ├─> messagePump processes vehicle messages                   │
│ └─> connection manages MAVLink protocol                      │
└─────────────────────────────────────────────────────────────┘
							│
							▼
┌─────────────────────────────────────────────────────────────┐
│ DisconnectAsync(vehicleId) OR DisposeAsync()                │
│ ├─> Dispose messagePump & connection (signals stop)          │
│ ├─> Wait for tasks with 5-second timeout                     │
│ ├─> Clean up transport                                       │
│ ├─> Cancel serviceCts (if DisposeAsync)                      │
│ └─> Dispose serviceCts (if DisposeAsync)                     │
└─────────────────────────────────────────────────────────────┘
```

## Benefits

1. **Proper Lifetime Management**: Tasks live as long as the service instance
2. **Graceful Shutdown**: Timeouts prevent hanging during disposal
3. **Resource Safety**: All resources properly cleaned up
4. **Cancellation Support**: Service-level token allows coordinated shutdown
5. **Error Resilience**: Comprehensive error handling prevents cleanup failures
6. **Logging**: Clear visibility into task lifecycle events

## Testing Recommendations

1. **Connect and disconnect multiple times** - verify no resource leaks
2. **Dispose service while connected** - verify graceful shutdown
3. **Cancel during connection** - verify cancellation propagates correctly
4. **Long-running connection** - verify tasks remain alive
5. **Stress test** - rapid connect/disconnect cycles

## Notes

- Tasks are intentionally NOT awaited in `ConnectSerialAsync` - they must run in background
- 5-second timeout chosen to balance responsiveness vs. graceful shutdown
- `WaitAsync()` is .NET 6+ feature for timeout support
- Consider applying same pattern to `ConnectTcpAsync()` and `ConnectUdpAsync()` if they need similar background task management
