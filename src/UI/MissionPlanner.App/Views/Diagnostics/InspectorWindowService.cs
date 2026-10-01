using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using System.ComponentModel;
using MissionPlanner.App.Views.Analysis;
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
        using var iconStream = Avalonia.Platform.AssetLoader.Open(
            new Uri("avares://MissionPlanner.App/Resources/AppIcon/mpdesktop.ico"));
        window = new UrsaWindow
        {
            Title = "Diagnostics",
            Icon = new Avalonia.Controls.WindowIcon(iconStream),
            Width = model.DrawerWidth,
            Height = 800,
            MinWidth = 360,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        void UpdateContent()
        {
            if (window is null)
            {
                return;
            }
            if (model.ShowDataFlashLogs)
            {
                if (window.Content is not FftAnalysisView)
                {
                    window.Content = new FftAnalysisView();
                    window.Width = 1200;
                    window.Height = 850;
                    window.MinWidth = 800;
                }
                window.Title = "DataFlash Log Browser";
            }
            else if (window.Content is not LiveTelemetryInspectorView)
            {
                window.Content = new LiveTelemetryInspectorView { DataContext = model };
                window.Title = "Diagnostics";
                window.MinWidth = 360;
                window.Width = model.DrawerWidth;
            }
        }
        void DestinationChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(model.Destination))
            {
                UpdateContent();
            }
        }
        UpdateContent();
        model.PropertyChanged += DestinationChanged;
        window.Closed += (_, _) =>
        {
            model.PropertyChanged -= DestinationChanged;
            window = null;
            closed();
        };
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            window.Show(owner);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
        }
    }

    /// <inheritdoc />
    public void Focus() => window?.Activate();

    /// <inheritdoc />
    public void Close() => window?.Close();
}
