using MissionPlanner.Library.Browser;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class BrowserPlannerSecretStoreTests
{
    [Fact]
    public async Task SecretsAreIsolatedToAnApplicationInstance()
    {
        var store = new BrowserPlannerSecretStore();
        await store.SetAsync("token", "first", TestContext.Current.CancellationToken);
        await store.SetAsync("token", "replacement", TestContext.Current.CancellationToken);
        Assert.Equal("replacement", await store.GetAsync("token", TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync("TOKEN", TestContext.Current.CancellationToken));
        Assert.Null(await new BrowserPlannerSecretStore().GetAsync("token", TestContext.Current.CancellationToken));
        await store.RemoveAsync("token", TestContext.Current.CancellationToken);
        Assert.Null(await store.GetAsync("token", TestContext.Current.CancellationToken));
        await store.RemoveAsync("token", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancelledWritesAndRemovalsLeaveSecretsUnchanged()
    {
        var store = new BrowserPlannerSecretStore();
        await store.SetAsync("token", "original", TestContext.Current.CancellationToken);
        var cancelled = new CancellationToken(true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.SetAsync("token", "new", cancelled));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.RemoveAsync("token", cancelled));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.GetAsync("token", cancelled));
        Assert.Equal("original", await store.GetAsync("token", TestContext.Current.CancellationToken));
    }
}
