using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MissionPlanner.Maps.Offline;
using NSubstitute;

using MissionPlanner.Library.Windows.Maps;

namespace MissionPlanner.AvaloniaUI.Tests.Maps;

public sealed class OfflineMapPackTests
{
    [Fact]
    public async Task Validator_AcceptsValidRasterMbTilesAndRejectsHashMismatch()
    {
        var context = await CreateContextAsync();
        try
        {
            var validator = new MbTilesOfflineMapPackValidator();
            await validator.ValidateAsync(context.Manifest, context.ArchivePath, TestContext.Current.CancellationToken);
            var invalid = context.Manifest with { Sha256 = new string('0', 64) };
            var action = () => validator.ValidateAsync(invalid, context.ArchivePath, TestContext.Current.CancellationToken).AsTask();
            await action.Should().ThrowAsync<InvalidDataException>();
        }
        finally { DeleteTree(context.Root); }
    }

    [Fact]
    public async Task Validator_RejectsCorruptDatabaseAndTraversalManifest()
    {
        var context = await CreateContextAsync();
        try
        {
            var corrupt = Path.Combine(context.Root, "corrupt.mbtiles");
            await File.WriteAllBytesAsync(corrupt, [1, 2, 3], TestContext.Current.CancellationToken);
            var bytes = await File.ReadAllBytesAsync(corrupt, TestContext.Current.CancellationToken);
            var manifest = context.Manifest with { ArchiveFileName = "corrupt.mbtiles", SizeBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            var validator = new MbTilesOfflineMapPackValidator();
            var corruptAction = () => validator.ValidateAsync(manifest, corrupt, TestContext.Current.CancellationToken).AsTask();
            await corruptAction.Should().ThrowAsync<SqliteException>();
            var traversalAction = () => validator.ValidateAsync(context.Manifest with { ArchiveFileName = "../pack.mbtiles" }, context.ArchivePath, TestContext.Current.CancellationToken).AsTask();
            await traversalAction.Should().ThrowAsync<InvalidDataException>();
        }
        finally { DeleteTree(context.Root); }
    }

    [Fact]
    public async Task Installer_PromotesAtomicallyListsAndRejectsDuplicateVersion()
    {
        var context = await CreateContextAsync();
        var installRoot = Path.Combine(Path.GetTempPath(), $"mp-pack-install-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileOfflineMapPackRepository(installRoot);
            var installer = new OfflineMapPackInstaller(repository, new MbTilesOfflineMapPackValidator());
            await using var stream = File.OpenRead(context.ArchivePath);
            var installed = await installer.InstallAsync(context.Manifest, stream, TestContext.Current.CancellationToken);
            installed.ArchivePath.Should().Contain(Path.Combine("Maps", "Packs", "test-pack", "1.0"));
            (await repository.ListAsync(TestContext.Current.CancellationToken)).Should().ContainSingle();
            Directory.EnumerateDirectories(repository.RootPath, ".staging-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();

            await using var duplicateStream = File.OpenRead(context.ArchivePath);
            var duplicate = () => installer.InstallAsync(context.Manifest, duplicateStream, TestContext.Current.CancellationToken).AsTask();
            await duplicate.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { DeleteTree(context.Root); DeleteTree(installRoot); }
    }

    [Fact]
    public async Task Repository_PreventsRemovingActivePackThenRemovesInactivePack()
    {
        var context = await CreateContextAsync();
        var installRoot = Path.Combine(Path.GetTempPath(), $"mp-pack-install-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileOfflineMapPackRepository(installRoot);
            var installer = new OfflineMapPackInstaller(repository, new MbTilesOfflineMapPackValidator());
            await using var stream = File.OpenRead(context.ArchivePath);
            await installer.InstallAsync(context.Manifest, stream, TestContext.Current.CancellationToken);
            var activeRemoval = () => repository.RemoveAsync("test-pack", "1.0", "test-pack", TestContext.Current.CancellationToken).AsTask();
            await activeRemoval.Should().ThrowAsync<InvalidOperationException>();
            await repository.RemoveAsync("test-pack", "1.0", cancellationToken: TestContext.Current.CancellationToken);
            (await repository.ListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }
        finally { DeleteTree(context.Root); DeleteTree(installRoot); }
    }

    /// <summary>Verifies the common installer rejects truncated and oversized streams before validation.</summary>
    [Fact]
    public async Task InstallerBoundsDeclaredSizeDuringStreaming()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mp-pack-bounds-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileOfflineMapPackRepository(root);
            var validator = Substitute.For<IOfflineMapPackValidator>();
            var installer = new OfflineMapPackInstaller(repository, validator);
            var manifest = new OfflineMapPackManifest("bounded", "1", "Bounded", "pack.mbtiles", Convert.ToHexString(SHA256.HashData([1, 2, 3])), 3, new(0, 0, 1, 1), 0, 1, "EPSG:3857", "png", "Attribution", "License");

            await FluentActions.Awaiting(() => installer.InstallAsync(manifest, new MemoryStream([1, 2]), TestContext.Current.CancellationToken).AsTask()).Should().ThrowAsync<InvalidDataException>();
            await FluentActions.Awaiting(() => installer.InstallAsync(manifest, new MemoryStream([1, 2, 3, 4]), TestContext.Current.CancellationToken).AsTask()).Should().ThrowAsync<InvalidDataException>();
            Directory.EnumerateDirectories(repository.RootPath, ".staging-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
            await validator.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default!, TestContext.Current.CancellationToken);
        }
        finally { DeleteTree(root); }
    }

    /// <summary>Verifies one corrupt manifest is isolated while valid packs remain enumerable.</summary>
    [Fact]
    public async Task RepositorySkipsCorruptManifest()
    {
        var context = await CreateContextAsync();
        var root = Path.Combine(Path.GetTempPath(), $"mp-pack-corrupt-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileOfflineMapPackRepository(root);
            await using var stream = File.OpenRead(context.ArchivePath);
            await new OfflineMapPackInstaller(repository, new MbTilesOfflineMapPackValidator()).InstallAsync(context.Manifest, stream, TestContext.Current.CancellationToken);
            var corruptDirectory = Path.Combine(repository.RootPath, "corrupt", "1");
            Directory.CreateDirectory(corruptDirectory);
            await File.WriteAllTextAsync(Path.Combine(corruptDirectory, FileOfflineMapPackRepository.ManifestFileName), "{broken", TestContext.Current.CancellationToken);

            (await repository.ListAsync(TestContext.Current.CancellationToken)).Should().ContainSingle();
            repository.LastDiagnostics.Should().ContainSingle();
        }
        finally { DeleteTree(context.Root); DeleteTree(root); }
    }

    /// <summary>Verifies active ownership denies ordinary removal and explicit force switches first.</summary>
    [Fact]
    public async Task ManagerOwnsActivePackRemoval()
    {
        var repository = Substitute.For<IOfflineMapPackRepository>();
        var active = new TestActiveSource("pack:test:1");
        var manager = new OfflineMapPackManager(Substitute.For<IOfflineMapPackInstaller>(), repository, active);
        await FluentActions.Awaiting(() => manager.RemoveAsync("test", "1", TestContext.Current.CancellationToken).AsTask()).Should().ThrowAsync<InvalidOperationException>();

        await manager.ForceRemoveAsync("test", "1", cancellationToken: TestContext.Current.CancellationToken);

        active.SelectedSourceId.Should().Be("osm-standard");
        await repository.Received().RemoveAsync("test", "1", null, Arg.Any<CancellationToken>());
    }

    private static async Task<TestContextData> CreateContextAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mp-mbtiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archive = Path.Combine(root, "pack.mbtiles");
        await using (var connection = new SqliteConnection($"Data Source={archive};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE metadata (name TEXT, value TEXT); CREATE TABLE tiles (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_data BLOB); INSERT INTO metadata VALUES ('format','png'),('type','baselayer'),('bounds','0,0,1,1'),('minzoom','0'),('maxzoom','1'); INSERT INTO tiles VALUES (0,0,0,$tile);";
            command.Parameters.AddWithValue("$tile", new byte[] { 0x89, 0x50, 0x4E, 0x47, 1 });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var bytes = await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken);
        var manifest = new OfflineMapPackManifest("test-pack", "1.0", "Test Pack", "pack.mbtiles", Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, new(0, 0, 1, 1), 0, 1, "EPSG:3857", "png", "Test attribution", "Test license");
        return new(root, archive, manifest);
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, true);
    }

    private sealed record TestContextData(string Root, string ArchivePath, OfflineMapPackManifest Manifest);

    private sealed class TestActiveSource(string sourceId) : IActiveMapSourceStore
    {
        public string SelectedSourceId { get; private set; } = sourceId;
        public ValueTask SetSelectedSourceIdAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            SelectedSourceId = sourceId;
            return ValueTask.CompletedTask;
        }
    }
}
