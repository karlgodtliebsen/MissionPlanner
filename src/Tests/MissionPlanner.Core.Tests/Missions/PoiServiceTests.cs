using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests;

public sealed class PoiServiceTests
{
    [Fact]
    public async Task AddEditDelete_PersistsAcrossServiceRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poi-{Guid.NewGuid():N}.json");
        try
        {
            var first = new PoiService(new JsonPoiRepository(path)); await first.InitializeAsync(TestContext.Current.CancellationToken);
            var item = await first.AddAsync("Site", new(56, 10), 42, "Initial", "Survey", TestContext.Current.CancellationToken);
            await first.UpdateAsync(item with { Description = "Updated" }, TestContext.Current.CancellationToken);
            var second = new PoiService(new JsonPoiRepository(path)); await second.InitializeAsync(TestContext.Current.CancellationToken);
            second.Snapshot.Items.Single().Description.Should().Be("Updated");
            await second.DeleteAsync(item.Id, TestContext.Current.CancellationToken); second.Snapshot.Items.Should().BeEmpty();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Repository_IsolatesCorruptFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poi-{Guid.NewGuid():N}.json"); await File.WriteAllTextAsync(path, "not-json", TestContext.Current.CancellationToken);
        var result = await new JsonPoiRepository(path).LoadAsync(TestContext.Current.CancellationToken);
        result.Should().BeEmpty(); Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*").Should().NotBeEmpty();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*")) File.Delete(file);
    }

    [Fact]
    public async Task Add_RejectsInvalidCoordinatesButAllowsDuplicateNames()
    {
        var repository = new MemoryRepository(); var service = new PoiService(repository); await service.InitializeAsync(TestContext.Current.CancellationToken);
        await service.AddAsync("Same", new(56,10), null, null, null, TestContext.Current.CancellationToken);
        await service.AddAsync("Same", new(56.1,10.1), null, null, null, TestContext.Current.CancellationToken);
        service.Snapshot.Items.Should().HaveCount(2);
        var action = () => service.AddAsync("Bad", new(100,10), null, null, null, TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<ArgumentException>();
    }
    private sealed class MemoryRepository : IPoiRepository
    { public IReadOnlyList<PointOfInterest> Items { get; private set; } = []; public Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items); public Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default) { Items = items; return Task.CompletedTask; } }
}
