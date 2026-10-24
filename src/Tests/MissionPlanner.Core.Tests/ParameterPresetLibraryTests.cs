using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MissionPlanner.Core.ConfigTuning.Profiles;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies immutable preset import and refusal of ambiguous or invalid parameter values.</summary>
public sealed class ParameterPresetLibraryTests
{
    [Theory]
    [InlineData("A,NaN")]
    [InlineData("A,Infinity")]
    [InlineData("A,1\nA,2")]
    [InlineData("A,1,2")]
    [InlineData("../x,1")]
    [InlineData("A,1e100")]
    public void InvalidPresetsAreRejected(string text) => Assert.Throws<InvalidDataException>(() => ParameterPresetLibrary.Parse(text));

    [Fact]
    public void CommentsAndScientificNotationRemainInvariant()
    {
        var parsed = ParameterPresetLibrary.Parse("# frame notes\nATC_RAT_RLL_P,1.25e-1 # comment\nMOT_THST_EXPO\t0.65\n");
        Assert.Equal(.125, parsed["ATC_RAT_RLL_P"]);
        Assert.Equal(.65, parsed["MOT_THST_EXPO"]);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "The Git blob identity protocol requires SHA-1; this test does not use it for authentication.")]
    public async Task DownloadPinsBlobIdentityAndDoesNotInferFirmwareCompatibility()
    {
        var bytes = Encoding.UTF8.GetBytes("ATC_RAT_RLL_P,0.1\n");
        var sha = Convert.ToHexString(SHA1.HashData(Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0").Concat(bytes).ToArray())).ToLowerInvariant();
        var handler = new ReplyHandler(JsonSerializer.Serialize(new { encoding = "base64", content = Convert.ToBase64String(bytes) }));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ParameterPresets").Returns(_ => new HttpClient(handler, false));
        var library = new ParameterPresetLibrary(factory);
        var entry = new ParameterPresetEntry("test.param", "Tools/Frame_params/test.param", sha, false);
        var profile = await library.DownloadAsync(entry, TestContext.Current.CancellationToken);
        Assert.Equal("https://api.github.com/repos/ArduPilot/ardupilot/git/blobs/" + sha, handler.RequestUri);
        Assert.Null(profile.FirmwareFamily);
        Assert.Equal(.1, profile.Values["ATC_RAT_RLL_P"]);
        await Assert.ThrowsAsync<InvalidDataException>(() => library.DownloadAsync(entry with { Sha = new string('0', 40) }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => library.ListAsync("Tools/Frame_params/../secret", TestContext.Current.CancellationToken));
    }

    private sealed class ReplyHandler(string json) : HttpMessageHandler
    {
        internal string? RequestUri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}
