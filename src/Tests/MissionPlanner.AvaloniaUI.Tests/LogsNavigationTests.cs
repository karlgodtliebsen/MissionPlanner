using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Logs;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class LogsNavigationTests
{
    [Fact]
    public async Task DefaultsToTelemetryAndRestoresSelectionAcrossHosts()
    {
        var state = new LogsNavigationState();
        var dispatcher = Substitute.For<IUiDispatcher>();
        var events = Substitute.For<IDomainEventHub>();
        var telemetry = new UserControl();
        var application = new UserControl();
        var factory = Substitute.For<ILogsViewFactory>();
        factory.Create(LogsSection.Telemetry).Returns(telemetry);
        factory.Create(LogsSection.Application).Returns(application);

        using (var model = new LogsViewModel(factory, state, dispatcher, events, NullLogger<LogsViewModel>.Instance))
        {
            Assert.Same(telemetry, model.Content);
            model.SelectedSection = LogsSection.Application;
            Assert.Same(application, model.Content);
            await model.DeactivateAsync();
            await model.ActivateAsync();
            Assert.Equal(LogsSection.Application, model.SelectedSection);
        }

        using var reopened = new LogsViewModel(factory, state, dispatcher, events, NullLogger<LogsViewModel>.Instance);
        Assert.Same(application, reopened.Content);
        reopened.SelectedSection = LogsSection.Telemetry;
        Assert.Same(telemetry, reopened.Content);
    }
}
