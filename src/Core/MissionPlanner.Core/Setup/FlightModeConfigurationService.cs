using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup;

/// <summary>Projects firmware flight-mode slot configuration and applies guarded, readback-confirmed writes.</summary>
public sealed class FlightModeConfigurationService : IFlightModeConfigurationService
{
    private static readonly TimeSpan staleWindow = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);

    // Standard ArduPilot six-position PWM bands used by the flight-mode channel.
    private static readonly (int Low, int High)[] pwmBands =
    [
        (0, 1230), (1231, 1360), (1361, 1490), (1491, 1620), (1621, 1749), (1750, 2200)
    ];

    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterService parameterService;
    private readonly IArduPilotModeCatalog modeCatalog;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<FlightModeConfigurationService> logger;

    /// <summary>Initializes the flight-mode configuration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="modeCatalog">The firmware mode catalog.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public FlightModeConfigurationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterService parameterService,
        IArduPilotModeCatalog modeCatalog,
        IDateTimeProvider clock,
        ILogger<FlightModeConfigurationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.parameterService = parameterService;
        this.modeCatalog = modeCatalog;
        this.clock = clock;
        this.logger = logger;
    }

    /// <inheritdoc />
    public FlightModeConfiguration GetConfiguration(VehicleId vehicleId)
    {
        var state = RequireActiveVehicle(vehicleId);
        var family = state.Identity.Firmware.Family;
        if (!TryResolveNames(family, out var channelName, out var slotPrefix))
        {
            return FlightModeConfiguration.Unsupported(vehicleId, family);
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        if (!parameters.TryGetValue(channelName, out var channelParameter))
        {
            return FlightModeConfiguration.Unsupported(vehicleId, family);
        }

        var modeChannel = (int)Math.Round(channelParameter.Value);
        var options = modeCatalog.GetModes(family);
        var activeSlot = ResolveActiveSlot(state, modeChannel);
        var slots = new List<FlightModeSlot>(pwmBands.Length);
        for (var slot = 1; slot <= pwmBands.Length; slot++)
        {
            var band = pwmBands[slot - 1];
            var modeNumber = parameters.TryGetValue($"{slotPrefix}{slot}", out var slotParameter) ? (int)Math.Round(slotParameter.Value) : 0;
            var option = options.FirstOrDefault(item => item.CustomMode == (uint)modeNumber);
            slots.Add(new FlightModeSlot(slot, band.Low, band.High, modeNumber,
                option?.Name ?? $"Mode {modeNumber}", activeSlot == slot));
        }

        return new FlightModeConfiguration(vehicleId, family, true, modeChannel, slots, options, activeSlot);
    }

    /// <inheritdoc />
    public async Task<FlightModeApplyResult> SetSlotAsync(VehicleId vehicleId, int slot, int modeNumber, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var family = state.Identity.Firmware.Family;
        if (!TryResolveNames(family, out _, out var slotPrefix))
        {
            return new FlightModeApplyResult(false, $"Flight-mode slots are not configurable for {family}.");
        }

        if (slot < 1 || slot > pwmBands.Length)
        {
            return new FlightModeApplyResult(false, $"Slot {slot} is outside the supported range 1-{pwmBands.Length}.");
        }

        if (modeCatalog.GetModes(family).All(option => option.CustomMode != (uint)modeNumber))
        {
            return new FlightModeApplyResult(false, $"Mode {modeNumber} is not offered by {family}.");
        }

        var name = $"{slotPrefix}{slot}";
        logger.LogInformation("Assigning flight-mode slot {Slot} to mode {Mode} on {VehicleId}.", slot, modeNumber, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, name, modeNumber, cancellationToken).ConfigureAwait(false))
        {
            return new FlightModeApplyResult(true, $"Confirmed slot {slot} assignment by vehicle readback.");
        }

        return new FlightModeApplyResult(false, $"Readback did not confirm slot {slot}. Reconnect, refresh, and verify before flying.");
    }

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        if (!TryResolveNames(state.Identity.Firmware.Family, out var channelName, out var slotPrefix))
        {
            return;
        }

        foreach (var name in Enumerable.Range(1, pwmBands.Length).Select(slot => $"{slotPrefix}{slot}").Prepend(channelName))
        {
            if (parameterRegistry.GetParameter(vehicleId, name) is not null)
            {
                await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private int? ResolveActiveSlot(VehicleState state, int modeChannel)
    {
        if (modeChannel < 1 || state.Radio.IsStale(clock.UtcNow, staleWindow))
        {
            return null;
        }

        var raw = state.Radio.ChannelsRaw;
        if (modeChannel > raw.Count)
        {
            return null;
        }

        var pwm = raw[modeChannel - 1];
        if (pwm == 0)
        {
            return null;
        }

        for (var slot = 0; slot < pwmBands.Length; slot++)
        {
            if (pwm >= pwmBands[slot].Low && pwm <= pwmBands[slot].High)
            {
                return slot + 1;
            }
        }

        return null;
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        return state;
    }

    private static bool TryResolveNames(FirmwareFamily family, out string channelName, out string slotPrefix)
    {
        switch (family)
        {
            case FirmwareFamily.ArduCopter:
            case FirmwareFamily.ArduPlane:
            case FirmwareFamily.Blimp:
                channelName = "FLTMODE_CH";
                slotPrefix = "FLTMODE";
                return true;
            case FirmwareFamily.Rover:
                channelName = "MODE_CH";
                slotPrefix = "MODE";
                return true;
            default:
                channelName = string.Empty;
                slotPrefix = string.Empty;
                return false;
        }
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, int value, CancellationToken cancellationToken)
    {
        var type = parameterRegistry.GetParameter(vehicleId, name)?.Type ?? MavParamType.Int8;
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == name &&
                Math.Abs(parameter.Value - value) <= 0.5f)
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

            if (parameterRegistry.GetParameter(vehicleId, name) is { } current && Math.Abs(current.Value - value) <= 0.5f)
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
}
