using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Uses live parameters and firmware metadata to provide guarded frame setup.</summary>
public sealed class FrameConfigurationService : IFrameConfigurationService
{
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private static readonly IReadOnlyDictionary<FirmwareFamily, string[]> frameParameters =
        new Dictionary<FirmwareFamily, string[]>
        {
            [FirmwareFamily.ArduCopter] = ["FRAME_CLASS", "FRAME_TYPE"],
            [FirmwareFamily.ArduPlane] = ["Q_FRAME_CLASS", "Q_FRAME_TYPE"],
            [FirmwareFamily.Rover] = ["FRAME_CLASS", "FRAME_TYPE"]
        };
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly ILogger<FrameConfigurationService> logger;

    /// <summary>Initializes the frame-configuration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="logger">The logger.</param>
    public FrameConfigurationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        ILogger<FrameConfigurationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<FrameConfigurationSnapshot> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var values = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var settings = new List<FrameParameterSetting>();

        if (frameParameters.TryGetValue(state.Identity.Firmware.Family, out var names))
        {
            foreach (var name in names)
            {
                if (!values.TryGetValue(name, out var parameter) ||
                    !metadata.TryGetValue(name, out var definition) || definition.ReadOnly)
                {
                    continue;
                }

                var options = definition.GetValueOptions()
                    .OrderBy(option => option.Key)
                    .Select(option => new FrameParameterOption(option.Key, option.Value))
                    .ToArray();
                if (options.Length == 0)
                {
                    continue;
                }

                settings.Add(new FrameParameterSetting(
                    name,
                    definition.DisplayName ?? name,
                    parameter.Value,
                    parameter.Type,
                    definition.RebootRequired,
                    options));
            }
        }

