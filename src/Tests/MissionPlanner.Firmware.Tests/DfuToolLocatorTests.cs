using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuToolLocatorTests
{
    [Fact]
    public async Task ConfiguredMissingPathIsReportedWithoutExecutingAnything()
    {
        var runner = new FakeProcessRunner(Success("2.20.0"));
        var locator = CreateLocator([new DfuToolCandidate("missing.exe", DfuToolDiscoverySource.UserConfigured, false)], runner);

        var result = await locator.LocateAsync(TestContext.Current.CancellationToken);

        result.Availability.Should().Be(DfuToolAvailability.PathInvalid);
        runner.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidCandidateIsProbedDirectlyAndReturned()
    {
        var runner = new FakeProcessRunner(Success("STM32CubeProgrammer version 2.20.0"));
        var locator = CreateLocator([new DfuToolCandidate(@"C:\ST\STM32_Programmer_CLI.exe", DfuToolDiscoverySource.KnownInstallation, true)], runner);

        var result = await locator.LocateAsync(TestContext.Current.CancellationToken);

        result.Availability.Should().Be(DfuToolAvailability.Available);
        result.Version.Should().Be(new Version(2, 20, 0));
        runner.Requests.Should().ContainSingle().Which.Arguments.Should().Equal("--version");
    }

    [Fact]
    public async Task FileVersionAvoidsDependingOnLocalizedProbeText()
    {
        var runner = new FakeProcessRunner(Success("Version installée"));
        var locator = CreateLocator([
            new DfuToolCandidate("tool.exe", DfuToolDiscoverySource.Registry, true, new Version(2, 18, 0, 0))], runner);

        var result = await locator.LocateAsync(TestContext.Current.CancellationToken);

        result.Availability.Should().Be(DfuToolAvailability.Available);
        result.Version.Should().Be(new Version(2, 18, 0, 0));
    }

    [Fact]
    public async Task OldVersionIsDistinguishedFromExecutionFailure()
    {
        var oldLocator = CreateLocator([new DfuToolCandidate("old.exe", DfuToolDiscoverySource.Path, true)], new FakeProcessRunner(Success("2.8.0")));
        var blockedLocator = CreateLocator([new DfuToolCandidate("blocked.exe", DfuToolDiscoverySource.Path, true)],
            new FakeProcessRunner(new DfuProcessResult(5, [], FailureCode: "ExitCode")));

        var old = await oldLocator.LocateAsync(TestContext.Current.CancellationToken);
        var blocked = await blockedLocator.LocateAsync(TestContext.Current.CancellationToken);

        old.Availability.Should().Be(DfuToolAvailability.UnsupportedVersion);
        blocked.Availability.Should().Be(DfuToolAvailability.ExecutionBlocked);
    }

    [Fact]
    public async Task NoCandidatesReportsNotInstalled()
    {
        var result = await CreateLocator([], new FakeProcessRunner(Success("2.20")))
            .LocateAsync(TestContext.Current.CancellationToken);

        result.Availability.Should().Be(DfuToolAvailability.NotInstalled);
    }

    private static Stm32CubeProgrammerToolLocator CreateLocator(IReadOnlyList<DfuToolCandidate> candidates, FakeProcessRunner runner) =>
        new(new FakeDiscoverySource(candidates), runner, Options.Create(new DfuOptions()));

    private static DfuProcessResult Success(string text) =>
        new(0, [new DfuProcessOutput(DateTimeOffset.UtcNow, false, text)]);

    private sealed class FakeDiscoverySource(IReadOnlyList<DfuToolCandidate> candidates) : IDfuToolDiscoverySource
    {
        public IReadOnlyList<DfuToolCandidate> Discover() => candidates;
    }

    private sealed class FakeProcessRunner(DfuProcessResult result) : IDfuProcessRunner
    {
        public List<DfuProcessRequest> Requests { get; } = [];

        public Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
