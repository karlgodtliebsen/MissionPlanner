using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Diagnostics;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises the real readiness markup with production light/dark themes and narrow layouts.</summary>
[Collection("Document rendering")]
public sealed class ReadinessViewTests
{
    /// <summary>Creates an offline application for layout and screenshot verification.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(new ServiceCollection().AddLogging().BuildServiceProvider()))
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>All outcome types wrap without object strings, retain buttons and collapse successful checks.</summary>
    [Fact]
    public async Task MixedResultsRenderAtNarrowAndWideWidths()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ReadinessViewTests));
        try
        {
            await session.Dispatch(() =>
            {
                using var model = new LiveTelemetryInspectorViewModel(Substitute.For<IVehicleLiveDiagnostics>(),
                    Substitute.For<IActiveVehicleContext>(), Substitute.For<ITextClipboardService>(), Substitute.For<IInspectorWindowService>(),
                    TimeProvider.System, Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(), NullLogger<LiveTelemetryInspectorViewModel>.Instance);
                foreach (var outcome in new[] { PreflightCheckStatus.Fail, PreflightCheckStatus.Warning, PreflightCheckStatus.NotAvailable,
                    PreflightCheckStatus.Stale, PreflightCheckStatus.NotApplicable, PreflightCheckStatus.Pass })
                {
                    var text = outcome == PreflightCheckStatus.Fail
                        ? "FC blocker: Battery 1 below minimum arming voltage. Voltage: 0.02 V; reported percentage: 99% (not verified state of charge). Arming minimum: 11.00 V."
                        : outcome == PreflightCheckStatus.Stale ? "Disconnected — retained sample is historical; recovery is unconfirmed."
                        : "NextGen assessment — missing, disabled and passing evidence have distinct outcomes.";
                    var result = new PreflightCheckResult(outcome.ToString(), PreflightCheckCategory.Power, "Battery readiness — " + outcome,
                        outcome, text, new("BATTERY_STATUS / FC evidence", text, DateTimeOffset.UtcNow), "Review battery monitoring and wiring.", ["BATT_MONITOR"]);
                    var item = new ReadinessCheckItem(result, new VehicleId(16, 1), (_, _) => { }, (_, _) => { });
                    (outcome == PreflightCheckStatus.Pass ? model.PassedChecks : model.ReadinessChecks).Add(item);
                }
                var view = new ReadinessView { DataContext = model };
                var window = new Window { Content = view, Width = 700, Height = 950 };
                window.Show();
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    foreach (var width in new[] { 360, 700 })
                    {
                        window.Width = width;
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        var text = view.GetVisualDescendants().OfType<TextBlock>().Select(item => item.Text ?? "").ToArray();
                        Assert.DoesNotContain(text, item => item.Contains("PreflightCheckResult {"));
                        Assert.Contains(text, item => item.Contains("✖ Failed"));
                        Assert.Contains(text, item => item.Contains("Unknown"));
                        Assert.All(view.GetVisualDescendants().OfType<SelectableTextBlock>(), item => Assert.InRange(item.Bounds.Width, 0, width));
                        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "View evidence"));
                        var passed = view.GetVisualDescendants().OfType<Expander>().Single(item => Equals(item.Header, "Passed checks"));
                        Assert.False(passed.IsExpanded);
                        var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                            using var bitmap = new RenderTargetBitmap(new PixelSize(width, 950));
                            bitmap.Render(view);
                            bitmap.Save(Path.Combine(directory, $"readiness-{theme}-{width}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                        }
                    }
                }
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}
