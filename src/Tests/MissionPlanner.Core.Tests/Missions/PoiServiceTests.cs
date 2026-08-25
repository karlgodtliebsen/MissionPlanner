using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Planning;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Missions;

public sealed class PoiServiceTests
{
    [Fact]
    public async Task AddEditDelete_PersistsAcrossServiceRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poi-{Guid.NewGuid():N}.json");
        try
        {
            var provider = Substitute.For<IJsonPoiFilePathProvider>();
            provider.GetPath().Returns(path);

            var logger1 = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<JsonPoiRepository>();
            var logger2 = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PoiService>();
            var first = new PoiService(new JsonPoiRepository(provider, logger1), logger2);
            await first.ActivateAsync(TestContext.Current.CancellationToken);
            var item = await first.AddAsync("Site", new(56, 10), 42, "Initial", "Survey", TestContext.Current.CancellationToken);
            await first.UpdateAsync(item with
            {
                Description = "Updated"
            }, TestContext.Current.CancellationToken);
            var second = new PoiService(new JsonPoiRepository(provider, logger1), logger2);
            await second.ActivateAsync(TestContext.Current.CancellationToken);
            second.Snapshot.Items.Single().Description.Should().Be("Updated");
            await second.DeleteAsync(item.Id, TestContext.Current.CancellationToken);
            second.Snapshot.Items.Should().BeEmpty();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Repository_IsolatesCorruptFiles()
    {
        var provider = Substitute.For<IJsonPoiFilePathProvider>();
        provider.GetPath().Returns(Path.Combine(Path.GetTempPath(), $"poi-{Guid.NewGuid():N}.json"));

        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<JsonPoiRepository>();
        var path = provider.GetPath();

        await File.WriteAllTextAsync(path, "not-json", TestContext.Current.CancellationToken);
        var result = await new JsonPoiRepository(provider, logger).LoadAsync(TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
        Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*").Should().NotBeEmpty();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*"))
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Add_RejectsInvalidCoordinatesButAllowsDuplicateNames()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PoiService>();
        var repository = new MemoryRepository();
        var service = new PoiService(repository, logger);
        await service.ActivateAsync(TestContext.Current.CancellationToken);
        await service.AddAsync("Same", new(56, 10), null, null, null, TestContext.Current.CancellationToken);
        await service.AddAsync("Same", new(56.1, 10.1), null, null, null, TestContext.Current.CancellationToken);
        service.Snapshot.Items.Should().HaveCount(2);
        var action = () => service.AddAsync("Bad", new(100, 10), null, null, null, TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<ArgumentException>();
    }
    private sealed class MemoryRepository : IPoiRepository
    {
        public IReadOnlyList<PointOfInterest> Items { get; private set; } = [];
        public Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items);
        }

        public Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default)
        {
            Items = items;
            return Task.CompletedTask;
        }
    }
}
