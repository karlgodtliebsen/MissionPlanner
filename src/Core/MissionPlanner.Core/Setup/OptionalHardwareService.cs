using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Aggregates registered optional-hardware modules and filters by parameter presence.</summary>
public sealed class OptionalHardwareCatalog : IOptionalHardwareCatalog
{
    /// <summary>Initializes the catalog from the registered modules.</summary>
    /// <param name="modules">The registered optional-hardware modules.</param>
    public OptionalHardwareCatalog(IEnumerable<IOptionalHardwareModule> modules) =>
        Modules = modules.OrderBy(module => module.Title, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<IOptionalHardwareModule> Modules { get; }

    /// <inheritdoc />
    public IReadOnlyList<IOptionalHardwareModule> GetAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        Modules.Where(module => module.IsAvailable(parameters)).ToArray();
}

/// <summary>Discovers available optional-hardware modules and applies guarded, readback-confirmed edits.</summary>
public sealed class OptionalHardwareService : IOptionalHardwareService
{
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareCatalog catalog;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly ILogger<OptionalHardwareService> logger;

    /// <summary>Initializes the optional-hardware service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="catalog">The optional-hardware module catalog.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="logger">The logger.</param>
    public OptionalHardwareService(
        IActiveVehicleContext activeVehicle,
        IOptionalHardwareCatalog catalog,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        ILogger<OptionalHardwareService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.catalog = catalog;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OptionalHardwareModuleView>> GetModulesAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        return catalog.GetAvailable(parameters).Select(module => module.Build(parameters, metadata)).ToArray();
    }

    /// <inheritdoc />
    public async Task<OptionalHardwareApplyResult> SetValueAsync(VehicleId vehicleId, string parameterName, double value, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var modules = await GetModulesAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var setting = modules.SelectMany(module => module.Settings).FirstOrDefault(item => item.Name == parameterName);
        if (setting is null)
        {
            return new OptionalHardwareApplyResult(false, $"{parameterName} is not an editable setting of an available module.");
        }

        // Never log the value of a secret setting.
        logger.LogInformation("Applying optional-hardware parameter {Parameter} on {VehicleId}.", parameterName, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, setting, value, cancellationToken).ConfigureAwait(false))
        {
            return new OptionalHardwareApplyResult(true, $"Confirmed {parameterName} by vehicle readback.", setting.RebootRequired);
        }

        return new OptionalHardwareApplyResult(false, $"Readback did not confirm {parameterName}. Reconnect, refresh, and verify before flying.");
    }

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var modules = await GetModulesAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        foreach (var name in modules.SelectMany(module => module.Settings).Select(setting => setting.Name).Distinct(StringComparer.Ordinal))
        {
            if (parameterRegistry.GetParameter(vehicleId, name) is not null)
            {
                await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        return state;
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, PeripheralSetting setting, double value, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == setting.Name && NearlyEqual(parameter.Value, value))
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, setting.Name, (float)value, setting.ParameterType, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, setting.Name) is { } current && NearlyEqual(current.Value, value))
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, setting.Name, cancellationToken).ConfigureAwait(false);
            await readback.Task.WaitAsync(readbackTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }

    private static bool NearlyEqual(double first, double second) => Math.Abs(first - second) <= 0.0005;
}
