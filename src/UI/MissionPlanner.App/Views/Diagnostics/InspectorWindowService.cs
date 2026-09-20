using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Ursa.Controls;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Platform boundary for optional detached Inspector presentation.</summary>
public interface IInspectorWindowService
{
    /// <summary>Whether the current application lifetime supports native windows.</summary>
    bool IsSupported { get; }
    /// <summary>Shows the existing presentation model in a desktop host.</summary>
    void Show(LiveTelemetryInspectorViewModel model, Action closed);
    /// <summary>Focuses an existing detached host.</summary>
    void Focus();
    /// <summary>Closes the optional detached host.</summary>
    void Close();
}

/// <summary>Creates native windows only for a desktop application lifetime.</summary>
public sealed class InspectorWindowService : IInspectorWindowService
{
    private UrsaWindow? window;

    /// <inheritdoc />
    public bool IsSupported => !OperatingSystem.IsBrowser() &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    /// <inheritdoc />
    public void Show(LiveTelemetryInspectorViewModel model, Action closed)
    {
        if (!IsSupported)
        {
            return;
        }
        if (window is not null)
        {
            window.Activate();
            return;
        }
        window = new UrsaWindow
        {
            Title = "Live Telemetry Inspector",
            Width = model.DrawerWidth,
            Height = 800,
            MinWidth = 360,
            MinHeight = 400,
            Content = new LiveTelemetryInspectorView { DataContext = model }
        };
        window.Closed += (_, _) =>
        {
            window = null;
            closed();
        };
        window.Show();
    }

    /// <inheritdoc />
    public void Focus() => window?.Activate();

    /// <inheritdoc />
    public void Close() => window?.Close();
}
