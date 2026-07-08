# Vehicle Parameter Service Usage Guide

This guide explains how to use the MAVLink parameter request/response handling in MissionPlanner Next Generation.

## Overview

The parameter system allows you to:
- Request all parameters from a vehicle
- Request a specific parameter by name
- Set parameter values
- Track parameter updates via domain events

## Architecture

The implementation follows the established service patterns:

### Core Components

1. **Models** (`MissionPlanner.MavLink.Parameters`)
   - `MavParamType` - Parameter type enumeration
   - `VehicleParameter` - Parameter value object

2. **Messages** (`MissionPlanner.MavLink.Messages`)
   - `ParamValueMessage` - Received from vehicle

3. **Services** (`MissionPlanner.Core.Services.Abstractions`)
   - `IVehicleParameterService` - Request/set parameters
   - `IVehicleParameterRegistry` - Store/retrieve parameters

4. **Handlers** (`MissionPlanner.Core.VehicleHandler`)
   - `ParamValueVehicleHandler` - Processes incoming PARAM_VALUE messages

5. **Domain Events** (`MissionPlanner.Core.DomainEvents`)
   - `VehicleParameterReceived` - Published when a parameter is received

## Usage Examples

### 1. Request All Parameters from a Vehicle

```csharp
// Inject the service
public class MyService
{
    private readonly IVehicleParameterService _parameterService;

    public MyService(IVehicleParameterService parameterService)
    {
        _parameterService = parameterService;
    }

    public async Task 
    (VehicleId vehicleId)
    {
        // Request all parameters
        var success = await _parameterService.RequestParameterListAsync(
            vehicleId,
            CancellationToken.None);

        if (success)
        {
            // Parameters will arrive via PARAM_VALUE messages
            // and be published as VehicleParameterReceived events
        }
    }
}
```

### 2. Request a Specific Parameter

```csharp
public async Task RequestSpecificParameterAsync(VehicleId vehicleId)
{
    var success = await _parameterService.RequestParameterAsync(
        vehicleId,
        "ACRO_RP_P", // Parameter name (max 16 chars)
        CancellationToken.None);
}
```

### 3. Set a Parameter Value

```csharp
public async Task SetParameterAsync(VehicleId vehicleId)
{
    var success = await _parameterService.SetParameterAsync(
        vehicleId,
        "ACRO_RP_P",
        4.5f, // New value
        MavParamType.Real32, // Parameter type
        CancellationToken.None);

    // Vehicle will respond with PARAM_VALUE confirming the new value
}
```

### 4. Retrieve Stored Parameters

```csharp
public class MyService
{
    private readonly IVehicleParameterRegistry _parameterRegistry;

    public MyService(IVehicleParameterRegistry parameterRegistry)
    {
        _parameterRegistry = parameterRegistry;
    }

    public void GetParameters(VehicleId vehicleId)
    {
        // Get a specific parameter
        var parameter = _parameterRegistry.GetParameter(vehicleId, "ACRO_RP_P");
        if (parameter != null)
        {
            Console.WriteLine($"{parameter.Name} = {parameter.Value}");
        }

        // Get all parameters
        var allParams = _parameterRegistry.GetAllParameters(vehicleId);
        foreach (var (name, param) in allParams)
        {
            Console.WriteLine($"{name} = {param.Value} ({param.Type})");
        }

        // Get parameter count
        var count = _parameterRegistry.GetParameterCount(vehicleId);
        Console.WriteLine($"Total parameters: {count}");
    }
}
```

### 5. Subscribe to Parameter Updates (Domain Events)

```csharp
public class ParameterMonitor
{
    private readonly IDomainEventHub _eventHub;

    public ParameterMonitor(IDomainEventHub eventHub)
    {
        _eventHub = eventHub;
    }

    public async Task StartMonitoringAsync()
    {
        await _eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(
            async (@event, ct) =>
            {
                var vehicleId = @event.VehicleId;
                var parameter = @event.Parameter;

                Console.WriteLine(
                    $"Parameter received from {vehicleId}: " +
                    $"{parameter.Name} = {parameter.Value} " +
                    $"(index {parameter.Index}/{parameter.Count})");
            });
    }
}
```

## Parameter Types

The `MavParamType` enum supports the following types:

- `Uint8` - 8-bit unsigned integer
- `Int8` - 8-bit signed integer
- `Uint16` - 16-bit unsigned integer
- `Int16` - 16-bit signed integer
- `Uint32` - 32-bit unsigned integer
- `Int32` - 32-bit signed integer
- `Real32` - 32-bit floating point (most common)

## MAVLink Protocol Details

### Message Flow

1. **Request All Parameters**
   ```
   GCS → Vehicle: PARAM_REQUEST_LIST
   Vehicle → GCS: PARAM_VALUE (x N, where N = total parameter count)
   ```

2. **Request Specific Parameter**
   ```
   GCS → Vehicle: PARAM_REQUEST_READ (with param_id)
   Vehicle → GCS: PARAM_VALUE (single response)
   ```

3. **Set Parameter**
   ```
   GCS → Vehicle: PARAM_SET (with param_id, value, type)
   Vehicle → GCS: PARAM_VALUE (confirms new value)
   ```

### Parameter Storage

- Parameters are stored per-vehicle in the `IVehicleParameterRegistry`
- Thread-safe concurrent storage using `ConcurrentDictionary`
- Parameters persist in memory until the vehicle is disconnected or parameters are cleared

## Implementation Notes

- Parameter names are limited to 16 characters (MAVLink specification)
- Values are transmitted as 32-bit floats on the wire, regardless of actual type
- The parameter type indicates how the value should be interpreted
- Parameter index and count are used to track download progress
- All parameter operations are asynchronous and return `Task<bool>`

## Next Steps

For a complete parameter UI implementation (similar to the original MissionPlanner), you would need to:

1. Implement parameter metadata system (download XML definitions from ArduPilot)
2. Add parameter validation (ranges, options, read-only flags)
3. Create UI components for parameter display/editing
4. Add parameter file import/export functionality
5. Implement parameter comparison features
