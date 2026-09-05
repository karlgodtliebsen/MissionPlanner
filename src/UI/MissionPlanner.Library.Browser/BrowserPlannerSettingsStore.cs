using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library.Browser.Interop;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Library.Browser;

/// <summary>Persists non-secret settings in the current browser origin's local storage.</summary>
public sealed class BrowserPlannerSettingsStore(ILogger<BrowserPlannerSettingsStore> logger) : IPlannerSettingsStore
{
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(BrowserInterop.ReadSettings());
        }
        catch (JSException exception)
        {
            // Storage may be disabled by browser policy. Startup can still use defaults.
            logger.LogWarning(exception, "Browser settings storage is unavailable; using defaults.");
            return ValueTask.FromResult<string?>(null);
        }
    }

    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        BrowserInterop.WriteSettings(document);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BrowserInterop.ClearSettings();
        return ValueTask.CompletedTask;
    }
}
