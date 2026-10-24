using MissionPlanner.Core.Setup.OptionalHardware;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class JoystickSessionTests
{
    [Fact]
    public async Task PreviewNeverEnablesOutputAndInputLossReleasesEnabledControl()
    {
        var provider = Substitute.For<IJoystickProvider>();
        var output = Substitute.For<IJoystickVehicleOutput>();
        var device = new Device();
        provider.OpenAsync("test", Arg.Any<CancellationToken>()).Returns(device);
        var enabled = false;
        output.IsEnabled.Returns(_ => enabled);
        output.EnableAsync(Arg.Any<JoystickOutputMode>(), true, Arg.Any<CancellationToken>()).Returns(_ => { enabled = true; return Task.CompletedTask; });
        output.ReleaseAsync(Arg.Any<CancellationToken>()).Returns(_ => { enabled = false; return Task.CompletedTask; });
        var sampled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.SendManualControlAsync(Arg.Any<JoystickVehicleCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            device.Fail = true;
            sent.TrySetResult();
            return Task.CompletedTask;
        });
        await using var session = new JoystickControlSession(provider, output);
        session.Sampled += (_, _) => sampled.TrySetResult();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StatusChanged += text => { if (text.StartsWith("Joystick stopped", StringComparison.Ordinal)) failed.TrySetResult(); };
        await session.StartAsync("test", new() { CalibrationReviewed = true }, TestContext.Current.CancellationToken);
        await sampled.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.False(enabled);
        Assert.DoesNotContain(output.ReceivedCalls(), call => call.GetMethodInfo().Name == "SendManualControlAsync");
        await session.EnableAsync(true, TestContext.Current.CancellationToken);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await session.StopAsync();
        Assert.False(enabled);
        Assert.True(device.Disposed);
    }

    [Theory]
    [InlineData(false, true, -1)]
    [InlineData(true, false, -1)]
    [InlineData(true, true, 0)]
    public async Task ControlRequiresReviewedCalibrationHeldDeadmanAndLowThrottle(bool reviewed, bool held, double throttle)
    {
        var provider = Substitute.For<IJoystickProvider>();
        var output = Substitute.For<IJoystickVehicleOutput>();
        var device = new Device { Held = held, Throttle = throttle };
        provider.OpenAsync("test", Arg.Any<CancellationToken>()).Returns(device);
        await using var session = new JoystickControlSession(provider, output);
        var sample = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Sampled += (_, _) => sample.TrySetResult();
        await session.StartAsync("test", new() { CalibrationReviewed = reviewed }, TestContext.Current.CancellationToken);
        await sample.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.EnableAsync(true, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(output.ReceivedCalls(), call => call.GetMethodInfo().Name == "EnableAsync");
    }

    [Fact]
    public void MappingAppliesExpoAndExplicitButtonBits()
    {
        var profile = new JoystickProfile
        {
            Axes = new[] { new JoystickAxisMapping(JoystickFunction.Roll, 0, Expo: 1), new(JoystickFunction.Pitch, 1), new(JoystickFunction.Throttle, 2), new(JoystickFunction.Yaw, 3) },
            Buttons = new[] { -1, 2 },
        };
        profile.Validate(new("test", "Test", 4, 4));
        var state = new JoystickState("test", new[] { new JoystickAxisState(0, .5), new(1, 0), new(2, -1), new(3, 0) },
            new[] { new JoystickButtonState(0, true), new(2, true) }, DateTimeOffset.UtcNow);
        var command = JoystickControlSession.Map(state, profile);
        Assert.Equal(.125, command.Roll);
        Assert.Equal(2u, command.Buttons);
        Assert.Throws<ArgumentException>(() => (profile with { Buttons = new[] { 0 } }).Validate(new("test", "Test", 4, 4)));
    }

    private sealed class Device : IJoystickDevice
    {
        public bool Fail;
        public bool Held = true;
        public double Throttle = -1;
        public bool Disposed;
        public JoystickDeviceDescriptor Descriptor => new("test", "Test joystick", 4, 4);
        public Task<JoystickState> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("Simulated device loss");
            return Task.FromResult(new JoystickState("test", new[] { new JoystickAxisState(0, 0), new(1, 0), new(2, Throttle), new(3, 0) },
                new[] { new JoystickButtonState(0, Held) }, DateTimeOffset.UtcNow));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
