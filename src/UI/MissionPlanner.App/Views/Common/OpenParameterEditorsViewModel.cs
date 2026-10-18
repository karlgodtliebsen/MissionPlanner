using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using Ursa.Controls;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the OpenParameterEditorsView, responsible for handling user interactions and navigation related to parameter editors. 
/// </summary>
/// <param name="navigation"></param>
/// <param name="services">Creates independent editor views and sessions.</param>
public partial class OpenParameterEditorsViewModel(INavigationService navigation, IServiceProvider services) : ViewModelBase
{
    /// <summary>Opens the shared Parameters Editor workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync(string commandParameter)
    {
        if (IsBusy)
        {
            return;
        }

        if (commandParameter == "Navigate")
        {
            await navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
            return;
        }
        if (commandParameter == "ParametersEditorInWindow")
        {
            if (!CanOpenWindow())
            {
                return;
            }
            var view = services.GetRequiredService<ParametersEditorTabView>();
            using var model = (ParametersEditorTabViewModel)view.DataContext!;
            model.DialogHostId = $"parameters-editor-{Guid.NewGuid():N}";
            await ShowEditorWindowAsync(view, "Parameters Editor", () => model.IsBusy, model.DialogHostId);
            return;
        }
    }

    private bool CanOpenWindow()
    {
        if (!OperatingSystem.IsBrowser() && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            return true;
        }
        NotificationManager?.Show("Separate editor windows are available in the desktop app.");
        return false;
    }

    private static async Task ShowEditorWindowAsync(Control view, string title, Func<bool> isBusy, string? hostId = null)
    {
        var window = new UrsaWindow
        {
            Title = title,
            Width = 1200,
            Height = 900,
            MinWidth = 800,
            MinHeight = 600,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Grid
            {
                // Ursa draws its title bar over the client area. Reserve the same
                // space as MainWindow so it cannot intercept editor toolbar clicks.
                Margin = new Thickness(0, 50, 0, 0),
                Children = { view, new OverlayDialogHost { HostId = hostId ?? $"parameter-editor-{Guid.NewGuid():N}" } }
            }
        };
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closing += (_, args) => args.Cancel = isBusy();
        window.Closed += (_, _) => closed.TrySetResult();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
        await closed.Task;
    }
}
