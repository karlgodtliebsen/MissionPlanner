# Parameter Metadata System

The Parameter Metadata System provides rich contextual information about ArduPilot parameters, including descriptions, valid ranges, units, and enumerated values.

## Overview

The metadata system complements the parameter values retrieved from vehicles by providing:
- **Human-readable descriptions** of what each parameter does
- **Valid ranges** (min/max values)
- **Units of measurement** (e.g., m/s, deg, Hz)
- **Enumerated options** (e.g., 0:Disabled, 1:Enabled)
- **Bitmask definitions** for flag-based parameters
- **Read-only flags** and reboot requirements

## Architecture

```
┌─────────────────────────────────────────────┐
│  IVehicleParameterMetadataService           │  ← UI Layer
│  (High-level API for metadata lookup)       │
└────────────────┬────────────────────────────┘
                 │
┌────────────────▼────────────────────────────┐
│  IParameterMetadataRepository                │
│  (Caching & management)                      │
└────────────────┬────────────────────────────┘
                 │
        ┌────────┴──────────┐
        │                   │
┌───────▼──────┐   ┌────────▼─────────┐
│  Downloader  │   │      Parser      │
│ (HTTP/GZIP)  │   │  (XML parsing)   │
└──────────────┘   └──────────────────┘
        │                   │
        └────────┬──────────┘
                 │
        ┌────────▼─────────┐
        │  Local File Cache │
        │  (7-day expiry)   │
        └──────────────────┘
```

## Components

### 1. Models

**`ParameterMetadata`** (`MissionPlanner.MavLink.Parameters`)
```csharp
public sealed record ParameterMetadata(
    string Name,
    string? DisplayName,
    string? Description,
    string? Units,
    string? Range,           // "min max"
    string? Values,          // "0:Disabled,1:Enabled"
    string? Bitmask,         // "0:Bit0,1:Bit1"
    string? Increment,
    string? UserLevel,       // "Standard" or "Advanced"
    bool RebootRequired,
    bool ReadOnly)
{
    // Helper properties
    float? MinValue { get; }
    float? MaxValue { get; }
    float? IncrementValue { get; }

    // Helper methods
    Dictionary<float, string> GetValueOptions();
    Dictionary<int, string> GetBitmaskOptions();
    bool IsValueValid(float value);
    string? GetValidationError(float value);
}
```

**`VehicleType`** (`MissionPlanner.MavLink.Parameters`)
```csharp
public enum VehicleType
{
    ArduCopter, ArduPlane, Rover, ArduSub,
    AntennaTracker, AP_Periph, SITL, Blimp, Heli
}
```

### 2. Parser

**`IParameterMetadataParser`** → **`ParameterMetadataXmlParser`**
- Parses ArduPilot XML parameter definition files
- Format: `apm.pdef.xml` (see example below)
- Handles vehicle-specific sections (ArduCopter2, ArduPlane, etc.)

### 3. Downloader

**`IParameterMetadataDownloader`** → **`ParameterMetadataDownloader`**
- Downloads from: `https://autotest.ardupilot.org/Parameters/{vehicle}/apm.pdef.xml.gz`
- Decompresses GZIP files
- Uses `IHttpClientFactory` for reliable HTTP operations
- Timeout: 30 seconds

### 4. Repository (Cache Manager)

**`IParameterMetadataRepository`** → **`ParameterMetadataRepository`**
- **In-memory cache**: Fast lookups per vehicle type
- **File cache**: `%LocalAppData%\MissionPlanner\ParameterCache\{VehicleType}_metadata.xml`
- **Cache expiry**: 7 days
- **Thread-safe**: Uses `SemaphoreSlim` for download synchronization
- **Auto-refresh**: Downloads when cache is expired or missing

### 5. Service (Application Layer)

**`IVehicleParameterMetadataService`** → **`VehicleParameterMetadataService`**
- Maps vehicle IDs to vehicle types automatically
- Provides convenient lookup by `VehicleId` or `VehicleType`
- Integrates with `IVehicleRegistry`

## Usage Examples

### Basic Usage

```csharp
public class ParameterViewModel
{
    private readonly IVehicleParameterMetadataService _metadataService;
    private readonly IVehicleParameterService _parameterService;
    private readonly IVehicleParameterRegistry _parameterRegistry;

    public async Task LoadParametersAsync(VehicleId vehicleId)
    {
        // Request parameters from vehicle
        await _parameterService.RequestParameterListAsync(vehicleId);

        // Get all parameters
        var parameters = _parameterRegistry.GetAllParameters(vehicleId);

        // Get metadata for each parameter
        foreach (var param in parameters)
        {
            var metadata = await _metadataService.GetMetadataAsync(
                vehicleId,
                param.Key);

            if (metadata != null)
            {
                DisplayParameter(param.Value, metadata);
            }
        }
    }

    private void DisplayParameter(VehicleParameter param, ParameterMetadata metadata)
    {
        Console.WriteLine($"{metadata.DisplayName ?? param.Name}");
        Console.WriteLine($"  Value: {param.Value} {metadata.Units ?? ""}");
        Console.WriteLine($"  Description: {metadata.Description}");

        if (metadata.Range != null)
        {
            Console.WriteLine($"  Range: {metadata.MinValue} - {metadata.MaxValue}");
        }
    }
}
```

