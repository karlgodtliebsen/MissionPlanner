using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Discovers STM32CubeProgrammer CLI installations from ordered Windows sources.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCubeProgrammerDiscoverySource(IOptions<DfuOptions> options) : IDfuToolDiscoverySource
{
    private const string ExecutableName = "STM32_Programmer_CLI.exe";

    /// <inheritdoc />
    public IReadOnlyList<DfuToolCandidate> Discover()
    {
        var result = new List<DfuToolCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configured = options.Value;
        if (!string.IsNullOrWhiteSpace(configured.CubeProgrammerExecutablePath))
            AddCandidate(result, seen, configured.CubeProgrammerExecutablePath, DfuToolDiscoverySource.UserConfigured, includeMissing: true);

        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            AddCandidate(result, seen,
                Path.Combine(root, "STMicroelectronics", "STM32Cube", "STM32CubeProgrammer", "bin", ExecutableName),
                DfuToolDiscoverySource.KnownInstallation);
        }

        foreach (var path in RegistryCandidates()) AddCandidate(result, seen, path, DfuToolDiscoverySource.Registry);
        if (configured.SearchPathForCubeProgrammer)
        {
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddCandidate(result, seen, Path.Combine(directory, ExecutableName), DfuToolDiscoverySource.Path);
        }
        return result.AsReadOnly();
    }

    private static void AddCandidate(List<DfuToolCandidate> result, HashSet<string> seen, string path, DfuToolDiscoverySource source, bool includeMissing = false)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(path.Trim().Trim('"')); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            if (includeMissing) result.Add(new DfuToolCandidate(path, source, false));
            return;
        }
        var validName = string.Equals(Path.GetFileName(fullPath), ExecutableName, StringComparison.OrdinalIgnoreCase);
        var exists = validName && File.Exists(fullPath);
        if ((!exists && !includeMissing) || !seen.Add(fullPath)) return;
        result.Add(new DfuToolCandidate(fullPath, source, exists, exists ? ReadVersion(fullPath) : null));
    }

    private static Version? ReadVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.FileMajorPart < 0 || info.FileMinorPart < 0 ||
                   (info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 && info.FilePrivatePart == 0)
                ? null
                : new Version(info.FileMajorPart, info.FileMinorPart, Math.Max(0, info.FileBuildPart), Math.Max(0, info.FilePrivatePart));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> RegistryCandidates()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) continue;
            foreach (var childName in uninstall.GetSubKeyNames())
            {
                using var child = uninstall.OpenSubKey(childName);
                var displayName = child?.GetValue("DisplayName") as string;
                if (displayName?.Contains("STM32CubeProgrammer", StringComparison.OrdinalIgnoreCase) != true) continue;
                var installLocation = child?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(installLocation))
                    yield return Path.Combine(installLocation, "bin", ExecutableName);
                var displayIcon = child?.GetValue("DisplayIcon") as string;
                if (!string.IsNullOrWhiteSpace(displayIcon) && displayIcon.Contains(ExecutableName, StringComparison.OrdinalIgnoreCase))
                    yield return displayIcon.Split(',')[0].Trim('"');
            }
        }
    }
}
