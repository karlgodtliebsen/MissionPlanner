using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Controls;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.Reporting;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises the real servo Configuration tab with application themes.</summary>
[Collection("Document rendering")]
public sealed class ServoOutputViewTests
{
    /// <summary>Builds the production application with isolated setup services.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => Dispatcher.UIThread.Post(call.Arg<Action>()!));
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IActiveVehicleContext>())
            .AddSingleton(Substitute.For<IServoOutputConfigurationService>())
            .AddSingleton(Substitute.For<IDomainEventHub>())
            .AddSingleton(Substitute.For<INavigationService>())
            .AddSingleton(Substitute.For<ISetupReportService>())
            .AddSingleton<SetupReportDocumentFactory>()
            .AddSingleton<SetupInformationViewModel>()
            .AddSingleton<ServoOutputSetupViewModel>().BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>Loaded rows must produce visible function and PWM editors in both themes.</summary>
    [Fact]
    public async Task ConfigurationRendersLoadedServoRows()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ServoOutputViewTests));
        try
        {
            await session.Dispatch(() =>
            {
                var services = ((MissionPlanner.App.App)Application.Current!).ServiceProvider;
                var model = services.GetRequiredService<ServoOutputSetupViewModel>();
                var output = new ServoOutputItemViewModel(new ServoOutputInfo(1, 33, "Motor 1", false,
                    1000, 1500, 2000, 1100, false, 800, 2200), [new ServoFunctionOption(33, "Motor 1")], _ => { });
                model.Outputs.Add(output);
                var view = new ServoOutputSetupView();
                var tabs = Assert.Single(view.GetLogicalDescendants().OfType<TabControl>());
                tabs.SelectedIndex = 1;
                var window = new Window { Content = view, Width = 1100, Height = 750 };
                try
                {
                    window.Show();
                    foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    {
                        window.RequestedThemeVariant = theme;
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        var grid = Assert.Single(view.GetVisualDescendants().OfType<DataGrid>());
                        Assert.Same(model.Outputs, grid.ItemsSource);
                        Assert.NotNull(grid.Template);
                        Assert.NotEmpty(grid.GetVisualDescendants().OfType<DataGridRow>());
                        Assert.Contains(grid.GetVisualDescendants().OfType<ComboBox>(), box => box.SelectedItem == output.SelectedFunction);
                        Assert.Equal(3, grid.GetVisualDescendants().OfType<NumericUpDown>().Count());
                        Assert.True(grid.Bounds.Height > 0);
                    }
                    Assert.False(output.IsDirty);
                }
                finally
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                    model.Dispose();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}
