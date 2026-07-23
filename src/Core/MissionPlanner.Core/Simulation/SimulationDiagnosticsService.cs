using System.Text.Json;
using MissionPlanner.Core.Replay;
using MissionPlanner.MavLink;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Simulation;

/// <summary>Creates redacted structured diagnostics for the simulation workspace.</summary>
public sealed class SimulationDiagnosticsService(IReplaySessionManager? replaySessionManager = null) : ISimulationDiagnosticsService
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly string[] sensitiveTerms = ["password", "passwd", "secret", "token", "api-key", "apikey"];

    /// <inheritdoc />
    public string CreateBundle(SimulationSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var profile = snapshot.Profile;
        var knownSecrets = GetKnownSecrets(profile, snapshot.RuntimeDiagnostics);
        var document = new
        {
            schema = "missionplanner-simulation-diagnostics-v2",
            generatedAt = DateTimeOffset.UtcNow,
            versions = new
            {
                core = AssemblyVersion(typeof(SimulationDiagnosticsService)),
                mavlink = AssemblyVersion(typeof(MavLinkFrame)),
                transport = AssemblyVersion(typeof(TransportEndPoint)),
                runtime = Environment.Version.ToString(),
                operatingSystem = Environment.OSVersion.VersionString
            },
            session = new
            {
                snapshot.SessionId,
                snapshot.State,
                snapshot.RuntimeIdentity,
                snapshot.ConnectionEndpoints,
                snapshot.StartedAt,
                snapshot.EndedAt,
                message = RedactKnownSecrets(snapshot.Message, knownSecrets),
                failure = RedactKnownSecrets(snapshot.Failure, knownSecrets),
                processState = snapshot.State.ToString(),
                artifacts = snapshot.Artifacts,
                runtime = snapshot.RuntimeDiagnostics is null
                    ? null
                    : new
                    {
                        executablePath = snapshot.RuntimeDiagnostics.ExecutablePath,
                        commandArguments = RedactArguments(snapshot.RuntimeDiagnostics.Arguments, knownSecrets),
                        snapshot.RuntimeDiagnostics.RuntimeVersion,
                        snapshot.RuntimeDiagnostics.ProcessStartedAt,
                        heartbeat = snapshot.RuntimeDiagnostics.Heartbeat
                    }
            },
            profile = profile is null
                ? null
                : new
                {
                    profile.Id,
                    profile.Name,
                    profile.FirmwareFamily,
                    profile.FrameModel,
                    profile.Location,
                    profile.Speedup,
                    profile.Endpoints,
                    binary = new
                    {
                        profile.Binary.Version,
                        profile.Binary.ExecutablePath,
                        profile.Binary.Source
                    },
                    additionalArguments = RedactArguments(profile.AdditionalArguments, knownSecrets),
                    environment = profile.Environment.ToDictionary(
                        item => item.Key,
                        item => IsSensitive(item.Key)
                            ? "***"
                            : RedactKnownSecrets(item.Value, knownSecrets) ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                },
            replay = CreateReplayDiagnostics(replaySessionManager?.Snapshot),
            recentOutput = snapshot.RecentOutput.Select(line => line with
            {
                Text = RedactKnownSecrets(line.Text, knownSecrets) ?? string.Empty
            }).ToArray()
        };
        return JsonSerializer.Serialize(document, jsonOptions);
    }

    private static object? CreateReplayDiagnostics(ReplaySessionSnapshot? replay)
    {
        if (replay is null || replay.State == ReplaySessionState.Unloaded)
        {
            return null;
        }

        return new
        {
            replay.SessionId,
            replay.State,
            source = replay.Index?.SourceName,
            frameCount = replay.Index?.Entries.Count ?? 0,
            replay.NextFrameIndex,
            replay.DecodedFrames,
            replay.RejectedFrames,
            replay.Clock,
            vehicles = replay.Vehicles.Select(vehicle => new
            {
                vehicle.VehicleId,
                vehicle.DisplayName,
                firmware = vehicle.Identity.Firmware.Family
            }).ToArray(),
            transmission = "prohibited"
        };
    }

    private static IReadOnlyList<string> RedactArguments(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> knownSecrets)
    {
        var result = new string[arguments.Count];
        var redactNext = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var original = arguments[index];
            if (redactNext)
            {
                result[index] = "***";
                redactNext = false;
                continue;
            }

            var argument = RedactKnownSecrets(original, knownSecrets) ?? string.Empty;
            var separator = argument.IndexOf('=');
            if (separator > 0 && IsSensitive(argument[..separator]))
            {
                result[index] = $"{argument[..separator]}=***";
                continue;
            }

            if (separator < 0 && IsSensitive(argument))
            {
                if (argument.StartsWith('-'))
                {
                    result[index] = argument;
                    redactNext = true;
                }
                else
                {
                    result[index] = "***";
                }

                continue;
            }

            result[index] = argument;
        }

        return result;
    }

    private static bool IsSensitive(string value) =>
        sensitiveTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> GetKnownSecrets(
        SimulatorProfile? profile,
        SimulationRuntimeDiagnostics? runtimeDiagnostics)
    {
        var values = profile?.Environment
            .Where(item => IsSensitive(item.Key) && !string.IsNullOrEmpty(item.Value))
            .Select(item => item.Value)
            .ToList() ?? [];
        AddArgumentSecrets(values, profile?.AdditionalArguments ?? []);
        AddArgumentSecrets(values, runtimeDiagnostics?.Arguments ?? []);
        return values.Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length).ToArray();
    }

    private static void AddArgumentSecrets(List<string> values, IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            var separator = argument.IndexOf('=');
            if (separator > 0 && IsSensitive(argument[..separator]) && separator + 1 < argument.Length)
            {
                values.Add(argument[(separator + 1)..]);
            }
            else if (separator < 0 && IsSensitive(argument) && index + 1 < arguments.Count)
            {
                values.Add(arguments[++index]);
            }
        }
    }

    private static string? RedactKnownSecrets(string? value, IReadOnlyList<string> knownSecrets)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var secret in knownSecrets)
        {
            value = value.Replace(secret, "***", StringComparison.Ordinal);
        }

        return value;
    }

    private static string AssemblyVersion(Type type) =>
        type.Assembly.GetName().Version?.ToString() ?? "unavailable";
}
