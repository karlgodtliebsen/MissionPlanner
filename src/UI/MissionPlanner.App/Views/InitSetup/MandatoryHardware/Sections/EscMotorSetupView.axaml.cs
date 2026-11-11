using Avalonia.Controls;
using Avalonia.Interactivity;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using Ursa.Controls;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays the EscMotorSetupView setup workflow.</summary>
public partial class EscMotorSetupView : UserControlViewBase<EscMotorSetupViewModel>
{
    /// <summary>Initializes the EscMotorSetupView.</summary>
    public EscMotorSetupView()
    {
        InitializeComponent();
    }

    private void OpenMotorTest(object? sender, RoutedEventArgs e)
    {
        var available = TopLevel.GetTopLevel(this)?.ClientSize;
        var view = new MotorTestView
        {
            Width = Math.Min(1000, Math.Max(600, (available?.Width ?? 1096) - 96)),
            Height = Math.Min(800, Math.Max(300, (available?.Height ?? 960) - 160))
        };
        ServiceHelper.GetRequiredService<IDialogService>().ShowStandard(view, view.DataContext,
            options: new OverlayDialogOptions
            {
                Title = "Motor Test",
                Buttons = DialogButton.None,
                IsCloseButtonVisible = true,
                CanResize = true,
                CanDragMove = true,
                CanLightDismiss = false
            });
    }
}