### Validation Before Setting

```csharp
public async Task<bool> SetParameterWithValidationAsync(
    VehicleId vehicleId,
    string paramName,
    float newValue)
{
    // Get metadata
    var metadata = await _metadataService.GetMetadataAsync(vehicleId, paramName);

    if (metadata == null)
    {
        _logger.LogWarning("No metadata for parameter {ParamName}", paramName);
        // Proceed anyway, or reject?
        return false;
    }

    // Check read-only
    if (metadata.ReadOnly)
    {
        _logger.LogError("Parameter {ParamName} is read-only", paramName);
        return false;
    }

    // Validate range
    if (!metadata.IsValueValid(newValue))
    {
        var error = metadata.GetValidationError(newValue);
        _logger.LogError("Invalid value for {ParamName}: {Error}", paramName, error);
        return false;
    }

    // Warn if reboot required
    if (metadata.RebootRequired)
    {
        _logger.LogWarning(
            "Changing {ParamName} requires vehicle reboot",
            paramName);
    }

    // Set the parameter
    var param = _parameterRegistry.GetParameter(vehicleId, paramName);
    if (param != null)
    {
        await _parameterService.SetParameterAsync(
            vehicleId,
            paramName,
            newValue,
            param.Type);
        return true;
    }

    return false;
}
```

### Working with Enumerated Values

```csharp
public async Task DisplayEnumeratedParameterAsync(
    VehicleId vehicleId,
    string paramName)
{
    var param = _parameterRegistry.GetParameter(vehicleId, paramName);
    var metadata = await _metadataService.GetMetadataAsync(vehicleId, paramName);

    if (param != null && metadata != null && metadata.Values != null)
    {
        var options = metadata.GetValueOptions();

        Console.WriteLine($"{metadata.DisplayName}:");
        Console.WriteLine($"  Current: {param.Value} = {options.GetValueOrDefault(param.Value, "Unknown")}");
        Console.WriteLine("  Options:");

        foreach (var (value, label) in options.OrderBy(x => x.Key))
        {
            var current = value == param.Value ? " (current)" : "";
            Console.WriteLine($"    {value}: {label}{current}");
        }
    }
}

// Example output:
// Flight Mode:
//   Current: 2 = AltHold
//   Options:
//     0: Stabilize
//     1: Acro
//     2: AltHold (current)
//     3: Auto
//     4: Guided
```

### Working with Bitmask Parameters

```csharp
public async Task DisplayBitmaskParameterAsync(
    VehicleId vehicleId,
    string paramName)
{
    var param = _parameterRegistry.GetParameter(vehicleId, paramName);
    var metadata = await _metadataService.GetMetadataAsync(vehicleId, paramName);

    if (param != null && metadata != null && metadata.Bitmask != null)
    {
        var bits = metadata.GetBitmaskOptions();
        var value = (int)param.Value;

        Console.WriteLine($"{metadata.DisplayName}:");
        Console.WriteLine($"  Value: {value}");
        Console.WriteLine("  Flags:");

        foreach (var (bitPos, label) in bits.OrderBy(x => x.Key))
        {
            var isSet = (value & (1 << bitPos)) != 0;
            var status = isSet ? "ON" : "OFF";
            Console.WriteLine($"    [{status}] {label}");
        }
    }
}

// Example output:
// ARMING_CHECK:
//   Value: 7
//   Flags:
//     [ON]  All checks
//     [ON]  Barometer
//     [ON]  Compass
//     [OFF] GPS
```

### Refresh Metadata Cache

```csharp
public async Task RefreshMetadataCacheAsync(VehicleType vehicleType)
{
    _logger.LogInformation("Refreshing metadata cache for {VehicleType}", vehicleType);

    await _metadataService.RefreshMetadataAsync(vehicleType);

    _logger.LogInformation("Metadata cache refreshed successfully");
}

// Clear entire cache
public void ClearAllMetadataCache()
{
    var repository = serviceProvider.GetRequiredService<IParameterMetadataRepository>();
    repository.ClearCache();
}
```

## XML File Format

