using MissionPlanner.App.Services;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SimulationDocumentStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mp-store-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScenarioPresetsRoundTripThroughAFile()
    {
        var path = Path.Combine(directory, "nested", "presets.json");
        var store = new JsonSimulationScenarioPresetStore(path);

        Assert.Null(await store.ReadAsync(TestContext.Current.CancellationToken));
        await store.WriteAsync("""{"v":1}""", TestContext.Current.CancellationToken);
        await store.WriteAsync("""{"v":2}""", TestContext.Current.CancellationToken);

        Assert.Equal("""{"v":2}""", await new JsonSimulationScenarioPresetStore(path).ReadAsync(TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task SimulatorProfilesTreatABlankFileAsMissing()
    {
        var path = Path.Combine(directory, "profiles.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "  ", TestContext.Current.CancellationToken);

        Assert.Null(await new JsonSimulatorProfileStore(path).ReadAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
