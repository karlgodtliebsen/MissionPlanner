namespace MissionPlanner.Maps.Offline;

/// <summary>Installs offline map packs through isolated staging and atomic directory promotion.</summary>
public sealed class OfflineMapPackInstaller(FileOfflineMapPackRepository repository, IOfflineMapPackValidator validator) : IOfflineMapPackInstaller
{
    /// <inheritdoc />
    public async ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(archive);
        var destination = repository.GetVersionPath(manifest.Id, manifest.Version);
        if (Directory.Exists(destination))
            throw new InvalidOperationException($"Offline map pack '{manifest.Id}' version '{manifest.Version}' is already installed.");

        Directory.CreateDirectory(repository.RootPath);
        var staging = Path.Combine(repository.RootPath, $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var archivePath = Path.Combine(staging, manifest.ArchiveFileName);
            await using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await archive.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await validator.ValidateAsync(manifest, archivePath, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(staging, FileOfflineMapPackRepository.ManifestFileName), OfflineMapPackJson.Serialize(manifest), cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            File.SetAttributes(Path.Combine(destination, manifest.ArchiveFileName), FileAttributes.ReadOnly);
            return new(manifest, destination, Path.Combine(destination, manifest.ArchiveFileName));
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }
}
