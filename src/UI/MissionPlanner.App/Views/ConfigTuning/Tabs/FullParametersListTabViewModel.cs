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
    private readonly IDialogService dialogs;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly CancellationTokenSource cts;
    private CancellationTokenSource ctsProgress = new();

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    //private IDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>();
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
            LoadMetaDataAsync().FireAndForget();
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
            }
            catch (Exception)
            {
                //Noop
            }
        });
    }

    private async Task VehicleRegistered(VehicleConnected vehicle, CancellationToken cancellationToken)
    {
        await dispatcher.DispatchAsync(async () =>
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
    }

    private async Task LoadMetaDataAsync()
    {
        await SetLoadState();
        // Load metadata for the vehicle
        await dispatcher.DispatchAsync(() => ProgressMessage = "Loading parameter metadata...");

        metadata.Clear();
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle != null)
        {
            metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicle.Id, cts.Token));
            logger.LogInformation("Loaded metadata for {Count} parameters", metadata.Count);

            IList<ParameterItemViewModel> parameters = [];

            var parameterMetadata = metadata.Values.OrderBy(v => v.Name);
            foreach (var metaData in parameterMetadata)
            {
                var model = new ParameterItemViewModel(metaData);
                parameters.Add(model);
            }

            await ResetState();
            allParameterItems.Clear();
            allParameterItems.AddRange(parameters.OrderBy(p => p.Name));
            await dispatcher.DispatchAsync(() =>
            {
                Parameters.Clear();
                Parameters.AddRange(allParameterItems);
            });
        }
    }

    private async Task LoadAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        if (metadata.Count == 0)
        {
            logger.LogWarning("No metadata available for vehicle {VehicleId}", vehicleId);
            return;
        }

        logger.LogDebug("Starting to load parameters for vehicle {VehicleId}", vehicleId);

        IProgress<ParameterStreamProgress>? progress = new Progress<ParameterStreamProgress>(p =>
            dispatcher.DispatchAsync(() =>
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
                await ResetState();
                await dispatcher.DispatchAsync(() =>
                {
                    ShowLoadingPanel = true;
                    ShowLoadingCompletedWithError = true;
                });
                logger.LogError("Failed to load parameters: {Error}", result.ErrorMessage);
                return;
            }

            var parameters = new Dictionary<string, VehicleParameter>(result.Parameters);

            foreach (var parameter in parameters.Values.OrderBy(p => p.Name))
            {
                var item = allParameterItems.FirstOrDefault(m => m.Name == parameter.Name);
                if (item is not null)
                {
                    item.SetData(parameter);
                }
                else
                {
                    var vehicleParameter = new VehicleParameter(parameter.Name ?? "", 0, MavParamType.Real32, 0, (ushort)metadata.Count());
                    var model = new ParameterItemViewModel(vehicleParameter);
                    allParameterItems.Add(model);
                }
            }

            await dispatcher.DispatchAsync(() =>
            {
                TotalParameterCount = allParameterItems.Count;
                ModifiedParameterCount = 0;
                if (allParameterItems.Count != Parameters.Count)
                {
                    Parameters.Clear();
                    Parameters.AddRange(allParameterItems.OrderBy(p => p.Name));
                }
            });

            await ResetState();
            logger.LogInformation("Successfully loaded {Count} parameters with metadata", parameters.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading parameters");
            await ResetState();
            await dispatcher.DispatchAsync(async () =>
            {
                Parameters.Clear();
                await dialogs.DisplayTextPromptAsync("Load failed. Ensure there is a connection and try again", ex.Message, "OK");
            });
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
            await SetLoadState();
            await Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    [RelayCommand]
    private async Task CreateTestParametersAsync()
    {
        await SetLoadState();
        IList<ParameterItemViewModel> testParameters = [];
        for (var i = 0; i < 20; i++)
        {
            var vehicleParameter = new VehicleParameter($"VEHICLE_NAME {i}", i, MavParamType.Real32, (ushort)i, 100);
            var model = new ParameterItemViewModel(vehicleParameter);
            testParameters.Add(model);

            await dispatcher.DispatchAsync(() =>
            {
                Progress += 0.05;
            });

            await Task.Delay(10);
        }

        await dispatcher.DispatchAsync(() =>
        {
            Parameters.Clear();
            Parameters.AddRange(testParameters);
        });
        await ResetState();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task CreateTestParametersWithMetadata()
    {
        await SetLoadState();
        IList<ParameterItemViewModel> testParameters = [];

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            if (metadata.Count == 0)
            {
                metadata.AddAll(await metadataService.GetAllMetadataAsync(vehicle.Id, cts.Token));
            }

            var vehicleParameter = new VehicleParameter("First Line", (float)1.0, MavParamType.Real32, (ushort)testParameters.Count, (ushort)metadata.Count());
            var model = new ParameterItemViewModel(vehicleParameter);
            testParameters.Add(model);


            var parameterMetadata = metadata.Values.OrderBy(v => v.Name);
            foreach (var metaData in parameterMetadata)
            {
                vehicleParameter = new VehicleParameter(metaData.Name ?? "", (float)1.0, MavParamType.Real32, (ushort)testParameters.Count, (ushort)metadata.Count());
                model = new ParameterItemViewModel(vehicleParameter);
                model.SetMetadata(metaData);
                testParameters.Add(model);
            }
        }

        await ResetState();

        await dispatcher.DispatchAsync(() =>
        {
            Parameters.Clear();
            Parameters.AddRange(testParameters);
        });
    }


    [RelayCommand(CanExecute = nameof(CanExecuteConnection))]
    private async Task CancelLoad()
    {
        await ctsProgress.CancelAsync();
        ctsProgress = new CancellationTokenSource();
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

            await dispatcher.DispatchAsync(() => Parameters.Clear());
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

            await dispatcher.DispatchAsync(() =>
            {
                Parameters.Clear();
                Parameters.AddRange(loadedParameters);
            });
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
            var parameters = Parameters.Where(v => v.OriginalParameter is not null).Select(v => v.OriginalParameter).ToList();
            var result = await parametersFileHandler.SaveParametersToFile(parameters!, cts.Token);
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
    private async Task ResetToDefault()
    {
        await ResetState();
        Parameters.Clear();
    }

    private async Task ResetState()
    {
        await dispatcher.DispatchAsync(() =>
        {
            Progress = 0;
            ProgressMessage = "";
            IsBusy = false;
            ShowLoadingProgress = false;
            ShowLoadingPanel = false;
            ShowLoadingCompletedWithError = false;
            ShowLoadingCancelled = false;
            ShowVehicleDisconnected = false;
        });
    }

    private async Task SetLoadState()
    {
        await ResetState();
        await dispatcher.DispatchAsync(() =>
        {
            IsBusy = true;
            ShowLoadingPanel = true;
            ShowLoadingProgress = true;
            ProgressMessage = "Loading parameters...";
            //Parameters.Clear();
            //allParameterItems.Clear();
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

        eventSubscriptions.Clear();
    }
}
