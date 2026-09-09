using Avalonia.Controls;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Displays the selected firmware panel.</summary>
public partial class SelectedFirmwareView : UserControl
{
    /// <summary>Controls the APJ preparation action when reused as catalogue identity in DFU.</summary>
    public static readonly Avalonia.StyledProperty<bool> ShowApjDownloadProperty =
        Avalonia.AvaloniaProperty.Register<SelectedFirmwareView, bool>(nameof(ShowApjDownload), true);

    /// <summary>Gets or sets whether the APJ download action is shown.</summary>
    public bool ShowApjDownload
    {
        get => GetValue(ShowApjDownloadProperty);
        set => SetValue(ShowApjDownloadProperty, value);
    }

    /// <summary>Initializes the view.</summary>
    public SelectedFirmwareView()
    {
        InitializeComponent();
    }
}
