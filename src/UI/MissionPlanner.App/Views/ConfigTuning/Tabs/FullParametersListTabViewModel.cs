using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNext.Collections.Generic;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.ViewModels;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Dialogs;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the full list of parameters for a vehicle.
/// </summary>
public partial class FullParametersListTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleConnectionSession session;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDispatcher dispatcher;
    private readonly IDialogService dialogs;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly CancellationTokenSource cts;
    private CancellationTokenSource ctsProgress = new();

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    private IDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>();
    private readonly IDictionary<string, ParameterMetadata> metadata = new Dictionary<string, ParameterMetadata>();
    private readonly List<ParameterItemViewModel> allParameterItems = [];

    /// <summary>
    /// Gets the collection of vehicle parameters.
    /// </summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; set; } = [];

    [ObservableProperty] public partial string ProgressMessage { get; set; } = null!;

    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool ShowLoadingPanel { get; set; }

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
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="parametersFileHandler">The parameters file handler.</param>
    /// <param name="metadataService">The vehicle parameter metadata service.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession session,
        IVehicleRegistry vehicleRegistry,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        IDialogService dialogs,
        ParametersFileHandler parametersFileHandler,
        IVehicleParameterMetadataService metadataService,
        CancellationTokenSource cts,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.session = session;
        this.vehicleRegistry = vehicleRegistry;
        this.dispatcher = dispatcher;
        this.dialogs = dialogs;
        this.parametersFileHandler = parametersFileHandler;
        this.metadataService = metadataService;
        this.cts = cts;
        this.logger = logger;
        var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(VehicleRegistered);
        eventSubscriptions.Add(eventSubscription);

        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(VehicleDisconnected);
        eventSubscriptions.Add(eventSubscription);

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        HasConnection = vehicle != null;
        if (HasConnection)
        {
            _ = LoadMetaDataAsync();
        }
    }

    private Task VehicleDisconnected(VehicleDisconnected vehicle, CancellationToken cancellationToken)
    {
        ResetUIState();
        dispatcher.Dispatch(() =>
        {
            try
            {
                HasConnection = false;
                metadata.Clear();
            }
            catch (Exception)
            {
                //Noop
            }
        });
        return Task.CompletedTask;
    }

    private Task VehicleRegistered(VehicleConnected vehicle, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(async () =>
        {
            try
            {
                HasConnection = true;
                await LoadMetaDataAsync();
            }
            catch (Exception)
            {
                //Noop
            }
        });
        return Task.CompletedTask;
    }

    private async Task LoadMetaDataAsync()
    {
        metadata.Clear();
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle != null)
        {
            metadata.Clear();
            metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicle.Id, cts.Token));
            logger.LogInformation("Loaded metadata for {Count} parameters", metadata.Count);
        }
    }

    private bool CanExecuteConnection()
    {
        return HasConnection;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task LoadMetaData()
    {
        await LoadMetaDataAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task RefreshParameters()
    {
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            PrepareLoad();
            await Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    [RelayCommand]
    private async Task CreateTestParametersAsync()
    {
        PrepareLoad();
        IList<ParameterItemViewModel> testParameters = [];
        for (var i = 0; i < 20; i++)
        {
            var vehicleParameter = new VehicleParameter($"VEHICLE_NAME {i}", i, MavParamType.Real32, (ushort)i, 100);
            var model = new ParameterItemViewModel(vehicleParameter) { Description = $"Vehicle Name Parameter {i}", Units = "N/A", Options = "1,2,3,4,5" };
            testParameters.Add(model);
            Progress += 0.05;
            await Task.Delay(10);
        }


        dispatcher.Dispatch(() =>
        {
            Parameters.Clear();
            Parameters.AddRange(testParameters);
        });
        ResetUIState();
    }

    [RelayCommand]
    //    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task CreateTestParametersWithMetadata()
    {
        PrepareLoad();
        IList<ParameterItemViewModel> testParameters = [];

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            if (metadata.Count == 0)
            {
                metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicle.Id, cts.Token));
            }

            var parameterMetadata = metadata.Values.OrderBy(v => v.Name);
            foreach (var metaData in parameterMetadata)
            {
                var vehicleParameter = new VehicleParameter(metaData.Name ?? "", (float)1.0, MavParamType.Real32, (ushort)testParameters.Count, (ushort)metadata.Count());
                var model = new ParameterItemViewModel(vehicleParameter) { Description = metaData.Description ?? "", Units = metaData.Units ?? "", Options = "5,4,3,2,1" };
                testParameters.Add(model);
            }
        }

        dispatcher.Dispatch(() =>
        {
            Parameters.Clear();
            Parameters.AddRange(testParameters);
        });

        ResetUIState();
    }


    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task CancelLoad()
    {
        await ctsProgress.CancelAsync();
        ctsProgress = new CancellationTokenSource();
        ResetUIState();
    }

    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        try
        {
            var loadedParameters = await parametersFileHandler.LoadParametersFromFileAsync(
                Parameters.Select(v => v.OriginalParameter).ToList(), cts.Token);
            //TODO: create a method to update existing parameters with loaded values instead of clearing and replacing
            Parameters.Clear();
            // Parameters.AddRange(loadedParameters);
        }
        catch (Exception ex)
        {
            await dialogs.ConfirmAsync("Load failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task LoadFromJsonFileAsync()
    {
        try
        {
            var loadedParameters = await parametersFileHandler.LoadParametersFromJsonFileAsync(cts.Token);
            Parameters.Clear();
            Parameters.AddRange(loadedParameters);
        }
        catch (Exception ex)
        {
            await dialogs.ConfirmAsync("Load failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task SaveToFileAsync()
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToFile(Parameters.Select(v => v.OriginalParameter).ToList(), cts.Token);
            await dialogs.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception ex)
        {
            await dialogs.ConfirmAsync("Save failed", ex.Message, "OK");
        }
    }


    [RelayCommand]
    private async Task SaveToJsonFileAsync()
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToJsonFile(Parameters, cts.Token);
            await dialogs.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception ex)
        {
            await dialogs.ConfirmAsync("Save failed", ex.Message, "OK");
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
    private void ResetToDefault()
    {
        ResetUIState();
        Parameters.Clear();
    }

    private void ResetUIState()
    {
        dispatcher.Dispatch(() =>
        {
            Progress = 0;
            IsBusy = false;
            ShowLoadingProgress = false;
            ShowLoadingPanel = false;
            ShowLoadingCompletedWithError = false;
            ShowLoadingCancelled = false;
            ShowVehicleDisconnected = false;
        });
    }

    private void PrepareLoad()
    {
        ResetUIState();
        dispatcher.Dispatch(() =>
        {
            IsBusy = true;
            ShowLoadingPanel = true;
            ShowLoadingProgress = true;
            ProgressMessage = "Loading parameters...";
            Parameters.Clear();
            parameters.Clear();
            allParameterItems.Clear();
        });
    }

    private async Task LoadAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting to load parameters for vehicle {VehicleId}", vehicleId);

        IProgress<ParameterStreamProgress>? progress = new Progress<ParameterStreamProgress>(p =>
            dispatcher.Dispatch(() =>
            {
                Progress = (double)p.ReceivedCount / p.TotalCount;
                ProgressMessage = $"Loading parameters... {p.ReceivedCount}/{p.TotalCount}";
            }));

        try
        {
            // Stream all parameters with progress tracking
            var vehicleParameterStreamService = session.ParameterStreamService;
            var result = await vehicleParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: ctsProgress.Token);

            if (!result.Success)
            {
                ResetUIState();
                dispatcher.Dispatch(() =>
                {
                    ShowLoadingPanel = true;
                    ShowLoadingCompletedWithError = true;
                });
                logger.LogError("Failed to load parameters: {Error}", result.ErrorMessage);
                return;
            }

            //dispatcher.Dispatch(() => ShowLoadingProgress = false);

            parameters = new Dictionary<string, VehicleParameter>(result.Parameters);

            // Load metadata for the vehicle
            dispatcher.Dispatch(() => ProgressMessage = "Loading parameter metadata...");
            await LoadMetaDataAsync();

            // Create ParameterItemViewModel instances
            foreach (var parameter in parameters.Values.OrderBy(p => p.Name))
            {
                var item = new ParameterItemViewModel(parameter);

                // Set metadata if available
                if (metadata != null && metadata.TryGetValue(parameter.Name, out var paramMetadata))
                {
                    item.SetMetadata(paramMetadata);
                }

                allParameterItems.Add(item);
            }

            // Display all parameters initially
            dispatcher.Dispatch(() =>
            {
                TotalParameterCount = allParameterItems.Count;
                ModifiedParameterCount = 0;
                Parameters.Clear();
                Parameters.AddRange(allParameterItems);
            });

            ResetUIState();
            //dispatcher.Dispatch(() => ShowLoadingPanel = false);
            logger.LogInformation("Successfully loaded {Count} parameters with metadata", parameters.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading parameters");
            ResetUIState();
            dispatcher.Dispatch(async () =>
            {
                Parameters.Clear();
                await dialogs.DisplayTextPromptAsync("Load failed. Ensure there is a connection and try again", ex.Message, "OK");
            });
        }
    }

    /// <summary>
    /// Filters parameters based on search text.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        FilterParameters();
    }

    private void FilterParameters()
    {
        dispatcher.Dispatch(() =>
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

        eventSubscriptions.Clear();
    }
}
