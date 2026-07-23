using System.Diagnostics;
using System.Runtime.InteropServices;
using MissionPlanner.Core.Simulation;

namespace MissionPlanner.App.Helpers;

/// <summary>Supplies a dedicated SITL directory beneath the platform cache.</summary>
public sealed class MauiSitlCachePathProvider : ISitlCachePathProvider
{
    /// <inheritdoc />
    public string CacheRoot => Path.Combine(FileSystem.CacheDirectory, "sitl");
}

/// <summary>Detects the current desktop SITL platform and probes configured executables safely.</summary>
public sealed class LocalSitlPlatformService : ISitlPlatformService
{
    /// <inheritdoc />
    public SitlPlatformCapability Current { get; } = Detect();

    /// <inheritdoc />
    public async Task<string?> TryQueryVersionAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        if (!Current.CanExecuteNative || !Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("--version");
        try
        {
            if (!process.Start())
            {
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = string.IsNullOrWhiteSpace(await standardOutput.ConfigureAwait(false))
                ? await standardError.ConfigureAwait(false)
                : await standardOutput.ConfigureAwait(false);
            var firstLine = output.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            return firstLine?[..Math.Min(256, firstLine.Length)];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminateProbe(process);
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            TryTerminateProbe(process);
            return null;
        }
    }

    private static SitlPlatformCapability Detect()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => SitlArchitecture.X64,
            Architecture.Arm64 => SitlArchitecture.Arm64,
            var _ => SitlArchitecture.X64
        };
        var architectureSupported = RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;
        if (OperatingSystem.IsWindows())
        {
            return new SitlPlatformCapability(
                SitlPlatform.Windows,
                architecture,
                architectureSupported,
                architectureSupported ? "Native Windows SITL is supported." : "This Windows CPU architecture is unsupported.");
        }

        if (OperatingSystem.IsLinux())
        {
            var wsl = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));
            return new SitlPlatformCapability(
                wsl ? SitlPlatform.WindowsSubsystemForLinux : SitlPlatform.Linux,
                architecture,
                architectureSupported,
                architectureSupported
                    ? wsl ? "SITL can run inside the current WSL environment." : "Native Linux SITL is supported."
                    : "This Linux CPU architecture is unsupported.");
        }

        return OperatingSystem.IsMacOS()
            ? new SitlPlatformCapability(
                SitlPlatform.MacOS,
                architecture,
                architectureSupported,
                architectureSupported ? "Native macOS SITL is supported." : "This macOS CPU architecture is unsupported.")
            : new SitlPlatformCapability(
                SitlPlatform.Linux,
                architecture,
                false,
                "Local SITL execution is unavailable on this application platform.");
    }

    private static void TryTerminateProbe(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
