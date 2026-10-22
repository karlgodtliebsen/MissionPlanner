using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.Desktop;
using MissionPlanner.Library.Desktop.Configuration;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class DesktopPlatformServicesTests
{
    [Fact]
    public async Task SessionSecretsRoundTripAndAreRemoved()
    {
        IPlannerSecretStore store = new SessionPlannerSecretStore();

        Assert.Null(await store.GetAsync("key", TestContext.Current.CancellationToken));
        await store.SetAsync("key", "first", TestContext.Current.CancellationToken);
        await store.SetAsync("key", "second", TestContext.Current.CancellationToken);
        Assert.Equal("second", await store.GetAsync("key", TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync("KEY", TestContext.Current.CancellationToken));

        await store.RemoveAsync("key", TestContext.Current.CancellationToken);
        Assert.Null(await store.GetAsync("key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnavailableLocationNeverReportsAPositionAndHonoursCancellation()
    {
        var service = new UnavailablePlatformLocationService();

        Assert.Null(await service.GetLocationAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.GetLocationAsync(new CancellationToken(true)));
    }

    [Fact]
    public void CapabilitiesReportThePlatformAndWhatItSupports()
    {
        var capabilities = new DesktopAdvancedCapabilities("Linux", location: false, secureKeyStorage: true);

        var current = capabilities.Current;
        Assert.Equal("Linux", current.Platform);
        Assert.False(current.Location);
        Assert.True(current.SecureKeyStorage);
        Assert.True(current.SerialOutput);
        Assert.True(current.SessionKeyStorage);
    }

    [Fact]
    public void DesktopServicesRegisterTheSharedImplementations()
    {
        var services = new ServiceCollection().AddDesktopServices();

        Assert.Equal(typeof(DesktopOutputSinkFactory), services.Single(item => item.ServiceType == typeof(IOutputSinkFactory)).ImplementationType);
        Assert.Equal(typeof(DesktopWarningRuleStore), services.Single(item => item.ServiceType == typeof(IWarningRuleStore)).ImplementationType);
    }
}
