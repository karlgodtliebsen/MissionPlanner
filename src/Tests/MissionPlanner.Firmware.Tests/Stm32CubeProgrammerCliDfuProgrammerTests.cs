using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class Stm32CubeProgrammerCliDfuProgrammerTests
{
    [Fact]
    public void CommandBuilderSelectsUsbIndexAndMakesVerificationImmediateAndMandatory()
    {
        var request = new Stm32CubeProgrammerCommandBuilder().BuildProgramAndVerify(
            ToolPath(), 2, Path.Combine(Path.GetTempPath(), "firmware.hex"), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        request.Arguments.Should().Equal("-c", "port=usb2", "-w", Path.Combine(Path.GetTempPath(), "firmware.hex"), "-v");
        request.Purpose.Should().Be(DfuProcessPurpose.ProgramAndVerify);
        request.MayKillProcessTreeOnCancellation.Should().BeFalse();
    }

    [Theory]
    [InlineData("2.14.0", "File download complete\n  42%\nDownload verified successfully")]
    [InlineData("2.23.0", "Programming complete 42.5 %\nVerification completed successfully")]
    public async Task SuccessfulFixturesRequireProgrammingAndVerificationEvidence(string version, string fixture)
    {
        var runner = new FakeRunner(Result(0, fixture));
        var provider = CreateProvider(runner, Version.Parse(version));
        using var artifact = ValidArtifact();
        var progress = new ProgressRecorder();

        var result = await provider.ProgramAndVerifyAsync(new DfuProgrammingRequest(Device(2), artifact.Artifact), progress,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DfuProgrammingOutcome.Succeeded);
        result.ProgrammingSucceeded.Should().BeTrue();
        result.VerificationSucceeded.Should().BeTrue();
        result.ProviderLog.Should().ContainEquivalentOf("verif");
        runner.Requests.Should().ContainSingle().Which.Arguments.Should().ContainInOrder("-w", artifact.Path, "-v");
        progress.Items.Should().Contain(item => item.Percentage == 42 || item.Percentage == 42.5);
    }

    [Fact]
    public async Task SuccessfulWriteWithoutVerificationProofFailsConservatively()
    {
        var provider = CreateProvider(new FakeRunner(Result(0, "File download complete")));
        using var artifact = ValidArtifact();

        var result = await provider.ProgramAndVerifyAsync(new DfuProgrammingRequest(Device(1), artifact.Artifact),
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DfuProgrammingOutcome.VerificationFailed);
        result.ProgrammingSucceeded.Should().BeFalse();
        result.VerificationSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task ExplicitVerifyFailureOverridesSuccessfulWrite()
    {
        var provider = CreateProvider(new FakeRunner(Result(1, "File download complete\nError: Data mismatch found at address 0x08000000")));
        using var artifact = ValidArtifact();

        var result = await provider.ProgramAndVerifyAsync(new DfuProgrammingRequest(Device(1), artifact.Artifact),
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DfuProgrammingOutcome.VerificationFailed);
        result.ProviderLog.Should().Contain("Data mismatch");
    }

    [Fact]
    public async Task NonEnglishUnknownOutputReliesOnExitCodeConservativelyAndPreservesLog()
    {
        var provider = CreateProvider(new FakeRunner(Result(0, "Téléchargement terminé\nVérification terminée")));
        using var artifact = ValidArtifact();

        var result = await provider.ProgramAndVerifyAsync(new DfuProgrammingRequest(Device(1), artifact.Artifact),
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DfuProgrammingOutcome.ProgrammingFailed);
        result.ProviderLog.Should().Contain("Téléchargement terminé");
    }

    [Fact]
    public async Task PnpDeviceIsAssociatedWithUniqueCliUsbSerialBeforeInspection()
    {
        var runner = new FakeRunner(
            Result(0, "USB1\nSerial number: OTHER\nUSB3\nSerial number: MATCH"),
            Result(0, "Device ID : 0x450\nRevision ID : Rev V\nFlash size : 2048 KBytes"));
        var provider = CreateProvider(runner);

        var result = await provider.InspectAsync(Device(null) with { ProviderId = "pnp-id", SerialNumber = "MATCH" },
            TestContext.Current.CancellationToken);

        result.Device.ProviderUsbIndex.Should().Be(3);
        result.McuDeviceId.Should().Be("0x450");
        result.InternalFlashBytes.Should().Be(2 * 1024 * 1024);
        runner.Requests[1].Arguments.Should().Equal("-c", "port=usb3");
    }

    [Fact]
    public async Task InvalidArtifactCannotReachProcessRunner()
    {
        var runner = new FakeRunner(Result(0, "should not run"));
        var provider = CreateProvider(runner);
        var artifact = new DfuArtifact("bad.hex", Path.Combine(Path.GetTempPath(), "missing.hex"),
            new DfuArtifactMetadata(1, 1, 0x08000000, 0x08000000, new string('0', 64), [new DfuMemoryRange(0x08000000, new byte[] { 1 })], []));

        var result = await provider.ProgramAndVerifyAsync(new DfuProgrammingRequest(Device(1), artifact),
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DfuProgrammingOutcome.FileRejected);
        runner.Requests.Should().BeEmpty();
    }

    private static Stm32CubeProgrammerCliDfuProgrammer CreateProvider(FakeRunner runner, Version? version = null) =>
        new(new FakeLocator(new DfuToolStatus(DfuToolAvailability.Available, ToolPath(), version ?? new Version(2, 23))),
            runner, new IntelHexInspector(Options.Create(new DfuOptions()), TimeProvider.System),
            new Stm32CubeProgrammerCommandBuilder(), Options.Create(new DfuOptions()));

    private static DfuDeviceDescriptor Device(int? index) =>
        new(index is null ? "pnp" : $"usb{index}", 0x0483, 0xDF11, DfuDriverState.PresentReady, ProviderUsbIndex: index);

    private static DfuProcessResult Result(int exitCode, string output) => new(exitCode,
        output.Split('\n').Select(line => new DfuProcessOutput(DateTimeOffset.UtcNow, false, line)).ToArray());
    private static string ToolPath() => Path.Combine(Path.GetTempPath(), "STM32_Programmer_CLI.exe");

    private sealed class FakeLocator(DfuToolStatus status) : IDfuToolLocator
    {
        public Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default) => Task.FromResult(status);
    }

    private sealed class FakeRunner(params DfuProcessResult[] results) : IDfuProcessRunner
    {
        private readonly Queue<DfuProcessResult> remaining = new(results);
        public List<DfuProcessRequest> Requests { get; } = [];
        public Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var result = remaining.Dequeue();
            foreach (var line in result.Output) output?.Report(line);
            return Task.FromResult(result);
        }
    }

    private sealed class ProgressRecorder : IProgress<DfuProgress>
    {
        public List<DfuProgress> Items { get; } = [];
        public void Report(DfuProgress value) => Items.Add(value);
    }

    private sealed class TemporaryArtifact : IDisposable
    {
        public TemporaryArtifact()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dfu-{Guid.NewGuid():N}.hex");
            File.WriteAllText(Path, ":020000040800F2\n:0100000001FE\n:00000001FF\n");
            using var stream = File.OpenRead(Path);
            var metadata = new IntelHexInspector(Options.Create(new DfuOptions()), TimeProvider.System)
                .InspectAsync(stream).GetAwaiter().GetResult();
            Artifact = new DfuArtifact(System.IO.Path.GetFileName(Path), Path, metadata);
        }
        public string Path { get; }
        public DfuArtifact Artifact { get; }
        public void Dispose() => File.Delete(Path);
    }

    private static TemporaryArtifact ValidArtifact() => new();
}
