using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.FlightData.Auxiliary;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Generated;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class AuxFunctionTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IAuxiliaryFunctionCatalog catalog;
    private readonly IAuxiliaryFunctionService service;

    /// <summary>Initializes the auxiliary-function tab for the active vehicle lifetime.</summary>
    public AuxFunctionTabViewModel(IActiveVehicleContext activeVehicle, IAuxiliaryFunctionCatalog catalog,
        IAuxiliaryFunctionService service, ILogger<AuxFunctionTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.catalog = catalog;
        this.service = service;
    }

    /// <summary>Gets available reviewed functions.</summary>
    public ObservableRangeCollection<AuxiliaryFunctionDescriptor> Functions
    {
        get;
    } = [];

    /// <summary>Gets or sets the selected function.</summary>
    [ObservableProperty]
    public partial AuxiliaryFunctionDescriptor? SelectedFunction
    {
        get;
        set;
    }

    /// <summary>Gets or sets the switch level.</summary>
    [ObservableProperty]
    public partial MavCmdDoAuxFunctionSwitchLevel Level
    {
        get;
        set;
    }

    /// <summary>Gets available switch levels.</summary>
    public IReadOnlyList<MavCmdDoAuxFunctionSwitchLevel> Levels
    {
        get;
    } = Enum.GetValues<MavCmdDoAuxFunctionSwitchLevel>();

    /// <summary>Gets or sets explicit safety confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmed
    {
        get;
        set;
    }

    /// <summary>Gets the latest operation result.</summary>
    [ObservableProperty]
    public partial string Result
    {
        get;
        private set;
    } = "Select an auxiliary function.";

    /// <summary>Builds the active-vehicle catalog.</summary>
    public void Refresh()
    {
        if (activeVehicle.State is not { } state)
        {
            return;
        }

        var functions = new List<AuxiliaryFunctionDescriptor>();
        foreach (var function in catalog.GetFunctions(state))
        {
            functions.Add(function);
        }

        Functions.ReplaceRange(functions);
        SelectedFunction ??= Functions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.State is not { } state || SelectedFunction is not { } function)
        {
            Result = "Select an online vehicle and function.";
            return;
        }

        Result = (await service.ExecuteAsync(new AuxiliaryFunctionRequest(state, function, Level, IsConfirmed), cancellationToken)).Summary;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DeactivateAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        Refresh();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        return Task.CompletedTask;
    }

    private void OnActiveVehicleChanged(object? sender, EventArgs e)
    {
        Refresh();
    }
}
