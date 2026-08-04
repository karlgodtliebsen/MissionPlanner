using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Locates and non-destructively validates an external STM32CubeProgrammer CLI.</summary>
public sealed partial class Stm32CubeProgrammerToolLocator(
    IDfuToolDiscoverySource discovery,
    IDfuProcessRunner processRunner,
    IOptions<DfuOptions> options) : IDfuToolLocator
{
    /// <inheritdoc />
    public async Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var candidates = discovery.Discover();
        if (candidates.FirstOrDefault(candidate => candidate.Source == DfuToolDiscoverySource.UserConfigured) is { Exists: false } invalidConfigured)
            return new DfuToolStatus(DfuToolAvailability.PathInvalid, invalidConfigured.ExecutablePath, Diagnostic: "The configured STM32CubeProgrammer CLI path does not exist.");

        DfuToolStatus? strongestFailure = null;
        foreach (var candidate in candidates.Where(candidate => candidate.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new DfuProcessRequest(
                candidate.ExecutablePath,
                ["--version"],
                configured.CubeProgrammerProbeTimeout,
                configured.CubeProgrammerProbeTimeout);
            DfuProcessResult probe;
            try
            {
                probe = await processRunner.RunAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                strongestFailure ??= new DfuToolStatus(DfuToolAvailability.ExecutionBlocked, candidate.ExecutablePath, candidate.FileVersion, exception.Message);
                continue;
            }

            if (probe.TimedOut || probe.ExitCode is not 0)
            {
                strongestFailure ??= new DfuToolStatus(DfuToolAvailability.ExecutionBlocked, candidate.ExecutablePath, candidate.FileVersion,
                    probe.TimedOut ? "The validation probe timed out." : $"The validation probe exited with code {probe.ExitCode?.ToString() ?? "unknown"}.");
                continue;
            }

            var version = candidate.FileVersion ?? ParseVersion(probe.Output.Select(line => line.Text));
            if (version is null || version < configured.MinimumCubeProgrammerVersion)
            {
                strongestFailure = new DfuToolStatus(DfuToolAvailability.UnsupportedVersion, candidate.ExecutablePath, version,
                    version is null ? "The installed version could not be determined." : $"Version {version} is older than required {configured.MinimumCubeProgrammerVersion}.");
                continue;
            }

            return new DfuToolStatus(DfuToolAvailability.Available, candidate.ExecutablePath, version, "STM32CubeProgrammer CLI validation succeeded.");
        }

        return strongestFailure ?? new DfuToolStatus(DfuToolAvailability.NotInstalled, Diagnostic: "STM32CubeProgrammer CLI was not found.");
    }

    private static Version? ParseVersion(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = VersionPattern().Match(line);
            if (match.Success && Version.TryParse(match.Groups[1].Value, out var version)) return version;
        }
        return null;
    }

    [GeneratedRegex(@"(?<!\d)(\d+\.\d+(?:\.\d+){0,2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
