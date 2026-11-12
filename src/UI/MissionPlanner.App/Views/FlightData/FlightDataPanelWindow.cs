using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using Ursa.Controls;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>Moves a Flight Data section into a desktop window and restores it on close.</summary>
public sealed class FlightDataPanelWindow(ContentControl dock, string title, Size size, Action changed)
{
    private UrsaWindow? window;

    /// <summary>Gets whether this section is currently in a separate window.</summary>
    public bool IsOpen => window is not null;

    /// <summary>Shows the original controls, preserving their view models and selected tab.</summary>
    public void Show(Window owner)
    {
        if (window is not null)
        {
            window.Activate();
            return;
        }
        if (dock.Content is not Control content)
        {
            return;
        }

        var host = new ContentControl
        {
            // Ursa's title bar overlays the client area, as in the Parameters Editor window.
            Margin = new Thickness(0, 50, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        using var icon = AssetLoader.Open(new Uri("avares://MissionPlanner.Themes/Resources/AppIcon/mpdesktop.ico"));
        var popup = new UrsaWindow
        {
            Title = title,
            Icon = new WindowIcon(icon),
            Width = size.Width,
            Height = size.Height,
            MinWidth = 360,
            MinHeight = 350,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            DataContext = dock.DataContext,
            Content = host
        };
        window = popup;
        void Restore()
        {
            if (!ReferenceEquals(window, popup)) return;
            host.Content = null;
            popup.Content = null;
            dock.Content = content;
            window = null;
            changed();
        }
        popup.Closed += (_, _) => Restore();
        try
        {
            dock.Content = null;
            host.Content = content;
            changed();
            popup.Show(owner);
        }
        catch
        {
            Restore();
            popup.Close();
            throw;
        }
    }

    /// <summary>Closes the separate window and puts its section back into the page.</summary>
    public void Close() => window?.Close();
}
