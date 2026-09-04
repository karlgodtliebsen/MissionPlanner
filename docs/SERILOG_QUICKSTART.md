# Serilog quick start

MissionPlanner configures Serilog through the shared library and the Avalonia application's
dependency-injection composition root. Do not configure a second global logger in a view.

## Application configuration

The relevant files are:

- `src/Libraries/MissionPlanner.Library/Configuration/LoggingLibraryConfigurator.cs`
- `src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Configuration/ApplicationConfigurator.cs`
- `src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Utilities/ApplicationRunner.cs`

`ApplicationConfigurator` adds the shared logging configuration to the service collection.
`ApplicationRunner` records startup and unhandled failures. Feature classes receive
`ILogger<T>` through constructor injection or from their application view base.

```csharp
public sealed class VehicleMonitor(ILogger<VehicleMonitor> logger)
{
    public void ReportConnected(string vehicleId)
    {
        logger.LogInformation("Vehicle {VehicleId} connected", vehicleId);
    }
}
```

Use structured properties rather than interpolated message strings. Choose the appropriate
level, avoid logging high-rate telemetry per packet, and never log credentials or secrets.

## Adding a sink

Declare packages in the owning project. Configure sinks once in
`LoggingLibraryConfigurator` or configuration consumed by it. Keep paths under the
application data/log location and create directories before opening a rolling file. Do not
call UI storage-provider dialogs from logging configuration.

## Troubleshooting

- If a category is silent, check the configured minimum level and overrides.
- If a file is missing, verify the resolved application-data path and write permission.
- If shutdown loses events, ensure the host/logger disposal path completes.
- Log UI exceptions in the command or observed asynchronous path; do not use `async void`
  merely to catch them.
- Tests use the registration in `MissionPlanner.Test.Support`, not the desktop host.

After changing logging, build the solution and run the affected host to verify startup, one
structured event, exception output, and clean shutdown.
