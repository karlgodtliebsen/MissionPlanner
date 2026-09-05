using FluentAssertions;
using MissionPlanner.Library.Browser.Maps;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class BrowserOfflineMapsTests
{
    [Fact]
    public async Task ImportIsRejectedWithoutReadingArchive()
    {
        var maps = new BrowserOfflineMaps();
        using var archive = new MemoryStream([1, 2, 3]);
        var action = async () => await maps.InstallAsync(null!, archive);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage(BrowserOfflineMaps.UnsupportedMessage);
        archive.Position.Should().Be(0);
        archive.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task CancelledImportPreservesCancellation()
    {
        var action = async () => await new BrowserOfflineMaps().InstallAsync(null!, Stream.Null, new CancellationToken(true));
        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
