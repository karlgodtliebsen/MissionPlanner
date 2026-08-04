using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Resolves either an official sibling or an explicitly selected local Intel HEX artifact.</summary>
public sealed class DfuArtifactResolver(
    IDfuHexArtifactDownloader downloader,
    IIntelHexInspector inspector,
    IOptions<DfuOptions> options) : IDfuArtifactResolver
{
    /// <inheritdoc />
    public async Task<DfuArtifact> ResolveAsync(DfuInstallationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasOfficial = request.ManifestEntry is not null;
        var hasLocal = !string.IsNullOrWhiteSpace(request.LocalHexPath);
        if (hasOfficial == hasLocal) throw new DfuArtifactResolutionException("Select exactly one official manifest release or local Intel HEX file.");
        return hasOfficial
            ? await ResolveOfficialAsync(request, cancellationToken).ConfigureAwait(false)
            : await ResolveLocalAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DfuArtifact> ResolveOfficialAsync(DfuInstallationRequest request, CancellationToken cancellationToken)
    {
        var entry = request.ManifestEntry!;
        if (!string.Equals(entry.Target.Platform, request.SelectedPlatform, StringComparison.OrdinalIgnoreCase) ||
            (request.SelectedBoardId is int boardId && boardId != entry.Target.BoardId))
            throw new DfuArtifactResolutionException("The selected manifest release does not match the explicit DFU platform and board selection.");
        var source = entry.Artifact.DownloadUri;
        if (!IsTrustedOfficialSource(source)) throw new DfuArtifactResolutionException("Sibling derivation is allowed only from a configured official HTTPS firmware source.");
        var fileName = entry.Target.VehicleType switch
        {
            FirmwareVehicleType.Copter => "arducopter_with_bl.hex",
            FirmwareVehicleType.Plane => "arduplane_with_bl.hex",
            FirmwareVehicleType.Rover => "ardurover_with_bl.hex",
            FirmwareVehicleType.Sub => "ardusub_with_bl.hex",
            _ => throw new DfuArtifactResolutionException("This vehicle family has no approved official DFU sibling naming rule.")
        };
        var sibling = new Uri(source, fileName);
        if (!SameDirectory(source, sibling)) throw new DfuArtifactResolutionException("The official sibling URI escaped its platform/version directory.");
        return await downloader.DownloadAsync(sibling, entry.Target.Platform, entry.Target.BoardId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DfuArtifact> ResolveLocalAsync(DfuInstallationRequest request, CancellationToken cancellationToken)
    {
        string path;
        try { path = Path.GetFullPath(request.LocalHexPath!); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new DfuArtifactResolutionException("The local Intel HEX path is invalid.", exception);
        }
        if (!string.Equals(Path.GetExtension(path), ".hex", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new DfuArtifactResolutionException("Select an existing local .hex file.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var metadata = await inspector.InspectAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!Path.GetFileName(path).EndsWith("_with_bl.hex", StringComparison.OrdinalIgnoreCase))
        {
            metadata = metadata with
            {
                Warnings = metadata.Warnings.Concat(["The local filename does not identify a combined application-and-bootloader package; range evidence and explicit confirmation are required."]).ToArray()
            };
        }
        return new DfuArtifact(Path.GetFileName(path), path, metadata, Platform: request.SelectedPlatform, BoardId: request.SelectedBoardId);
    }

    private bool IsTrustedOfficialSource(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !options.Value.OfficialFirmwareHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase)) return false;
        var decoded = Uri.UnescapeDataString(uri.AbsolutePath);
        return !decoded.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..");
    }

    private static bool SameDirectory(Uri source, Uri sibling)
    {
        var sourceDirectory = new Uri(source, ".");
        var siblingDirectory = new Uri(sibling, ".");
        return sourceDirectory == siblingDirectory && source.Scheme == sibling.Scheme && source.IdnHost == sibling.IdnHost;
    }
}
