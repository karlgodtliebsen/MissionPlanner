using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.Exit;

//NavigationViewBase<PreferencesViewModel>
/// <inheritdoc />
public partial class ExitView : NavigationViewBase<ExitViewModel>
{
    private readonly IDialogService dialogService;
    private readonly IDomainEventHub eventHub;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExitView"/> class.
    /// </summary>
    public ExitView()
    {
        //   InitializeComponent();
        dialogService = ServiceHelper.GetRequiredService<IDialogService>();
        eventHub = ServiceHelper.GetRequiredService<IDomainEventHub>();
    }

    ///// <inheritdoc />
    //protected override void OnAppearing()
    //{
    //   // base.OnAppearing();
    //    //ShowExitDialog().FireAndForget();
    //}

    //private async Task ShowExitDialog()
    //{
    //    var exitContentView = ServiceHelper.GetRequiredService<ExitUserControlView>();
    //    var result = await dialogService.DisplayViewAsync(
    //        "Exit MissionPlanner", exitContentView, "Yes", "Cancel");

    //    if (result)
    //    {
    //        // Handle the exit logic here
    //        await eventHub.PublishDomainEventAsync(new ExitApplicationRequested());
    //    }
    //}
}
