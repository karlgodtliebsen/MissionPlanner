using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Reporting;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SetupReportDocumentTests
{
    private static SetupReport Report => new("Frame [unsafe](https://example.com)", "Overview", null, "Reported",
        [new("Device", "<img src='https://example.com'>")], ["Review configuration"],
        [new("FRAME_CLASS", 1), new("MOT_SPIN_ARM", 0.125), new("FRAME_TYPE", null)]);

    [Fact]
    public void AssignmentsAreInvariantAndMissingValuesRemainComments()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            var factory = new SetupReportDocumentFactory();
            var markdown = factory.CreateParameters(Report)!.Markdown;
            Assert.Contains("FRAME_CLASS = 1\nMOT_SPIN_ARM = 0.125\n// FRAME_TYPE: value unavailable", markdown);
            Assert.DoesNotContain("FRAME_TYPE =", markdown);
            var overview = factory.CreateOverview(Report, "[operation](https://example.com)", null).Markdown;
            Assert.Contains("\\[unsafe\\]", overview);
            Assert.Contains("\\<img", overview);
            Assert.Contains("\\[operation\\]", overview);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task UnchangedReportsKeepSelectionAndHiddenReportsStopUpdating()
    {
        var service = Substitute.For<ISetupReportService>();
        service.Capture(Arg.Any<SetupReportTopic>()).Returns(Report);
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        var vm = new SetupInformationViewModel(service, new SetupReportDocumentFactory(),
            Substitute.For<IActiveVehicleContext>(), dispatcher, Substitute.For<IDomainEventHub>(),
            NullLogger<SetupInformationViewModel>.Instance);
        try
        {
            await vm.ActivateAsync();
            var overview = vm.Overview;
            var parameters = vm.Parameters;
            vm.Refresh();
            Assert.Same(overview, vm.Overview);
            Assert.Same(parameters, vm.Parameters);
            service.Capture(Arg.Any<SetupReportTopic>()).Returns(Report with { Parameters = [] });
            vm.Refresh();
            Assert.Null(vm.Parameters);
            Assert.False(vm.HasParameters);
            await vm.DeactivateAsync();
            service.ClearReceivedCalls();
            vm.Refresh();
            service.DidNotReceive().Capture(Arg.Any<SetupReportTopic>());
            await vm.ActivateAsync();
            Assert.NotNull(vm.Overview);
        }
        finally
        {
            await vm.DeactivateAsync();
            vm.Dispose();
        }
    }
}
