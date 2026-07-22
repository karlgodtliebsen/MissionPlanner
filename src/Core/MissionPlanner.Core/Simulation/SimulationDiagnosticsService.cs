using System.Text.Json;

namespace MissionPlanner.Core.Simulation;

/// <summary>Creates redacted structured diagnostics for the simulation workspace.</summary>
public sealed class SimulationDiagnosticsService : ISimulationDiagnosticsService
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
        var knownSecrets = GetKnownSecrets(profile);
        var document = new
        {
            schema = "missionplanner-simulation-diagnostics-v1",
            session = new
            {
                snapshot.SessionId,
                snapshot.State,
                snapshot.RuntimeIdentity,
                snapshot.ConnectionEndpoints,
                snapshot.StartedAt,
                snapshot.EndedAt,
                message = RedactKnownSecrets(snapshot.Message, knownSecrets),
                failure = RedactKnownSecrets(snapshot.Failure, knownSecrets)
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
                    additionalArguments = profile.AdditionalArguments.Select(RedactArgument).ToArray(),
                    environment = profile.Environment.ToDictionary(
                        item => item.Key,
                        item => IsSensitive(item.Key) ? "***" : item.Value,
                        StringComparer.OrdinalIgnoreCase)
                },
            recentOutput = snapshot.RecentOutput.Select(line => line with
            {
                Text = RedactKnownSecrets(line.Text, knownSecrets) ?? string.Empty
            }).ToArray()
        };
        return JsonSerializer.Serialize(document, jsonOptions);
    }

    private static string RedactArgument(string argument)
    {
        var separator = argument.IndexOf('=');
        if (separator <= 0)
        {
            return IsSensitive(argument) ? "***" : argument;
        }

        var name = argument[..separator];
        return IsSensitive(name) ? $"{name}=***" : argument;
    }

    private static bool IsSensitive(string value) =>
        sensitiveTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> GetKnownSecrets(SimulatorProfile? profile)
    {
        if (profile is null)
        {
            return [];
        }

        var values = profile.Environment
            .Where(item => IsSensitive(item.Key) && !string.IsNullOrEmpty(item.Value))
            .Select(item => item.Value)
            .ToList();
        foreach (var argument in profile.AdditionalArguments)
        {
            var separator = argument.IndexOf('=');
            if (separator > 0 && IsSensitive(argument[..separator]) && separator + 1 < argument.Length)
            {
                values.Add(argument[(separator + 1)..]);
            }
        }

        return values.Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length).ToArray();
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
}
