using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Helpers;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel(ILogger logger) : BaseViewModel(logger)
{
    /// <summary>
    /// Gets the dispatcher for the current context.
    /// </summary>
    public required IDispatcher Dispatcher = ServiceHelper.GetRequiredService<IDispatcher>();


    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress
    {
        get; set;
    }


    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}
