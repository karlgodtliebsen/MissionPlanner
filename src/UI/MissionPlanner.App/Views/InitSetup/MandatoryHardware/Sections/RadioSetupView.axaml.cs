using Avalonia.Controls;
using Avalonia.Interactivity;
using MissionPlanner.App.Utilities.Dialogs;
using Ursa.Controls;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays the radio calibration workflow and compact receiver channels.</summary>
public partial class RadioSetupView : UserControlViewBase<RadioSetupViewModel>
{
    public RadioSetupView() => InitializeComponent();

    private void ShowChannelDetails(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RadioChannelDisplayViewModel channel }) return;
        ServiceHelper.GetRequiredService<IDialogService>().ShowStandard(
            new RadioChannelDetailsView { Width = 520, MaxWidth = 640 }, channel,
            options: new OverlayDialogOptions
            {
                Title = channel.Title,
                Buttons = DialogButton.OK,
                IsCloseButtonVisible = true,
                CanResize = false,
                CanLightDismiss = true
            });
    }
}
