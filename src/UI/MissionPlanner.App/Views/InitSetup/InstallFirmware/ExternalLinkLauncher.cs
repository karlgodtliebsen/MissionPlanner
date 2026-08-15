namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Uses the MAUI host launcher for external HTTPS destinations.</summary>
public sealed class ExternalLinkLauncher : IExternalLinkLauncher
{
    /// <inheritdoc />
    public async Task OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("External firmware links must use HTTPS.", nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await Launcher.Default.OpenAsync(uri))
        {
            throw new InvalidOperationException($"The host could not open {uri.Host}.");
        }
    }
}
