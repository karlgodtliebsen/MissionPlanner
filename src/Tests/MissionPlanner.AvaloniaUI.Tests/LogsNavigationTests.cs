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
        Control Create(int section)
        {
            return section == 1 ? application : telemetry;
        }

        using (var model = new LogsViewModel(Create, state, dispatcher, events, NullLogger<LogsViewModel>.Instance))
        {
            Assert.Same(telemetry, model.Content);
            model.SelectedSection = 1;
            Assert.Same(application, model.Content);
            await model.DeactivateAsync();
            await model.ActivateAsync();
            Assert.Equal(1, model.SelectedSection);
        }

        using var reopened = new LogsViewModel(Create, state, dispatcher, events, NullLogger<LogsViewModel>.Instance);
        Assert.Same(application, reopened.Content);
        reopened.SelectedSection = 0;
        Assert.Same(telemetry, reopened.Content);
    }
}
