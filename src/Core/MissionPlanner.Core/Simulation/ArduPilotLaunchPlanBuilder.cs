using System.Globalization;
using System.Net;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Builds typed argument tokens for an ArduPilot direct SITL executable.</summary>
public sealed class ArduPilotLaunchPlanBuilder(IArduPilotFrameCatalog frameCatalog) : IArduPilotLaunchPlanBuilder
{
    private static readonly string[] protectedArguments =
    [
        "--model", "-M", "--home", "-O", "--speedup", "-s", "--instance", "-I",
        "--sysid", "--serial", "--defaults", "--wipe"
    ];

    /// <inheritdoc />
    public ArduPilotLaunchPlan Build(SimulatorProfile profile, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!frameCatalog.IsSupported(profile.FirmwareFamily, profile.FrameModel))
        {
            throw new InvalidOperationException(
                $"Frame/model '{profile.FrameModel}' is not supported for {profile.FirmwareFamily} by the direct SITL adapter.");
        }

        var mavLink = profile.Endpoints.SingleOrDefault(endpoint =>
            endpoint.Name.Equals("MAVLink", StringComparison.OrdinalIgnoreCase));
        if (mavLink is null || mavLink.Transport != SimulationEndpointTransport.Udp)
        {
            throw new InvalidOperationException("A single UDP endpoint named 'MAVLink' is required.");
        }

        var settings = profile.EffectiveLaunchSettings;
        var invariant = CultureInfo.InvariantCulture;
        var home = string.Join(",",
            profile.Location.LatitudeDegrees.ToString("0.#######", invariant),
            profile.Location.LongitudeDegrees.ToString("0.#######", invariant),
            profile.Location.AltitudeMeters.ToString("0.###", invariant),
            profile.Location.HeadingDegrees.ToString("0.###", invariant));
        var arguments = new List<string>
        {
            "--model",
            profile.FrameModel.Trim(),
            "--home",
            home,
            "--speedup",
            profile.Speedup.ToString("0.###", invariant),
            "--instance",
            settings.Instance.ToString(invariant),
            "--sysid",
            settings.SystemId.ToString(invariant),
            "--serial0",
            $"udpclient:{mavLink.Host}:{mavLink.Port}"
        };
        var serialIndices = new HashSet<int>();
        foreach (var serial in settings.EffectiveSerialEndpoints.OrderBy(item => item.Index))
        {
            if (serial.Index is < 1 or > 9 || !serialIndices.Add(serial.Index))
            {
                throw new InvalidOperationException("Additional serial endpoint indices must be unique values from 1 through 9.");
            }

            if (serial.Port is <= 0 or > 65535 || !IsValidEndpointHost(serial.Host))
            {
                throw new InvalidOperationException($"Serial{serial.Index} must have a valid host and port.");
            }

            var transport = serial.Transport switch
            {
                ArduPilotSerialTransport.UdpClient => "udpclient",
                ArduPilotSerialTransport.TcpClient => "tcpclient",
                var _ => throw new InvalidOperationException($"Serial{serial.Index} transport is unsupported.")
            };
            arguments.Add($"--serial{serial.Index}");
            arguments.Add($"{transport}:{serial.Host}:{serial.Port}");
        }

        if (settings.DefaultsFiles.Count != 0)
        {
            arguments.Add("--defaults");
            arguments.Add(string.Join(',', settings.DefaultsFiles.Select(Path.GetFullPath)));
        }

        if (settings.WipeState)
        {
            arguments.Add("--wipe");
        }

        foreach (var argument in profile.AdditionalArguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new InvalidOperationException("Additional SITL argument tokens cannot be empty.");
            }

            if (protectedArguments.Any(item => IsProtectedOverride(item, argument)))
            {
                throw new InvalidOperationException(
                    $"Additional argument '{argument}' attempts to override a typed launch setting.");
            }

            arguments.Add(argument);
        }

        return new ArduPilotLaunchPlan(
            Path.GetFullPath(profile.Binary.ExecutablePath),
            Path.GetFullPath(workingDirectory),
            arguments,
            new Dictionary<string, string>(profile.Environment, StringComparer.OrdinalIgnoreCase),
            mavLink,
            settings.SystemId,
            settings.ShowConsoleWindow);
    }

    private static bool IsProtectedOverride(string protectedArgument, string argument)
    {
        return argument.Equals(protectedArgument, StringComparison.OrdinalIgnoreCase) ||
               argument.StartsWith(protectedArgument + "=", StringComparison.OrdinalIgnoreCase) ||
               (protectedArgument.Equals("--serial", StringComparison.Ordinal) &&
                argument.StartsWith("--serial", StringComparison.OrdinalIgnoreCase)) ||
               (protectedArgument is "-M" or "-O" or "-s" or "-I" &&
                argument.StartsWith(protectedArgument, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidEndpointHost(string host)
    {
        return !string.IsNullOrWhiteSpace(host) &&
               !host.Contains(':') &&
               (IPAddress.TryParse(host, out var _) || Uri.CheckHostName(host) == UriHostNameType.Dns);
    }
}