ArduPilot parameter metadata files use this XML structure:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Params>
  <ArduCopter2>
    <ACRO_RP_P>
      <DisplayName>Acro Roll and Pitch P gain</DisplayName>
      <Description>Converts pilot roll and pitch into a desired rate of rotation in ACRO mode.</Description>
      <Range>1 10</Range>
      <Units>deg/s</Units>
      <User>Standard</User>
    </ACRO_RP_P>

    <ARMING_CHECK>
      <DisplayName>Arming Check</DisplayName>
      <Description>Checks prior to arming motor.</Description>
      <Bitmask>0:All,1:Barometer,2:Compass,3:GPS,4:INS,5:Parameters,6:RC,7:Board voltage,8:Battery Level</Bitmask>
      <User>Standard</User>
    </ARMING_CHECK>

    <SYSID_MYGCS>
      <DisplayName>My ground station number</DisplayName>
      <Description>Allows restricting radio overrides to only come from my ground station.</Description>
      <Values>0:Disabled,1:Enabled</Values>
      <ReadOnly>True</ReadOnly>
      <User>Advanced</User>
    </SYSID_MYGCS>
  </ArduCopter2>
</Params>
```

## Cache Location

Metadata is cached locally to improve performance:

**Windows**: `C:\Users\{User}\AppData\Local\MissionPlanner\ParameterCache\`
**macOS**: `/Users/{User}/Library/Application Support/MissionPlanner/ParameterCache/`
**Linux**: `/home/{User}/.local/share/MissionPlanner/ParameterCache/`

Cache files:
- `ArduCopter_metadata.xml`
- `ArduPlane_metadata.xml`
- `Rover_metadata.xml`
- etc.

## Error Handling

The system handles errors gracefully:

1. **Download failures**: Logged as errors, returns empty dictionary
2. **Parse failures**: Logged as warnings for individual parameters
3. **Missing metadata**: Returns `null` for specific parameters
4. **Cache errors**: Falls back to download, continues without file cache

```csharp
var metadata = await metadataService.GetMetadataAsync(vehicleId, "UNKNOWN_PARAM");
if (metadata == null)
{
    // No metadata available - parameter still works, just no rich info
    _logger.LogDebug("No metadata for UNKNOWN_PARAM");
}
```

## Performance Considerations

- **First call**: ~2-5 seconds (download + decompress + parse)
- **Cached calls**: <10ms (memory lookup)
- **Memory footprint**: ~1-2 MB per vehicle type (in-memory cache)
- **Disk space**: ~500 KB per vehicle type (compressed XML)
- **Thread-safe**: Concurrent calls for same vehicle type are deduplicated

## Best Practices

### 1. Preload Metadata on App Startup

```csharp
public async Task PreloadMetadataAsync()
{
    // Preload common vehicle types
    var types = new[] { VehicleType.ArduCopter, VehicleType.ArduPlane };

    await Parallel.ForEachAsync(types, async (type, ct) =>
    {
        await metadataService.GetAllMetadataAsync(type, ct);
    });
}
```

### 2. Display Loading Indicators

```csharp
LoadingIndicator.Show("Loading parameter definitions...");
try
{
    var metadata = await metadataService.GetAllMetadataAsync(vehicleId);
}
finally
{
    LoadingIndicator.Hide();
}
```

### 3. Handle Missing Metadata Gracefully

```csharp
var displayName = metadata?.DisplayName ?? paramName;
var description = metadata?.Description ?? "No description available";
var units = metadata?.Units ?? "";
```

### 4. Use Validation in UI

```csharp
// In a WPF/MAUI TextBox validation
private async Task ValidateParameterValue(string paramName, string input)
{
    if (!float.TryParse(input, out var value))
    {
        ShowError("Invalid number");
        return;
    }

    var metadata = await metadataService.GetMetadataAsync(vehicleId, paramName);
    var error = metadata?.GetValidationError(value);

    if (error != null)
    {
        ShowError(error);
    }
}
```

## Integration with UI

The metadata system is designed to support rich UI features:

- **Autocomplete**: Use `GetAllMetadataAsync()` to populate search/filter
- **Grouped views**: Group by first part of name (e.g., all `ACRO_*` parameters)
- **Type-specific editors**:
  - ComboBox for enumerated values
  - Checkboxes for bitmasks
  - NumericUpDown with min/max for ranges
- **Tooltips**: Show description on hover
- **Validation**: Real-time feedback before sending to vehicle
- **Read-only indicators**: Disable editing for read-only parameters
- **Reboot warnings**: Show icon/badge for parameters requiring reboot

## Summary

The Parameter Metadata System provides:
- ✅ Automatic download and caching of parameter definitions
- ✅ 7-day cache expiry with auto-refresh
- ✅ Rich metadata (descriptions, ranges, units, options)
- ✅ Validation helpers
- ✅ Thread-safe operations
- ✅ Integration with vehicle registry
- ✅ Graceful error handling

You now have everything needed to build a full-featured parameter editor UI!
