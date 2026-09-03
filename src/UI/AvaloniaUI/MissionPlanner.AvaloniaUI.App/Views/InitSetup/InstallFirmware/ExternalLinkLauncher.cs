using System.Diagnostics;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Uses the host launcher for external HTTPS destinations.</summary>
public sealed class ExternalLinkLauncher : IExternalLinkLauncher
{
    /// <inheritdoc />
    public Task OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("External firmware links must use HTTPS.", nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException($"The host could not open {uri.Host}.");
        }

        return Task.CompletedTask;
    }
}

