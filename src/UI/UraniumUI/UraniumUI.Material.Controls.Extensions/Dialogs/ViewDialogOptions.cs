namespace UraniumUI.Material.Dialogs;

/// <summary>
/// Options for configuring the appearance and behavior of a view dialog.
/// </summary>
public sealed record ViewDialogOptions
{
    /// <summary>
    /// Requested size of the complete dialog, including header and footer.
    /// It will always be clamped to the current page/window.
    /// </summary>
    public Size? RequestedSize { get; init; }

    /// <summary>
    /// Space retained between the dialog and the current window.
    /// Phone dialogs use at most 12 DIPs regardless of this value.
    /// </summary>
    public double OuterMargin { get; init; } = 24;

    public double DefaultDesktopWidth { get; init; } = 1024;

    public double DefaultDesktopHeight { get; init; } = 768;

    public double DefaultTabletWidthRatio { get; init; } = 0.90;

    public double DefaultTabletHeightRatio { get; init; } = 0.90;

    public bool CanDismissByTappingOutside { get; init; }

    public double MinimumHeightRequest { get; set; }
    public double MinimumWidthRequest { get; set; }


    /// <summary>
    /// Enable only when the supplied content does not already contain
    /// a ScrollView, CollectionView, DataGrid, or another scrolling control.
    /// </summary>
    public bool WrapContentInScrollView { get; init; }
}
