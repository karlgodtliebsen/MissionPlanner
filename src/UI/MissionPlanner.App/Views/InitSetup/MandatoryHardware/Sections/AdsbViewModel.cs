using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported ADS-B and avoidance parameters.</summary>
public sealed class AdsbViewModel : MandatoryParameterViewModel
{
    private readonly IAdsbService service;
    private readonly IVehicleParameterStreamService parameterStream;
    private readonly IPlannerSettingsService preferences;
    private readonly SemaphoreSlim favoriteGate = new(1, 1);

    /// <summary>Initializes the ADS-B workflow ViewModel.</summary>
    public AdsbViewModel(IActiveVehicleContext activeVehicle, IAdsbService service, ILogger<AdsbViewModel> logger, INavigationService navigation, IVehicleParameterStreamService parameterStream, IPlannerSettingsService preferences)
        : base(activeVehicle, logger, navigation)
    {
        this.service = service;
        this.parameterStream = parameterStream;
        this.preferences = preferences;
    }

    /// <inheritdoc />
    protected override bool IsFavorite(string name) => preferences.Current.AdsbFavorites?.Contains(name) == true;

    /// <inheritdoc />
    protected override async Task SaveFavoriteAsync(string name, bool favorite)
    {
        await favoriteGate.WaitAsync();
        try
        {
            var names = (preferences.Current.AdsbFavorites ?? []).ToHashSet(StringComparer.Ordinal);
            if (favorite)
            {
                names.Add(name);
            }
            else
            {
                names.Remove(name);
            }
            var result = await preferences.SaveAsync(preferences.Current with { AdsbFavorites = names.Order().ToArray() });
            if (!result.Success)
            {
                throw new InvalidOperationException("The favorite preference could not be saved.");
            }
        }
        finally
        {
            favoriteGate.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task ReloadParametersAsync(VehicleId vehicleId, CancellationToken token)
    {
        var result = await parameterStream.StreamAllParametersWithRetryAsync(vehicleId, cancellationToken: token);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Parameter download did not complete.");
        }
    }

    /// <inheritdoc />
    protected override Task<MandatoryParameterConfiguration> LoadConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return service.GetConfigurationAsync(vehicleId, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<MandatoryParameterApplyResult> ApplySettingAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken)
    {
        return service.ApplyAsync(vehicleId, name, value, cancellationToken);
    }
}

