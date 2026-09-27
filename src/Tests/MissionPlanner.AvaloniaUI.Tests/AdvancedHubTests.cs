using MissionPlanner.App.Views.InitSetup.Advanced;
using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class AdvancedHubTests
{
    [Fact]
    public void RegistryRejectsDuplicateUnknownAndMissingTargets()
    {
        var entry = new AdvancedToolRegistration(AdvancedFeatureId.Warnings, () => null!);
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry, entry]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry with { CreatePage = null! }]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry with { Id = (AdvancedFeatureId)99 }]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([]).Create("SetupAdvanced/Warnings"));
        Assert.Throws<InvalidOperationException>(() => new AdvancedToolRegistry([entry]).Create("SetupAdvanced/Warnings"));
    }

}
