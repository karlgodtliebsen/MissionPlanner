namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Displays the catalogue firmware panel.</summary>
public partial class FirmwareCatalogueView : UserControlViewBase<FirmwareCatalogueViewModel>
{
    /// <summary>Initializes the view.</summary>
    public FirmwareCatalogueView()
    {
        InitializeComponent();
    }
    //public static readonly StyledProperty<bool> IsDetectedDeviceVisibleProperty =
    //    AvaloniaProperty.Register<FirmwareCatalogueView, bool>(
    //        nameof(IsDetectedDeviceVisible),
    //        defaultValue: false);

    //public bool IsDetectedDeviceVisible
    //{
    //    get => GetValue(IsDetectedDeviceVisibleProperty);
    //    set => SetValue(IsDetectedDeviceVisibleProperty, value);
    //}
}
