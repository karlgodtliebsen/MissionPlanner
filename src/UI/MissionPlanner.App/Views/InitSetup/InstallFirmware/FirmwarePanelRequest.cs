using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Identifies work coordinated by the firmware page.</summary>
public enum FirmwarePanelAction
{
    /// <summary>Refresh catalogue and devices.</summary>
    Refresh,
    /// <summary>Download and validate the selection.</summary>
    Download,
    /// <summary>Install an APJ application.</summary>
    Install,
    /// <summary>Install a combined HEX through DFU.</summary>
    InstallDfu
}

/// <summary>An awaitable request sent from a panel to its active parent.</summary>
public sealed class FirmwarePanelRequest(FirmwarePanelAction action, CancellationToken cancellationToken, bool allOptions = false)
{
    /// <summary>Gets the requested operation.</summary>
    public FirmwarePanelAction Action { get; } = action;
    /// <summary>Gets command cancellation.</summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
    /// <summary>Gets whether all catalogue channels should be loaded.</summary>
    public bool AllOptions { get; } = allOptions;
    /// <summary>Gets or sets the operation task assigned by the parent event handler.</summary>
    public Task Completion { get; set; } = Task.CompletedTask;
    /// <summary>Raises a request and returns the task owned by its subscriber.</summary>
    public static Task SendAsync(Action<FirmwarePanelRequest>? handler, FirmwarePanelAction action, CancellationToken token, bool allOptions = false)
    {
        token.ThrowIfCancellationRequested();
        var request = new FirmwarePanelRequest(action, token, allOptions);
        handler?.Invoke(request);
        return request.Completion;
    }
}
