using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>
/// View model for the ConfirmDfuTargetView.xaml view.
/// </summary>
public sealed partial class ConfirmDfuTargetViewModel : DialogViewModelBase
{
    /// <summary>Initializes the diagnostics panel.</summary>
    public ConfirmDfuTargetViewModel(string expectedMessage, string message1, string message2, ILogger<ConfirmDfuTargetViewModel> logger)
    {
        this.ExpectedMessage = expectedMessage;
        this.Message1 = message1;
        this.Message2 = message2;
        Title = "Confirm exact DFU Target";
    }

    [ObservableProperty]
    public partial string? TargetMessage
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Message1
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Message2
    {
        get;
        set;
    }
    [ObservableProperty]
    public partial string? ExpectedMessage
    {
        get;
        set;
    }

}
