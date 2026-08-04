using System.ComponentModel;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuProcessRunnerTests
{
    [Fact]
    public async Task SuccessfulOutputIsTimestampedAndSeparated()
    {
        var child = FakeChild.Completed(0, ["ready"], ["warning"]);
        var result = await CreateRunner(child).RunAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(0);
        result.FailureCode.Should().BeNull();
        result.Output.Should().HaveCount(2);
        result.Output.Should().Contain(item => !item.IsError && item.Text == "ready");
        result.Output.Should().Contain(item => item.IsError && item.Text == "warning");
    }

    [Fact]
    public async Task NonzeroExitIsPreserved()
    {
        var result = await CreateRunner(FakeChild.Completed(7)).RunAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(7);
        result.FailureCode.Should().Be("NonzeroExit");
    }

    [Fact]
    public async Task HungProcessTimesOutAndIsKilledOnlyWhenAuthorized()
    {
        var killable = FakeChild.Hung();
        var request = Request() with { ExecutionTimeout = TimeSpan.FromMilliseconds(20), MayKillProcessTreeOnCancellation = true };

        var result = await CreateRunner(killable).RunAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        result.TimedOut.Should().BeTrue();
        killable.KillCalled.Should().BeTrue();
        killable.KillIncludedTree.Should().BeTrue();
    }

    [Fact]
    public async Task CancellationTerminatesOnlyAnExplicitlyKillableProcess()
    {
        var child = FakeChild.Hung();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = CreateRunner(child).RunAsync(Request() with { MayKillProcessTreeOnCancellation = true }, cancellationToken: cancellation.Token);
        await child.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        var result = await running;

        result.WasCancelled.Should().BeTrue();
        child.KillCalled.Should().BeTrue();
    }

    [Fact]
    public async Task LargeAndMalformedOutputIsBoundedAndPreservedAsDecodedText()
    {
        var child = FakeChild.Completed(0, ["12345", "67890", "bad\uFFFDtext"]);
        var options = new DfuOptions { MaximumProviderOutputLines = 2, MaximumProviderOutputCharacters = 20 };

        var result = await CreateRunner(child, options).RunAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        result.Output.Should().HaveCount(2);
        result.OutputTruncated.Should().BeTrue();
        result.Output.Select(item => item.Text).Should().Contain("67890");
    }

    [Fact]
    public async Task MissingExecutableReturnsTypedFailure()
    {
        var child = new FakeChild { StartException = new Win32Exception(2) };

        var result = await CreateRunner(child).RunAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        result.FailureCode.Should().Be("ExecutableMissing");
    }

    [Fact]
    public async Task ArbitraryExecutableOrArgumentsAreRejectedBeforeCreation()
    {
        var factory = new FakeFactory(FakeChild.Completed(0));
        var runner = new DfuProcessRunner(factory, Options.Create(new DfuOptions()), TimeProvider.System);
        var request = Request() with { ExecutablePath = Path.Combine(Path.GetTempPath(), "cmd.exe"), Arguments = ["/c", "anything"] };

        var result = await runner.RunAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        result.FailureCode.Should().Be("RequestRejected");
        factory.CreateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task CancellationAlreadyRequestedNeverCreatesAProcess()
    {
        var factory = new FakeFactory(FakeChild.Completed(0));
        var runner = new DfuProcessRunner(factory, Options.Create(new DfuOptions()), TimeProvider.System);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        var result = await runner.RunAsync(Request(), cancellationToken: cancellation.Token);

        result.WasCancelled.Should().BeTrue();
        factory.CreateCalled.Should().BeFalse();
    }

    private static DfuProcessRunner CreateRunner(FakeChild child, DfuOptions? options = null) =>
        new(new FakeFactory(child), Options.Create(options ?? new DfuOptions()), TimeProvider.System);

    private static DfuProcessRequest Request() => new(
        Path.Combine(Path.GetTempPath(), "STM32_Programmer_CLI.exe"),
        ["--version"],
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1));

    private sealed class FakeFactory(FakeChild child) : IDfuChildProcessFactory
    {
        public bool CreateCalled { get; private set; }
        public IDfuChildProcess Create(string executablePath, IReadOnlyList<string> arguments, Encoding outputEncoding)
        {
            CreateCalled = true;
            outputEncoding.DecoderFallback.Should().BeOfType<DecoderReplacementFallback>();
            return child;
        }
    }

    private sealed class FakeChild : IDfuChildProcess
    {
        private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IReadOnlyList<string> standardOutput = [];
        private IReadOnlyList<string> standardError = [];
        private int exitCode;
        public event Action<string?>? OutputReceived;
        public event Action<string?>? ErrorReceived;
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? StartException { get; init; }
        public bool KillCalled { get; private set; }
        public bool KillIncludedTree { get; private set; }
        public bool HasExited => exited.Task.IsCompleted;
        public int ExitCode => exitCode;

        public static FakeChild Completed(int exitCode, IReadOnlyList<string>? output = null, IReadOnlyList<string>? error = null) =>
            new() { exitCode = exitCode, standardOutput = output ?? [], standardError = error ?? [], CompleteAfterRead = true };
        public static FakeChild Hung() => new();
        private bool CompleteAfterRead { get; init; }
        public bool Start() { if (StartException is not null) throw StartException; return true; }
        public void BeginOutputRead()
        {
            foreach (var line in standardOutput) OutputReceived?.Invoke(line);
            foreach (var line in standardError) ErrorReceived?.Invoke(line);
            ReadStarted.TrySetResult();
            if (CompleteAfterRead) exited.TrySetResult();
        }
        public Task WaitForExitAsync(CancellationToken cancellationToken = default) => exited.Task.WaitAsync(cancellationToken);
        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;
            KillIncludedTree = entireProcessTree;
            exitCode = -1;
            exited.TrySetResult();
        }
        public void Dispose() { }
    }
}
