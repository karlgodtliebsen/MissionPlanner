using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Common;

namespace MissionPlanner.AvaloniaUI.App.Views.Samples;

/// <summary>
/// ViewModel for the Demo DataGridPage.
/// </summary>
public partial class DataGridViewModel : ViewModelBase
{
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IDialogService dialogService;
    private readonly List<ParameterItemViewModel> allParameterItems = [];

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="parametersFileHandler"></param>
    /// <param name="dialogService">The dialog service.</param>
    public DataGridViewModel(ParametersFileHandler parametersFileHandler, IDialogService dialogService)
    {
        this.parametersFileHandler = parametersFileHandler;
        this.dialogService = dialogService;
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the currently visible parameter rows.</summary>

    [RelayCommand]
    public async Task LoadFromJsonFileAsync(CancellationToken cancellationToken)
    {
        allParameterItems.Clear();
        await Dispatcher.DispatchAsync(() => Parameters.Clear());

        try
        {
            Environment.CurrentDirectory = ".";

            allParameterItems.AddRange(await parametersFileHandler.LoadParametersFromJsonFileAsync(cancellationToken));

            await Dispatcher.DispatchAsync(() => Parameters.AddRange(allParameterItems));
        }
        catch (Exception exception)
        {
            var options = AvaloniaDialogService.CreateDialogOptions("Load failed", "Ok", null);
            await dialogService.ConfirmAsync(options, exception.Message, cancellationToken);
        }
    }


}


