# Parameter Streaming Diagnostics

## Critical Discovery from Original MissionPlanner

Looking at the original code (`src-v.1/ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs:2073`):

```csharp
// exclude index of 65535
if (par.param_index != 65535)
    indexsreceived.Add(par.param_index);
```

**Index 65535 (0xFFFF) is a special "invalid" index** that should be excluded from tracking!

## Add This Diagnostic Code to Your Test

Add this after the streaming completes to see what's actually happening:

```csharp
[Fact]
public async Task Diagnose_Parameter_Indices()
{
    // ... your connection code ...

    var vehicleId = connection.VehicleId.Value;
    var parameterRegistry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();

    // Let parameters stream in
    await Task.Delay(TimeSpan.FromSeconds(10));

    var allParams = parameterRegistry.GetAllParameters(vehicleId);
    var totalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

    _testOutputHelper.WriteLine($"Total count reported: {totalCount}");
    _testOutputHelper.WriteLine($"Parameters received: {allParams.Count}");

    // Analyze indices
    var indices = allParams.Values.Select(p => p.Index).OrderBy(i => i).ToList();

    _testOutputHelper.WriteLine($"\nIndices received: {indices.Count}");
    _testOutputHelper.WriteLine($"Min index: {indices.Min()}");
    _testOutputHelper.WriteLine($"Max index: {indices.Max()}");

    // Check for index 65535 (invalid index)
    var invalidIndices = allParams.Values.Where(p => p.Index == 65535).ToList();
    _testOutputHelper.WriteLine($"\nParameters with index 65535 (invalid): {invalidIndices.Count}");
    foreach (var p in invalidIndices.Take(10))
    {
        _testOutputHelper.WriteLine($"  {p.Name} = {p.Value}");
    }

    // Check for duplicates
    var duplicates = indices.GroupBy(i => i).Where(g => g.Count() > 1).ToList();
    _testOutputHelper.WriteLine($"\nDuplicate indices: {duplicates.Count}");
    foreach (var dup in duplicates.Take(10))
    {
        _testOutputHelper.WriteLine($"  Index {dup.Key}: {dup.Count()} times");
    }

    // Check for gaps
    var validIndices = indices.Where(i => i != 65535).ToHashSet();
    var missing = Enumerable.Range(0, totalCount)
        .Select(i => (ushort)i)
        .Where(i => !validIndices.Contains(i))
        .ToList();

    _testOutputHelper.WriteLine($"\nMissing indices (excluding 65535): {missing.Count}");
    _testOutputHelper.WriteLine($"First 20 missing: {string.Join(", ", missing.Take(20))}");

    // Show first 20 parameters with their indices
    _testOutputHelper.WriteLine($"\nFirst 20 parameters:");
    foreach (var param in allParams.Values.OrderBy(p => p.Index).Take(20))
    {
        _testOutputHelper.WriteLine($"  [{param.Index:D4}] {param.Name} = {param.Value}");
    }

    // Show last 20 parameters
    _testOutputHelper.WriteLine($"\nLast 20 parameters:");
    foreach (var param in allParams.Values.OrderBy(p => p.Index).TakeLast(20))
    {
        _testOutputHelper.WriteLine($"  [{param.Index:D4}] {param.Name} = {param.Value}");
    }
}
```

## Questions to Answer

Running this diagnostic will help answer:

1. **How many parameters have index 65535?**
   - These should be excluded from tracking (see original MissionPlanner line 2073)

2. **Are there duplicate indices?**
   - Duplicates are OK (we handle them), but shows if params are being resent

3. **What's the actual index range?**
   - Are indices 0-1100, or something else?
   - Are there large gaps?

4. **What are the missing indices?**
   - Identifies exactly which parameters are not arriving

## Expected Issues

### Issue 1: Index 65535 Not Excluded

If you see many parameters with index 65535, they should NOT count toward completion:

```
Parameters with index 65535 (invalid): 138
  FENCE_ENABLE = 1
  ARMING_CHECK = 1
  ...
```

**Fix**: Update VehicleParameterStreamService to exclude index 65535:

```csharp
var receivedIndices = currentParams.Values
    .Where(p => p.Index != 65535)  // ← Add this
    .Select(p => p.Index)
    .ToHashSet();
```

### Issue 2: Non-Sequential Indices

If indices are not 0-1100 but something like [0-500, 1000-1600]:

```
Min index: 0
Max index: 1600
Missing indices: 580
```

This means the firmware uses non-sequential indices. We need to track "set of received" not "count vs total".

### Issue 3: Packet Loss Pattern

If you consistently see the same missing indices:

```
Missing: 45, 89, 127, 189, 243, 345, 456, 567, 678, 789, 890, 991
```

This suggests a pattern - maybe every Nth parameter is being lost due to buffer overflow.

## Alternative: Enable Verbose Logging

Add this to your appsettings.json or test configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "MissionPlanner.Core.VehicleHandler.ParamValueVehicleHandler": "Debug",
      "MissionPlanner.Core.Services.VehicleParameterStreamService": "Debug",
      "MissionPlanner.MavLink.Services.MavLinkV2FrameParser": "Information"
    }
  }
}
```

This will log every parameter as it arrives with its index.

## Next Steps

1. Run the diagnostic test above
2. Share the output here
3. I'll analyze the index pattern and adjust the strategy accordingly

The fact that original MissionPlanner gets all params in 1-2 seconds means it's solvable - we just need to match their approach exactly.
