using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects RC arming assignments and writes one explicitly reviewed free auxiliary channel.</summary>
public sealed class RadioArmingConfiguration(
    IActiveVehicleContext active, IVehicleParameterRegistry parameters, IDomainFactory factory)
{
    /// <summary>ArduPilot's Arm/Disarm auxiliary function, validated against firmware metadata when available.</summary>
    public const int ArmDisarmOption = 153;

    /// <summary>Describes parameter-derived arming configuration without inferring an arm request.</summary>
    public static string Describe(Func<string, float?> value)
    {
        var flight = value("FLTMODE_CH");
        var assigned = Enumerable.Range(1, 16).Where(channel => value($"RC{channel}_OPTION") == ArmDisarmOption).ToArray();
        var known = Enumerable.Range(1, 16).Any(channel => value($"RC{channel}_OPTION") is not null);
        var rudder = value("ARMING_RUDDER");
        return $"Flight mode channel: {(flight is > 0 ? $"RC{flight}" : flight == 0 ? "Disabled" : "Unknown")}\n" +
            $"Arm/Disarm auxiliary channel: {(assigned.Length > 0 ? string.Join(", ", assigned.Select(channel => $"RC{channel}")) : known ? "Not configured in loaded RC options" : "Unknown — parameters not loaded")}\n" +
            $"Stick arming: {(rudder is > 0 ? "Enabled" : rudder == 0 ? "Disabled" : "Unknown")}";
    }

    /// <summary>Explains why a channel cannot be assigned, including missing parameters and flight-control conflicts.</summary>
    public string? Conflict(VehicleId id, int channel)
    {
        if (channel is < 1 or > 16)
        {
            return "Choose an RC channel from 1 to 16.";
        }
        float? Value(string name) => parameters.GetParameter(id, name)?.Value;
        if (Value("FLTMODE_CH") is not { } flight || Value($"RC{channel}_OPTION") is not { } option)
        {
            return "Wait for the flight-mode and RC option parameters to load.";
        }
        if (flight == channel)
        {
            return $"RC{channel} is currently used for Flight Modes. Choose another channel for Arm/Disarm.";
        }
        foreach (var axis in new[] { "ROLL", "PITCH", "THROTTLE", "YAW" })
        {
            if (Value($"RCMAP_{axis}") is not { } mapped)
            {
                return "Wait for the pilot channel map to load.";
            }
            if (mapped == channel)
            {
                return $"RC{channel} is a primary {axis.ToLowerInvariant()} control. Choose another channel.";
            }
        }
        return option != 0 ? $"RC{channel} already has auxiliary function {option}. Choose a free channel." : null;
    }

    /// <summary>Writes only the selected RC option using firmware metadata, connection guards and confirmed readback.</summary>
    public async Task<string> AssignAsync(VehicleId id, int channel, CancellationToken cancellationToken)
    {
        var connection = active.ConnectionCancellationToken;
        void Guard()
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection.ThrowIfCancellationRequested();
            if (active.VehicleId != id || !active.IsOnline || active.State?.IsArmed != false || active.ConnectionCancellationToken != connection)
            {
                throw new InvalidOperationException("Connect the selected vehicle and keep it disarmed before assigning an arm switch.");
            }
            if (Conflict(id, channel) is { } conflict)
            {
                throw new InvalidOperationException(conflict);
            }
        }
        Guard();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(connection, cancellationToken);
        using var session = factory.Create<IParameterEditSession, ParameterEditScope>(new(id, active.State!.Identity.Firmware));
        var name = $"RC{channel}_OPTION";
        await session.LoadAsync([name], linked.Token);
        var field = session.GetField(name) ?? throw new InvalidOperationException($"{name} is unavailable.");
        if (field.Metadata.Options.Count > 0 && !field.Metadata.Options.Any(option => option.Value == ArmDisarmOption))
        {
            throw new InvalidOperationException("This firmware's metadata does not advertise Arm/Disarm for this channel.");
        }
        Guard();
        if (!session.TrySetPending(name, ArmDisarmOption, out var error))
        {
            throw new InvalidOperationException(error);
        }
        var result = await session.ApplyAsync(session.CreateWritePlan([name]), cancellationToken: linked.Token);
        return result.Success
            ? $"RC{channel} Arm/Disarm assignment verified." + (result.RebootRequired ? " Reboot required." : string.Empty)
            : $"RC{channel} assignment was not verified. Refresh parameters before retrying.";
    }
}

/// <summary>Retains a bounded low/high transition trace; movement is evidence of input, not of arming.</summary>
public sealed class RadioSwitchMovement
{
    private readonly Dictionary<int, List<ushort>> traces = [];

    /// <summary>Clears observations at vehicle, connection or page boundaries.</summary>
    public void Clear() => traces.Clear();

    /// <summary>Records endpoint transitions from a fresh radio sample.</summary>
    public void Observe(VehicleRadioState radio)
    {
        if (radio.ChannelsRaw is not { } channels)
        {
            return;
        }
        for (var index = 0; index < Math.Min(Math.Min(channels.Count, radio.ChannelCount ?? 0), 16); index++)
        {
            var pwm = channels[index];
            if (pwm is < 800 or > 2200 || pwm is > 1200 and < 1800)
            {
                continue;
            }
            if (!traces.TryGetValue(index + 1, out var trace))
            {
                trace = [];
                traces.Add(index + 1, trace);
            }
            if (trace.Count == 0 || (trace[^1] < 1500) != (pwm < 1500))
            {
                trace.Add(pwm);
                if (trace.Count > 3)
                {
                    trace.RemoveAt(0);
                }
            }
        }
    }

    /// <summary>Describes a channel only after at least two endpoint transitions.</summary>
    public string? Describe(int channel) => traces.TryGetValue(channel, out var trace) && trace.Count >= 3
        ? $"RC{channel}: {string.Join(" → ", trace)}. Appears to be a two-position switch." : null;
}
