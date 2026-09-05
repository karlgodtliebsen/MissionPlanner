using Avalonia;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// Options for configuring the appearance and behavior of a view dialog.
/// </summary>
public sealed record ViewDialogOptions
{
    /// <summary>
    /// Requested size of the complete dialog, including header and footer.
    /// It will always be clamped to the current page/window.
    /// </summary>
    public Size? RequestedSize
    {
        get; init;
    }

    /// <summary>
    /// Space retained between the dialog and the current window.
    /// Phone dialogs use at most 12 DIPs regardless of this value.
    /// </summary>
    public double OuterMargin { get; init; } = 24;

    /// <summary>
    /// Default width of the dialog on a desktop device.
    /// </summary>
    public double DefaultDesktopWidth { get; init; } = 1024;

    /// <summary>
    /// Default height of the dialog on a desktop device.
    /// </summary>      
    public double DefaultDesktopHeight { get; init; } = 768;

    /// <summary>
    /// Default width ratio of the dialog on a tablet device, relative to the screen width.
    /// </summary>
    public double DefaultTabletWidthRatio { get; init; } = 0.90;

    /// <summary>
    /// Default height ratio of the dialog on a tablet device, relative to the screen height.
    /// </summary>
    public double DefaultTabletHeightRatio { get; init; } = 0.90;

    /// <summary>
    /// Indicates whether the dialog can be dismissed by tapping outside of it.
    /// </summary>
    public bool CanDismissByTappingOutside
    {
        get; init;
    }

    /// <summary>
    ///    Minimum height request for the dialog content. This value is used to ensure that the dialog has a minimum height, even if the content does not require it.
    /// </summary>
    public double MinimumHeightRequest
    {
        get; set;
    }

    /// <summary>
    ///   Minimum width request for the dialog content. This value is used to ensure that the dialog has a minimum width, even if the content does not require it.
    /// </summary>
    public double MinimumWidthRequest
    {
        get; set;
    }


    /// <summary>
    /// Enable only when the supplied content does not already contain
    /// a ScrollView, CollectionView, DataGrid, or another scrolling control.
    /// </summary>
    public bool WrapContentInScrollView
    {
        get; init;
    }
}
