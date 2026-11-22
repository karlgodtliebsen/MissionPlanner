using Avalonia;
using Avalonia.Headless;
using Material.Icons;
using Material.Icons.Avalonia;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks that icon geometry does not leak between headless UI dispatchers.</summary>
[Collection("Document rendering")]
public sealed class MaterialIconSessionTests
{
    /// <summary>Creates a headless application with the production drawing backend.</summary>
    /// <returns>The configured headless application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>The same icon can be measured on each session's UI thread.</summary>
    /// <returns>A task that completes after both sessions have exercised the icon.</returns>
    [Fact]
    public async Task IconsCanBeMeasuredOnDifferentUiThreads()
    {
        // Keep both session threads alive so the thread pool cannot reuse the first thread.
        // Dispatch sequentially because Avalonia's platform services are process-global.
        await using var firstSession = HeadlessUnitTestSession.StartNew(typeof(MaterialIconSessionTests));
        await using var secondSession = HeadlessUnitTestSession.StartNew(typeof(MaterialIconSessionTests));
        var firstThread = await firstSession.Dispatch(() =>
        {
            // Hidden icons can populate the cache before layout measures them.
            var icon = new MaterialIcon { Kind = MaterialIconKind.OpenInNew };
            Assert.NotNull(icon.Drawing.Geometry);
            return Environment.CurrentManagedThreadId;
        }, TestContext.Current.CancellationToken);

        await secondSession.Dispatch(() =>
        {
            Assert.NotEqual(firstThread, Environment.CurrentManagedThreadId);
            var icon = new MaterialIcon { Kind = MaterialIconKind.OpenInNew };
            Assert.NotNull(icon.Drawing.Geometry);
            icon.Drawing.Geometry.VerifyAccess();
            Assert.True(icon.Drawing.Geometry.Bounds.Width > 0);
            Assert.True(icon.Drawing.Geometry.Bounds.Height > 0);
        }, TestContext.Current.CancellationToken);
    }
}
