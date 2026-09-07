using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.Core.Tests;

public sealed class AdvancedAvailabilityTests
{
    private readonly AdvancedAvailabilityService service = new();
    private static readonly AdvancedPlatformCapabilities desktop = new("Windows", FileOpen: true, FileSave: true,
        SerialOutput: true, NetworkOutput: true, Location: true, LocationPermissionGranted: true, SecureKeyStorage: true);
    private static readonly AdvancedSessionState connected = new(true, true, true);

    [Fact]
    public void CatalogueHasExactlyThirteenStableUniqueEntries()
    {
        var all = AdvancedFeatureCatalog.All;
        Assert.Equal(13, all.Count);
        Assert.Equal(Enumerable.Range(1, 13), all.Select(item => item.SortOrder));
        Assert.Equal(13, all.Select(item => item.Title).Distinct().Count());
        Assert.Equal(13, all.Select(item => item.Route).Distinct().Count());
        Assert.All(all, item => Assert.False(string.IsNullOrWhiteSpace(item.Help)));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void OutputRequiresAnAuditedBridgeInBrowser(bool browser, bool bridge, bool available)
    {
        var platform = desktop with { IsBrowser = browser, AuditedOutputBridge = bridge };
        var result = Evaluate(AdvancedFeatureId.Mirror, platform, connected);
        Assert.Equal(available, result.CanLaunch);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void ConnectionAndVehicleRequirementsAreDistinct()
    {
        Assert.Equal(AdvancedAvailabilityState.ConnectionRequired,
            Evaluate(AdvancedFeatureId.Proximity, desktop, new(false, false, false)).State);
        Assert.Equal(AdvancedAvailabilityState.VehicleRequired,
            Evaluate(AdvancedFeatureId.Proximity, desktop, new(true, false, false)).State);
        Assert.True(Evaluate(AdvancedFeatureId.Proximity, desktop, connected).CanLaunch);
    }

    [Fact]
    public void LocationPermissionMustBeGrantedEvenWithConnectedVehicle()
    {
        Assert.Equal(AdvancedAvailabilityState.PermissionRequired,
            Evaluate(AdvancedFeatureId.FollowMe, desktop with { LocationPermissionGranted = false }, connected).State);
        Assert.Equal(AdvancedAvailabilityState.UnsupportedPlatform,
            Evaluate(AdvancedFeatureId.FollowMe, desktop with { Location = false }, connected).State);
        Assert.True(Evaluate(AdvancedFeatureId.FollowMe, desktop, connected).CanLaunch);
    }

    [Fact]
    public void SessionKeysAreExplicitAndFilesRequireBothCapabilities()
    {
        var browser = desktop with { SecureKeyStorage = false, SessionKeyStorage = true };
        Assert.Contains("session-only", Evaluate(AdvancedFeatureId.Signing, browser, connected).Reason);
        Assert.False(Evaluate(AdvancedFeatureId.Signing, browser with { SessionKeyStorage = false }, connected).CanLaunch);
        Assert.False(Evaluate(AdvancedFeatureId.AnonymousLogs, desktop with { FileSave = false }, connected).CanLaunch);
        Assert.False(Evaluate(AdvancedFeatureId.AnonymousLogs, desktop with { FileOpen = false }, connected).CanLaunch);
    }

    [Fact]
    public void ParametersGateOnlyToolsWhichRequireThem()
    {
        var feature = AdvancedFeatureCatalog.All[0] with { Requirements = AdvancedRequirement.Parameters };
        Assert.Equal(AdvancedAvailabilityState.TemporarilyUnavailable,
            service.Evaluate(feature, desktop, connected with { ParametersLoaded = false }).State);
        Assert.True(service.Evaluate(feature, desktop, connected).CanLaunch);
    }

    [Fact]
    public void CapabilityChangesPublishOnlyNewEvidence()
    {
        var source = new AdvancedPlatformCapabilitySource();
        var count = 0;
        source.Changed += _ => count++;
        source.Update(desktop);
        source.Update(desktop with { });
        source.Update(desktop with { LocationPermissionGranted = false });
        Assert.Equal(2, count);
    }

    private AdvancedAvailability Evaluate(AdvancedFeatureId id, AdvancedPlatformCapabilities platform, AdvancedSessionState state)
        => service.Evaluate(AdvancedFeatureCatalog.All.Single(item => item.Id == id), platform, state);

    [Fact]
    public async Task TenToolLifetimesCancelAndReleaseAllOwnedResourcesExactlyOnce()
    {
        var count = 0;
        for (var index = 0; index < 10; index++)
        {
            var lifetime = new AdvancedToolLifetime();
            Assert.False(lifetime.Token.IsCancellationRequested);
            count++;
            lifetime.OnClosing(() =>
            {
                Assert.True(lifetime.Token.IsCancellationRequested);
                count--;
                return ValueTask.CompletedTask;
            });
            await lifetime.DisposeAsync();
            await lifetime.DisposeAsync();
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task CleanupContinuesAfterFailureAndRejectsLateOwnership()
    {
        var lifetime = new AdvancedToolLifetime();
        var cleaned = false;
        lifetime.OnClosing(() =>
        {
            cleaned = true;
            return ValueTask.CompletedTask;
        });
        lifetime.OnClosing(() => throw new InvalidOperationException("test failure"));
        await Assert.ThrowsAsync<AggregateException>(() => lifetime.DisposeAsync().AsTask());
        Assert.True(cleaned);
        Assert.Throws<ObjectDisposedException>(() => lifetime.OnClosing(() => ValueTask.CompletedTask));
    }
}
