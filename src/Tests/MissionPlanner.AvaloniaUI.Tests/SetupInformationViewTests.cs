using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Controls;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Reporting;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SetupInformationViewTests
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var reports = Substitute.For<ISetupReportService>();
        reports.Capture(Arg.Any<SetupReportTopic>()).Returns(new SetupReport("Frame", "Frame configuration", null,
            "Confirmed", [], ["Review changes before applying."], [new("FRAME_CLASS", 1)]));
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => Dispatcher.UIThread.Post(call.Arg<Action>()!));
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(reports).AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IActiveVehicleContext>())
            .AddSingleton(Substitute.For<IDomainEventHub>())
            .AddSingleton<SetupReportDocumentFactory>().AddSingleton<SetupInformationViewModel>()
            .BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task InheritsWorkflowBindingsWhileReportsUseTheirOwnContextAndTabLifetime()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(SetupInformationViewTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var provider = ((MissionPlanner.App.App)Application.Current!).ServiceProvider;
                var vm = provider.GetRequiredService<SetupInformationViewModel>();
                var service = provider.GetRequiredService<ISetupReportService>();
                var workflow = new Workflow { Status = "Initial operation message" };
                var view = new SetupInformationView { Topic = SetupReportTopic.Frame };
                view.Bind(SetupInformationView.WorkflowStatusProperty, new Binding(nameof(Workflow.Status)));
                var tabs = new TabControl
                {
                    DataContext = workflow,
                    ItemsSource = new[]
                    {
                        new TabItem { Header = "Information", Content = view },
                        new TabItem { Header = "Configuration", Content = new TextBlock { Text = "Configuration" } },
                    },
                };
                var window = new Window { Content = tabs, Width = 600, Height = 650 };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Same(workflow, view.DataContext);
                Assert.Contains("Initial operation message", vm.Overview!.Markdown);
                Assert.Equal(2, view.GetVisualDescendants().OfType<InformationDocumentView>().Count());
                Assert.InRange(view.Bounds.Width, 1, 600);
                workflow.Status = "Updated operation message";
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Updated operation message", vm.Overview.Markdown);
                tabs.SelectedIndex = 1;
                Dispatcher.UIThread.RunJobs();
                service.ClearReceivedCalls();
                vm.Refresh();
                service.DidNotReceive().Capture(Arg.Any<SetupReportTopic>());
                tabs.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                service.Received().Capture(SetupReportTopic.Frame);
                window.Close();
                await vm.DeactivateAsync();
                vm.Dispose();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    public sealed class Workflow : ObservableObject
    {
        private string status = string.Empty;
        public string Status { get => status; set => SetProperty(ref status, value); }
    }
}
