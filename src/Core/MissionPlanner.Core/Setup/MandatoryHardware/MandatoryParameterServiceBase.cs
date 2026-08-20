using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Reuses the shared vehicle parameter infrastructure for parameter-backed setup workflows.</summary>
public abstract class MandatoryParameterServiceBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;

    /// <summary>Initializes the shared parameter workflow implementation.</summary>
    protected MandatoryParameterServiceBase(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
    }

    /// <summary>Builds settings for reported parameter names matching the supplied predicate.</summary>
    protected async Task<IReadOnlyList<PeripheralSetting>> LoadSettingsAsync(
        VehicleId vehicleId,
        Func<string, bool> include,
        CancellationToken cancellationToken)
    {
        RequireActive(vehicleId);
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        return parameters.Keys
            .Where(include)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .Where(setting => setting is not null)
            .Cast<PeripheralSetting>()
            .ToArray();
    }

    /// <summary>Validates and writes a reported parameter using its existing wire type.</summary>
    protected async Task<MandatoryParameterApplyResult> ApplyAsync(
        VehicleId vehicleId,
        string name,
        double value,
        Func<string, double, string?> validate,
        CancellationToken cancellationToken)
    {
        RequireActive(vehicleId);
        if (validate(name, value) is { } error)
        {
            return new MandatoryParameterApplyResult(false, error);
        }

        if (parameterRegistry.GetParameter(vehicleId, name) is not { } parameter)
        {
            return new MandatoryParameterApplyResult(false, $"{name} is not reported by the connected vehicle.");
        }

        var success = await parameterService.SetParameterAsync(
            vehicleId,
            name,
            (float)value,
            parameter.Type,
            cancellationToken).ConfigureAwait(false);
        return success
            ? new MandatoryParameterApplyResult(true, $"{name} was accepted by the vehicle.")
            : new MandatoryParameterApplyResult(false, $"The vehicle rejected {name}.");
    }

    private void RequireActive(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }
    }
}
