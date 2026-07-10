using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.ViewModels;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the full list of parameters for a vehicle.
/// </summary>
public partial class FullParametersListTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleConnectionSession session;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDispatcher dispatcher;
    private readonly CancellationTokenSource cts;
    private CancellationTokenSource ctsProgress = new();

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    private IDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>();
    private readonly List<ParameterItemViewModel> allParameterItems = [];

    /// <summary>
    /// Gets the collection of vehicle parameters.
    /// </summary>
    public ObservableCollection<ParameterItemViewModel> Parameters { get; set; } = [];

    [ObservableProperty] public partial string ProgressMessage { get; set; } = null!;

    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool ShowLoadingPanel { get; set; }

    [ObservableProperty] public partial bool ShowLoadingProgress { get; set; }

    //[ObservableProperty] public partial bool ShowLoadingCompleted { get; set; }
    //[ObservableProperty] public partial bool ShowRendering { get; set; }
    //[ObservableProperty] public partial bool ShowData { get; set; }

    [ObservableProperty] public partial bool ShowLoadingCompletedWithError { get; set; }
    [ObservableProperty] public partial bool ShowLoadingCancelled { get; set; }

    [ObservableProperty] public partial bool ShowVehicleDisconnected { get; set; }
    [ObservableProperty] public partial int ModifiedParameterCount { get; set; }
    [ObservableProperty] public partial int TotalParameterCount { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabViewModel"/> class.
    /// </summary>
    /// <param name="session">The vehicle connection session.</param>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="cts">The cancellation token source.</param>
    /// <param name="logger">The logger.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession session,
        IVehicleRegistry vehicleRegistry,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        CancellationTokenSource cts,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.session = session;
        this.vehicleRegistry = vehicleRegistry;
        this.dispatcher = dispatcher;
        this.cts = cts;
        this.logger = logger;
        var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(VehicleRegistered);
        eventSubscriptions.Add(eventSubscription);

        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(VehicleDisconnected);
        eventSubscriptions.Add(eventSubscription);

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        //if (vehicle is not null)
        //{
        //  PrepareLoad();
        //    Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        //}
    }

    //private async Task VehicleParameterReceived(VehicleParameterReceived message, CancellationToken cancellationToken)
    //{
    //    await Task.Run(async () => await LoadAsync(message.VehicleId, cancellationToken), cancellationToken);
    //}

    private Task VehicleDisconnected(VehicleDisconnected vehicle, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(() =>
        {
            try
            {
                ResetUIState();
                Parameters.Clear();
                Progress = 0;
            }
            catch (Exception)
            {
                //Noop
            }
        });
        return Task.CompletedTask;
    }

    private async Task VehicleRegistered(VehicleConnected vehicle, CancellationToken cancellationToken)
    {
        PrepareLoad();
        await Task.Run(async () => await LoadAsync(vehicle.VehicleId, cancellationToken), cancellationToken);
    }

    [RelayCommand]
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
    private async Task CancelLoad()
    {
        await ctsProgress.CancelAsync();
        ctsProgress = new CancellationTokenSource();
        ResetUIState();
    }

    [RelayCommand]
    private void LoadFromFile()
    {
        //ParametersFileHandler
    }


    [RelayCommand]
    private void SaveToFile()
    {
        //ParametersFileHandler
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

            // TODO: Load metadata for the vehicle when metadata service is available
            // ProgressMessage = "Loading parameter metadata...";
            // var metadataService = session.ParameterMetadataService;
            // var metadata = await metadataService.GetParameterMetadataAsync(vehicleId, cancellationToken);

            // Create ParameterItemViewModel instances
            foreach (var parameter in parameters.Values.OrderBy(p => p.Name))
            {
                var item = new ParameterItemViewModel(parameter);

                // TODO: Set metadata if available
                // if (metadata != null && metadata.TryGetValue(parameter.Name, out var paramMetadata))
                // {
                //     item.SetMetadata(paramMetadata);
                // }

                allParameterItems.Add(item);
            }

            // Display all parameters initially
            dispatcher.Dispatch(() =>
            {
                TotalParameterCount = allParameterItems.Count;
                // Update counts
                ModifiedParameterCount = 0; // Initially no modifications
                Parameters.Clear();
                //Parameters = new ObservableCollection<ParameterItemViewModel>(allParameterItems);
                //Parameters.CopyTo(allParameterItems.ToArray(), 0);

                foreach (var item in allParameterItems)
                {
                    Parameters.Add(item);
                }
            });


            ResetUIState();
            //dispatcher.Dispatch(() => ShowLoadingPanel = false);
            logger.LogInformation("Successfully loaded {Count} parameters with metadata", parameters.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading parameters");
            ResetUIState();
            dispatcher.Dispatch(() => Parameters.Clear());
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
            ModifiedParameterCount = allParameterItems.Count(p => p.IsModified);
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
