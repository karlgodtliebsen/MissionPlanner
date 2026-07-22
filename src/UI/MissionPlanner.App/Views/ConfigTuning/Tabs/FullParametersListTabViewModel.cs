using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNext.Collections.Generic;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Dialogs;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the full list of parameters for a vehicle.
/// </summary>
public partial class FullParametersListTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleConnectionSession session;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDispatcher dispatcher;
    private readonly IDialogService dialogService;
    private readonly IExtendedDialogService extendedDialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly CancellationTokenSource cts;
    private CancellationTokenSource ctsProgress = new();

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    private readonly IDictionary<string, ParameterMetadata> metadata = new Dictionary<string, ParameterMetadata>();
    private readonly List<ParameterItemViewModel> allParameterItems = [];
    private IDisposable? progressDialog;

    /// <summary>
    /// Gets the collection of vehicle parameters.
    /// </summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; set; } = [];

    [ObservableProperty] public partial string ProgressMessage { get; set; } = null!;
    [ObservableProperty] public partial bool ShowDataGrid { get; set; }

    [ObservableProperty] public partial bool ShowEmptyView { get; set; }

    [ObservableProperty] public partial bool ShowLoadingProgress { get; set; }
    [ObservableProperty] public partial bool ShowLoadingCompletedWithError { get; set; }
    [ObservableProperty] public partial bool ShowLoadingCancelled { get; set; }
    [ObservableProperty] public partial bool ShowVehicleDisconnected { get; set; }
    [ObservableProperty] public partial int ModifiedParameterCount { get; set; }
    [ObservableProperty] public partial int TotalParameterCount { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [ObservableProperty]
    public partial bool HasConnection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabViewModel"/> class.
    /// </summary>
    /// <param name="session">The vehicle connection session.</param>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="cts">The cancellation token source.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="extendedDialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The service factory.</param>
    /// <param name="parametersFileHandler">The parameters file handler.</param>
    /// <param name="metadataService">The vehicle parameter metadata service.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession session,
        IVehicleRegistry vehicleRegistry,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        IDialogService dialogService,
        IExtendedDialogService extendedDialogService,
        IDomainFactory domainFactory,
        ParametersFileHandler parametersFileHandler,
        IVehicleParameterMetadataService metadataService,
        CancellationTokenSource cts,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.session = session;
        this.vehicleRegistry = vehicleRegistry;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.extendedDialogService = extendedDialogService;
        this.domainFactory = domainFactory;
        this.parametersFileHandler = parametersFileHandler;
        this.metadataService = metadataService;
        this.cts = cts;
        this.logger = logger;
        eventSubscriptions.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(VehicleConnected));
        eventSubscriptions.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(VehicleDisconnected));
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        HasConnection = vehicle != null;
        if (HasConnection)
        {
            Task.Run(LoadMetaDataAsync).FireAndForget();
        }
    }

    private async Task VehicleDisconnected(VehicleDisconnected vehicle, CancellationToken cancellationToken)
    {
        await ResetState();
        await dispatcher.DispatchAsync(() =>
        {
            try
            {
                HasConnection = false;
                metadata.Clear();
                allParameterItems.Clear();
            }
            catch (Exception)
            {
                //Noop
            }
        });
    }

    private async Task VehicleConnected(VehicleConnected vehicle, CancellationToken cancellationToken)
    {
        await dispatcher.DispatchAsync(async () =>
        {
            try
            {
                HasConnection = true;
            }
            catch (Exception)
            {
                //Noop
            }
        });

        await Task.Run(LoadMetaDataAsync, cancellationToken);
    }

    private async Task LoadMetaDataAsync()
    {
        metadata.Clear();
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle != null)
        {
            metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicle.Id, cts.Token));
            logger.LogInformation("Loaded metadata for {Count} parameters", metadata.Count);
        }
    }

    private async Task LoadAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        await ResetCancellationToken();
        if (metadata.Count == 0)
        {
            allParameterItems.Clear();
            logger.LogWarning("No metadata available for vehicle {VehicleId}", vehicleId);
            metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicleId, cts.Token));
            logger.LogInformation("Loaded metadata for {Count} parameters", metadata.Count);
        }

        logger.LogDebug("Starting to load parameters for vehicle {VehicleId}", vehicleId);

        IProgress<ParameterStreamProgress>? progress = new Progress<ParameterStreamProgress>(p =>
            dispatcher.DispatchAsync(() =>
            {
                ShowDataGrid = false;
                ProgressMessage = p.TotalCount > 0 ? $"Processing parameters... {p.ReceivedCount}/{p.TotalCount}" : $"Processing parameters...";
            }));

        progressDialog = await extendedDialogService.DisplayProgressCancellableAsync("Handling parameters", () => ProgressMessage, tokenSource: ctsProgress);

        try
        {
            // Stream all parameters with progress tracking
            var vehicleParameterStreamService = session.ParameterStreamService;
            var result = await vehicleParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: ctsProgress.Token);

            if (!result.Success)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    ShowDataGrid = true;
                    NullState();
                    ShowEmptyView = Parameters.Count == 0;
                    ShowLoadingCompletedWithError = true;
                });
                logger.LogError("Failed to load parameters: {Error}", result.ErrorMessage);
                await ResetCancellationToken();
                return;
            }

            await Task.Run(() => PrepareParameters(result.Parameters), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading parameters");
            allParameterItems.Clear();
            await dispatcher.DispatchAsync(async () =>
            {
                ShowDataGrid = true;
                NullState();
                Parameters.Clear();
                ShowEmptyView = Parameters.Count == 0;
                var errorModel = domainFactory.Create<ErrorViewModel, string>(ex.Message + "\nEnsure there is a connection and try again");
                var view = domainFactory.Create<ErrorView, ErrorViewModel>(errorModel);
                await dialogService.DisplayViewAsync("Load failed.", view, "OK");
            });
        }
    }

    private async Task PrepareParameters(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        await dispatcher.DispatchAsync(() =>
        {
            ShowDataGrid = false;
            ProgressMessage = $"Initializing data grid";
        });
        await Task.Delay(100); // Add a small delay to allow UI to update
        await Task.Run(() => AddParameters(parameters), ctsProgress.Token);
    }

    private async Task AddParameters(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        allParameterItems.Clear();
        foreach (var parameter in parameters.Values.OrderBy(p => p.Name))
        {
            if (metadata.TryGetValue(parameter.Name, out var md))
            {
                var model = new ParameterItemViewModel(md);
                model.SetData(parameter);
                allParameterItems.Add(model);
            }
            else
            {
                var vehicleParameter = new VehicleParameter(parameter.Name, parameter.Value, MavParamType.Real32, 0, (ushort)metadata.Count());
                var model = new ParameterItemViewModel(vehicleParameter);
                allParameterItems.Add(model);
            }
        }

        await Task.Run(RenderParameters, ctsProgress.Token);
    }

    private async Task RenderParameters()
    {
        await dispatcher.DispatchAsync(() =>
        {
            ProgressMessage = $"Populating data grid";
            TotalParameterCount = allParameterItems.Count;
            ModifiedParameterCount = 0;
            Parameters.Clear();
            ShowEmptyView = allParameterItems.Count == 0;
        });
        await Task.Delay(100); // Add a small delay to allow UI to update
        await dispatcher.DispatchAsync(() =>
        {
            Parameters.AddRange(allParameterItems.OrderBy(p => p.Name));
            NullState();
            ShowDataGrid = true;
            logger.LogInformation("Successfully loaded {Count} parameters with metadata", TotalParameterCount);
        });

        await ResetCancellationToken();
    }


    private bool CanExecuteConnection()
    {
        return HasConnection;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task RefreshParameters()
    {
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            await SetLoadState();
            await Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }


    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task CancelLoad()
    {
        await ResetCancellationToken();
        await ResetState();
    }

    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        try
        {
            var loadedParameters = await parametersFileHandler.LoadParametersFromFileAsync(
                Parameters.Select(v => v.OriginalParameter).ToList(), cts.Token);
            //TODO: create a method to update existing parameters with loaded values instead of clearing and replacing

            await dispatcher.DispatchAsync(() =>
            {
                Parameters.Clear();
                ShowEmptyView = Parameters.Count == 0;
            });
        }
        catch (Exception ex)
        {
            await dialogService.ConfirmAsync("Load failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task LoadFromJsonFileAsync()
    {
        try
        {
            var loadedParameters = await parametersFileHandler.LoadParametersFromJsonFileAsync(cts.Token);

            await dispatcher.DispatchAsync(() =>
            {
                Parameters.Clear();
                Parameters.AddRange(loadedParameters);
            });
        }
        catch (Exception ex)
        {
            await dialogService.ConfirmAsync("Load failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task SaveToFileAsync()
    {
        try
        {
            var parameters = Parameters.Where(v => v.OriginalParameter is not null).Select(v => v.OriginalParameter).ToList();
            var result = await parametersFileHandler.SaveParametersToFile(parameters!, cts.Token);
            await dialogService.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception ex)
        {
            await dialogService.ConfirmAsync("Save failed", ex.Message, "OK");
        }
    }


    [RelayCommand]
    private async Task SaveToJsonFileAsync()
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToJsonFile(Parameters, cts.Token);
            await dialogService.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception ex)
        {
            await dialogService.ConfirmAsync("Save failed", ex.Message, "OK");
        }
    }


    [RelayCommand]
    private void WriteParameters()
    {
    }

    [RelayCommand]
    private void CompareParameters()
    {
    }

    [RelayCommand]
    private void LoadPreSaved()
    {
    }

    [RelayCommand]
    private async Task ResetToDefault()
    {
        await ResetState();
        Parameters.Clear();
    }

    private async Task ResetState()
    {
        await dispatcher.DispatchAsync(NullState);
    }

    private async Task ResetCancellationToken()
    {
        await ctsProgress.CancelAsync();
        ctsProgress.Dispose();
        ctsProgress = new CancellationTokenSource();
    }

    private void NullState()
    {
        progressDialog?.Dispose();
        progressDialog = null;
        ProgressMessage = "";
        IsBusy = false;
        ShowLoadingProgress = false;
        ShowLoadingCompletedWithError = false;
        ShowLoadingCancelled = false;
        ShowVehicleDisconnected = false;
        ShowEmptyView = Parameters.Count == 0;
    }

    private async Task SetLoadState()
    {
        await dispatcher.DispatchAsync(() =>
        {
            NullState();
            IsBusy = true;
            ShowLoadingProgress = true;
            ProgressMessage = "Loading parameters...";
            ShowEmptyView = Parameters.Count == 0;
        });
    }

    /// <summary>
    /// Filters parameters based on search text.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        FilterParameters().FireAndForget();
    }


    private async Task FilterParameters()
    {
        await dispatcher.DispatchAsync(() =>
        {
            Parameters.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Show all parameters
                foreach (var item in allParameterItems)
                {
                    Parameters.Add(item);
                }
            }
            else
            {
                // Filter by name or description
                var searchLower = SearchText.ToLower();
                foreach (var item in allParameterItems.Where(p =>
                             p.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                             (p.Description?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false)))
                {
                    Parameters.Add(item);
                }
            }

            // Update modified count
            // ModifiedParameterCount = allParameterItems.Count(p => p.IsModified);
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in eventSubscriptions)
        {
            disposable.Dispose();
        }

        Parameters.Clear();
        eventSubscriptions.Clear();
    }
}
