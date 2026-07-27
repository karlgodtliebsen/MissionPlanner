using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Dialogs;

namespace UraniumUI.Material.Extensions.Samples.ArduPilotSample;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class VirtualizedDataGridViewModel : ObservableObject
{
    private readonly IDispatcher dispatcher;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IDialogService dialogService;
    private readonly List<ParameterItemViewModel> allParameterItems = [];

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="dispatcher"></param>
    /// <param name="parametersFileHandler"></param>
    /// <param name="dialogService">The dialog service.</param>
    public VirtualizedDataGridViewModel(IDispatcher dispatcher, ParametersFileHandler parametersFileHandler, IDialogService dialogService
    )
    {
        this.dispatcher = dispatcher;
        this.parametersFileHandler = parametersFileHandler;
        this.dialogService = dialogService;
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    [RelayCommand]
    public async Task LoadFromJsonFileAsync()
    {
        allParameterItems.Clear();
        dispatcher.Dispatch(() => Parameters.Clear());

        try
        {
            allParameterItems.AddRange(await parametersFileHandler.LoadParametersFromJsonFileAsync(CancellationToken.None));
            dispatcher.Dispatch(() => Parameters.AddRange(allParameterItems));
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Load failed", exception.Message, "OK");
        }
    }
}