        var recommendations = CreateRecommendations(values, metadata);
        return new FrameConfigurationSnapshot(vehicleId, state.Identity.Firmware.Family, settings, recommendations);
    }

    /// <inheritdoc />
    public async Task<FrameConfigurationApplyResult> ApplyAsync(
        VehicleId vehicleId,
        IReadOnlyList<FrameParameterChange> changes,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        if (changes.Count == 0)
        {
            return new FrameConfigurationApplyResult(FrameConfigurationApplyStatus.NoChanges, "No reviewed changes were selected.", [], [], false);
        }

        var configuration = await GetConfigurationAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var allowedSettings = configuration.Settings.ToDictionary(setting => setting.Name, StringComparer.Ordinal);
        var allowedRecommendations = configuration.Recommendations.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (!IsAllowed(change, allowedSettings, allowedRecommendations))
            {
                return new FrameConfigurationApplyResult(
                    FrameConfigurationApplyStatus.Failed,
                    $"{change.Name} is not an available metadata-backed choice for this vehicle.", [], [], false);
            }
        }

        var confirmed = new List<FrameParameterChange>();
        try
        {
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation("Applying reviewed frame setup parameter {Parameter} to {VehicleId}.", change.Name, vehicleId);
                if (!await WriteAndConfirmAsync(vehicleId, change.Name, change.PendingValue, change.ParameterType, cancellationToken).ConfigureAwait(false))
                {
                    return await RollBackAsync(vehicleId, confirmed, $"Readback did not confirm {change.Name}.").ConfigureAwait(false);
                }

                confirmed.Add(change);
            }
        }
        catch (OperationCanceledException)
        {
            if (confirmed.Count == 0)
            {
                return new FrameConfigurationApplyResult(FrameConfigurationApplyStatus.Cancelled, "Frame configuration was cancelled before any value was confirmed.", [], [], false);
            }

            return new FrameConfigurationApplyResult(
                FrameConfigurationApplyStatus.PartialFailure,
                "The operation was cancelled after one or more writes. Reconnect and verify the listed parameters before flying.",
                confirmed.Select(item => item.Name).ToArray(), [], RequiresReboot(confirmed, configuration));
        }

        await RefreshAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        return new FrameConfigurationApplyResult(
            FrameConfigurationApplyStatus.Succeeded,
            "All reviewed values were confirmed by vehicle readback.",
            confirmed.Select(item => item.Name).ToArray(), [], RequiresReboot(confirmed, configuration));
    }

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var names = frameParameters.GetValueOrDefault(state.Identity.Firmware.Family) ?? [];
        foreach (var name in names.Append("ARMING_CHECK").Distinct(StringComparer.Ordinal))
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

    private static IReadOnlyList<FrameInitialParameterRecommendation> CreateRecommendations(
        IReadOnlyDictionary<string, VehicleParameter> values,
        IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        const string name = "ARMING_CHECK";
        if (!values.TryGetValue(name, out var parameter) || parameter.Value == 1 ||
            !metadata.TryGetValue(name, out var definition) || definition.ReadOnly ||
            definition.MinValue is { } minimum && 1 < minimum ||
            definition.MaxValue is { } maximum && 1 > maximum)
        {
            return [];
        }

        return
        [
            new FrameInitialParameterRecommendation(
                name,
                definition.DisplayName ?? name,
                parameter.Value,
                1,
                parameter.Type,
                "Enable all firmware-defined pre-arm checks while completing initial setup.",
                definition.RebootRequired)
        ];
    }

    private static bool IsAllowed(
        FrameParameterChange change,
        IReadOnlyDictionary<string, FrameParameterSetting> settings,
        IReadOnlyDictionary<string, FrameInitialParameterRecommendation> recommendations)
    {
        if (settings.TryGetValue(change.Name, out var setting))
        {
            return setting.ParameterType == change.ParameterType &&
                NearlyEqual(setting.CurrentValue, change.OriginalValue) &&
                setting.Options.Any(option => NearlyEqual(option.Value, change.PendingValue));
        }

        return recommendations.TryGetValue(change.Name, out var recommendation) &&
            recommendation.ParameterType == change.ParameterType &&
            NearlyEqual(recommendation.CurrentValue, change.OriginalValue) &&
            NearlyEqual(recommendation.RecommendedValue, change.PendingValue);
    }

    private async Task<FrameConfigurationApplyResult> RollBackAsync(
        VehicleId vehicleId,
        IReadOnlyList<FrameParameterChange> confirmed,
        string failure)
    {
        var rollbackFailed = new List<string>();
        using var rollbackCancellation = new CancellationTokenSource(readbackTimeout);
        foreach (var change in confirmed.Reverse())
        {
            try
            {
                if (!await WriteAndConfirmAsync(vehicleId, change.Name, change.OriginalValue, change.ParameterType, rollbackCancellation.Token).ConfigureAwait(false))
                {
                    rollbackFailed.Add(change.Name);
                }
            }
            catch (OperationCanceledException)
            {
                rollbackFailed.AddRange(confirmed
                    .Where(item => !rollbackFailed.Contains(item.Name, StringComparer.Ordinal))
                    .Select(item => item.Name));
                break;
            }
        }

        var status = rollbackFailed.Count == 0 ? FrameConfigurationApplyStatus.RolledBack : FrameConfigurationApplyStatus.PartialFailure;
        var guidance = rollbackFailed.Count == 0
            ? "Previously written values were restored and confirmed."
            : $"Could not confirm rollback for {string.Join(", ", rollbackFailed)}. Reconnect, refresh, and review these values before flying.";
        return new FrameConfigurationApplyResult(status, $"{failure} {guidance}", confirmed.Select(item => item.Name).ToArray(), rollbackFailed, false);
    }

    private async Task<bool> WriteAndConfirmAsync(
        VehicleId vehicleId,
        string name,
        float value,
        MavParamType type,
        CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter &&
                parameter.Name == name && NearlyEqual(parameter.Value, value))
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, name, value, type, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, name) is { } current && NearlyEqual(current.Value, value))
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
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

    private static bool RequiresReboot(
        IEnumerable<FrameParameterChange> changes,
        FrameConfigurationSnapshot configuration) =>
        changes.Any(change =>
            configuration.Settings.FirstOrDefault(item => item.Name == change.Name)?.RebootRequired == true ||
            configuration.Recommendations.FirstOrDefault(item => item.Name == change.Name)?.RebootRequired == true);

    private static bool NearlyEqual(float first, float second) => Math.Abs(first - second) <= 0.0001f;
}
