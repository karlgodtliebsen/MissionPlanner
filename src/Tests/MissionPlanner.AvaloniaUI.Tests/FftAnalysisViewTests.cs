using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Analysis;
using MissionPlanner.Core.Analysis;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class FftAnalysisViewTests
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(Create(Substitute.For<IFileOpenService>(), Substitute.For<IFileSaveService>()))
            .BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task CompiledViewRendersSourceControlsAndBothPlotModes()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(FftAnalysisViewTests));
        try
        {
            await session.Dispatch(() =>
            {
                var model = ((MissionPlanner.App.App)Application.Current!).ServiceProvider.GetRequiredService<FftAnalysisViewModel>();
                using var samples = Fixture();
                var log = new DataFlashImuSampleProvider(new DataFlashRecordReader()).Read(samples, "test.log", false, TestContext.Current.CancellationToken);
                model.Sources = log.Series;
                model.SelectedSource = log.Series[0];
                model.Result = Workspace().Analyze(log, log.Series[0], new(0, log.Series[0].EndTimeSeconds, 64, 0, null),
                    null, null, [], TestContext.Current.CancellationToken);
                var view = new FftAnalysisView();
                var window = new Window { Content = view, Width = 1200, Height = 950 };
                try
                {
                    window.Show();
                    foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    {
                        window.RequestedThemeVariant = theme;
                        foreach (var spectrogram in new[] { false, true })
                        {
                            model.ShowSpectrogram = spectrogram;
                            Dispatcher.UIThread.RunJobs();
                            window.UpdateLayout();
                            var plot = Assert.Single(view.GetVisualDescendants().OfType<FrequencyPlot>());
                            Assert.Same(model.Result, plot.Result);
                            Assert.Equal(spectrogram, plot.ShowSpectrogram);
                            Assert.True(plot.Bounds.Width > 300);
                            Assert.True(plot.Bounds.Height > 200);
                            Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b => ReferenceEquals(b.Command, model.AnalyzeCommand));
                            Assert.Contains(view.GetVisualDescendants().OfType<ComboBox>(), b => ReferenceEquals(b.SelectedItem, model.SelectedSource));
                        }
                    }
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

    [Fact]
    public async Task ViewModelLoadsAnalyzesCapturesBaselineAndExportsStructuredEvidence()
    {
        var files = Substitute.For<IFileOpenService>();
        files.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<OpenedPlanningFile?>(new("synthetic.log", Fixture())));
        var saves = Substitute.For<IFileSaveService>();
        byte[]? bytes = null;
        saves.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            using var copy = new MemoryStream();
            call.Arg<Stream>()!.CopyTo(copy);
            bytes = copy.ToArray();
            return Task.FromResult<string?>("fft-evidence.json");
        });
        using var model = Create(files, saves);
        await model.ActivateAsync();
        await model.OpenLogCommand.ExecuteAsync(null);
        Assert.Equal(3, model.Sources.Length);
        model.FftSize = 64;
        await model.AnalyzeCommand.ExecuteAsync(null);
        Assert.Null(model.ErrorMessage);
        Assert.NotNull(model.Result);
        model.CaptureBaselineCommand.Execute(null);
        model.SimulateProposal = true;
        model.ProposedCenterHz = 128;
        model.ProposedBandwidthHz = 20;
        model.ProposedAttenuationDb = 40;
        await model.AnalyzeCommand.ExecuteAsync(null);
        Assert.Null(model.ErrorMessage);
        Assert.NotEmpty(model.Result.Comparison);
        Assert.All(model.Result.Comparison, row => Assert.InRange(Math.Abs(row.Change), 0, 1e-10));
        Assert.NotEmpty(model.Result.ProposedSimulation);
        await model.ExportEvidenceCommand.ExecuteAsync(null);
        Assert.Null(model.ErrorMessage);
        using var exported = JsonDocument.Parse(bytes!);
        Assert.Equal(1, exported.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.True(exported.RootElement.GetProperty("Result").GetProperty("Spectrogram").GetProperty("Frames").GetArrayLength() > 0);
        await model.DeactivateAsync();
    }

    [Fact]
    public async Task DeactivatedViewRejectsLateFilePickerResultAndCanReactivate()
    {
        var files = Substitute.For<IFileOpenService>();
        var pending = new TaskCompletionSource<OpenedPlanningFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        files.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        using var model = Create(files, Substitute.For<IFileSaveService>());
        await model.ActivateAsync();
        var load = model.OpenLogCommand.ExecuteAsync(null);
        Assert.True(model.IsBusy);
        await model.DeactivateAsync();
        pending.SetResult(new OpenedPlanningFile("late.log", Fixture()));
        await load;
        Assert.Empty(model.Sources);
        Assert.Null(model.Result);
        await model.ActivateAsync();
        files.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<OpenedPlanningFile?>(new("fresh.log", Fixture())));
        await model.OpenLogCommand.ExecuteAsync(null);
        Assert.False(model.IsBusy);
        Assert.Equal(3, model.Sources.Length);
    }

    private static FftAnalysisViewModel Create(IFileOpenService files, IFileSaveService saves)
    {
        return new FftAnalysisViewModel(new DataFlashImuSampleProvider(new DataFlashRecordReader()), Workspace(),
            new NotchParameterAnalysisService(Substitute.For<IActiveVehicleContext>(), Substitute.For<IVehicleParameterRegistry>(),
                Substitute.For<IVehicleParameterMetadataService>()), files, saves, NullLogger<FftAnalysisViewModel>.Instance,
            new InlineDispatcher(), Substitute.For<IDomainEventHub>());
    }

    private static FftWorkspaceService Workspace()
    {
        var fft = new FftAnalyzer();
        return new FftWorkspaceService(new SpectrumAverager(fft), new SpectrogramAnalyzer(fft), new PeakDetector(),
            new HarmonicDetector(), new MotorFrequencyCorrelator(), new ResonanceDetector(), new SpectrumComparer(), new NotchFilterSimulator());
    }

    private static MemoryStream Fixture()
    {
        var text = new StringBuilder("FMT,150,32,GYR,QBQfff,TimeUS,I,SampleUS,GyrX,GyrY,GyrZ\n");
        for (var i = 0; i < 128; i++)
        {
            var time = Math.Round(i * 1e6 / 1024);
            text.AppendLine(FormattableString.Invariant($"GYR,{time},0,{time},{Math.Sin(2 * Math.PI * 128 * i / 1024):R},0,0"));
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}
