using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;

namespace MissionPlanner.Firmware.Tests;

public sealed class HttpFirmwareManifestClientTests
{
    [Fact]
    public async Task RejectsStreamingResponseBeyondCompressedManifestBound()
    {
        var client = new HttpFirmwareManifestClient(
            new HttpClient(new Handler(new byte[11])),
            Options.Create(new FirmwareOptions { MaximumManifestDownloadBytes = 10 }));

        var act = async () => await client.GetAsync(
            new Uri("https://firmware.ardupilot.org/manifest.json.gz"),
            null,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareManifestException>();
    }

    [Fact]
    public async Task AcceptsBoundedManifestResponse()
    {
        var bytes = "{\"firmware\":[]}"u8.ToArray();
        var client = new HttpFirmwareManifestClient(
            new HttpClient(new Handler(bytes)),
            Options.Create(new FirmwareOptions { MaximumManifestDownloadBytes = 1024 }));

        var result = await client.GetAsync(
            new Uri("https://firmware.ardupilot.org/manifest.json.gz"),
            null,
            TestContext.Current.CancellationToken);

        result.Content.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new HttpFirmwareManifestClient(new HttpClient(new Handler([])), Options.Create(new FirmwareOptions()));
        var act = () => client.GetAsync(new Uri("https://firmware.ardupilot.org/manifest.json.gz"), null, cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class Handler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
