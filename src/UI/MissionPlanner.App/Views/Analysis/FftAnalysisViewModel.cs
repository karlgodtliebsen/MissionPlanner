using System.Collections.Immutable;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Analysis;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>Owns offline selections and cancellable work; all signal analysis belongs to Core/Analysis.</summary>
public sealed partial class FftAnalysisViewModel : ViewModelBase
{
    private readonly DataFlashImuSampleProvider provider;
    private readonly FftWorkspaceService workspace;
    private readonly NotchParameterAnalysisService parameters;
    private readonly IFileOpenService files;
    private readonly IFileSaveService saves;
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? operation;
    private int generation;
    private int activePresentations;
    private ImuLogData? log;
    private FftBaseline? baseline;
    private NotchParameterSnapshot? parameterSnapshot;
    /// <summary>Raised after a log finishes loading and its initial analysis completes.</summary>
    public event EventHandler? LogLoaded;
    /// <summary>Latest automatic refresh, also available to callers awaiting a source change.</summary>
    public Task SelectionAnalysis { get; private set; } = Task.CompletedTask;
    /// <summary>Display title of the loaded log.</summary>
    [ObservableProperty] public partial string FileName { get; set; } = "DataFlash Logs";
    /// <summary>Selected workspace tab: file information or graphs.</summary>
    [ObservableProperty]
    public partial int SelectedTab
    {
        get; set;
    }

    /// <summary>Creates the analysis presentation with explicit file, domain and dispatch services.</summary>
    public FftAnalysisViewModel(DataFlashImuSampleProvider provider, FftWorkspaceService workspace,
        NotchParameterAnalysisService parameters, IFileOpenService files, IFileSaveService saves,
        ILogger<FftAnalysisViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events) : base(logger, dispatcher, events)
    {
        this.provider = provider;
        this.workspace = workspace;
        this.parameters = parameters;
        this.files = files;
        this.saves = saves;
    }

    /// <summary>Selectable uniform segments, each identifying IMU, signal and axis.</summary>
    [ObservableProperty] public partial ImmutableArray<ImuSampleSeries> Sources { get; set; } = [];
    /// <summary>Selected sensor/axis segment.</summary>
    [ObservableProperty]
    public partial ImuSampleSeries? SelectedSource
    {
        get; set;
    }
    /// <summary>Artifact name and quality diagnostics.</summary>
    [ObservableProperty] public partial string SourceDescription { get; set; } = "Open an existing DataFlash .bin or FMT-based .log file.";
    /// <summary>Data-quality details for a successfully loaded artifact.</summary>
    [ObservableProperty] public partial string SourceDiagnostics { get; set; } = "No log loaded.";
    /// <summary>Selected start on the log boot-time clock.</summary>
    [ObservableProperty]
    public partial double StartSeconds
    {
        get; set;
    }
    /// <summary>Selected inclusive end on the log boot-time clock.</summary>
    [ObservableProperty]
    public partial double EndSeconds
    {
        get; set;
    }
    /// <summary>Selected FFT window length.</summary>
    [ObservableProperty] public partial int FftSize { get; set; } = 1024;
    /// <summary>Lower frequency bound.</summary>
    [ObservableProperty]
    public partial double MinimumHz
    {
        get; set;
    }
    /// <summary>Upper frequency bound; zero selects Nyquist.</summary>
    [ObservableProperty]
    public partial double MaximumHz
    {
        get; set;
    }
    /// <summary>Whether the plot shows time-frequency magnitude.</summary>
    [ObservableProperty]
    public partial bool ShowSpectrogram
    {
        get; set;
    }
    /// <summary>Completed immutable plot/evidence model.</summary>
    [ObservableProperty]
    public partial FftWorkspaceResult? Result
    {
        get; set;
    }
    /// <summary>Bounded textual assessment and comparison.</summary>
    [ObservableProperty] public partial string Report { get; set; } = "Select a source and interval, then Analyze.";
    /// <summary>Retained baseline provenance.</summary>
    [ObservableProperty] public partial string BaselineDescription { get; set; } = "No baseline captured.";
    /// <summary>Parameter values with metadata-backed explanations.</summary>
    [ObservableProperty] public partial string ParameterReport { get; set; } = "Read-only: capture connected parameters or load a saved snapshot.";
    /// <summary>Explicit metadata family for saved parameters.</summary>
    [ObservableProperty] public partial string VehicleFamily { get; set; } = "ArduCopter";
    /// <summary>Whether a proposed static filter is included in the next analysis.</summary>
    [ObservableProperty]
    public partial bool SimulateProposal
    {
        get; set;
    }
    /// <summary>Offline proposed center frequency.</summary>
    [ObservableProperty] public partial double ProposedCenterHz { get; set; } = 150;
    /// <summary>Offline proposed bandwidth.</summary>
    [ObservableProperty] public partial double ProposedBandwidthHz { get; set; } = 30;
    /// <summary>Offline proposed center attenuation.</summary>
    [ObservableProperty] public partial double ProposedAttenuationDb { get; set; } = 30;
    /// <summary>Supported FFT selection sizes.</summary>
    public IReadOnlyList<int> FftSizes { get; } = new[] { 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
    /// <summary>Metadata families exposed without protocol types.</summary>
    public IReadOnlyList<string> VehicleFamilies => NotchParameterAnalysisService.VehicleFamilies;

    partial void OnSelectedSourceChanged(ImuSampleSeries? value)
    {
        Result = null;
        Report = "Selections changed. Analyze to refresh the measured result.";
        if (value is not null)
        {
            StartSeconds = value.StartTimeSeconds;
            EndSeconds = value.EndTimeSeconds;
            MaximumHz = 0;
            MinimumHz = 0;
            if (value.SampleCount < FftSize)
            {
                FftSize = FftSizes.LastOrDefault(size => size <= value.SampleCount, 16);
            }
            if (!IsBusy && lifetime is not null && log is not null && value.SampleCount >= 16)
            {
                SelectionAnalysis = AnalyzeAsync();
            }
        }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (++activePresentations > 1)
        {
            return Task.CompletedTask;
        }
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        generation++;
        IsBusy = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        if (activePresentations == 0 || --activePresentations > 0)
        {
            return Task.CompletedTask;
        }
        generation++;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        var loadedSuccessfully = false;
        await RunAsync(async token =>
        {
            using var file = await files.OpenAsync("Open DataFlash for FFT analysis", ["*.bin", "*.log"], token);
            if (file is null)
            {
                return;
            }
            var loaded = await Task.Run(() => provider.Read(file.Content, file.FileName,
                file.FileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase), token), token);
            token.ThrowIfCancellationRequested();
            log = loaded;
            FileName = loaded.Name;
            Sources = loaded.Series;
            SelectedSource = Sources.FirstOrDefault();
            SourceDescription = $"Loaded {loaded.Name}: {Sources.Length} usable axis segments.";
            SourceDiagnostics = string.Join("\n", loaded.Diagnostics);
            if (Sources.IsEmpty)
            {
                Result = null;
                Report = "No valid IMU segments were found. Inspect the source diagnostics.";
            }
            else if (SelectedSource is { SampleCount: >= 16 })
            {
                await AnalyzeCurrentAsync(token);
            }
            else
            {
                Report = "Log loaded. Select a segment with at least 16 samples to display an FFT graph.";
            }
            SelectedTab = Result is not null ? 1 : 0;
            loadedSuccessfully = true;
        });
        if (loadedSuccessfully)
        {
            LogLoaded?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private Task AnalyzeAsync()
    {
        return RunAsync(AnalyzeCurrentAsync);
    }

    private async Task AnalyzeCurrentAsync(CancellationToken token)
    {
        var capturedLog = log ?? throw new InvalidOperationException("Open a DataFlash log first.");
        var source = SelectedSource ?? throw new InvalidOperationException("Select a uniform IMU segment.");
        var request = new FftAnalysisRequest(StartSeconds, EndSeconds, FftSize, MinimumHz, MaximumHz == 0 ? null : MaximumHz);
        var capturedBaseline = baseline;
        var capturedParameters = parameterSnapshot;
        NotchFilter[] proposed = SimulateProposal ? [new(ProposedCenterHz, ProposedBandwidthHz, ProposedAttenuationDb)] : [];
        var result = await Task.Run(() => workspace.Analyze(capturedLog, source, request, capturedBaseline, capturedParameters, proposed, token), token);
        var report = await Task.Run(() => FftReportFormatter.Format(result), token);
        token.ThrowIfCancellationRequested();
        Result = result;
        Report = report;
    }

    [RelayCommand]
    private void CaptureBaseline()
    {
        if (!IsBusy && Result is { } result)
        {
            baseline = new FftBaseline($"{log?.Name}: {result.Source.DisplayName}", result.Source.Unit, result.Spectrum);
            BaselineDescription = baseline.Name;
        }
    }

    [RelayCommand]
    private void ClearBaseline()
    {
        if (!IsBusy)
        {
            baseline = null;
            BaselineDescription = "No baseline captured. Analyze to refresh comparison results.";
        }
    }

    [RelayCommand]
    private Task ReadConnectedAsync()
    {
        return RunAsync(async token =>
    {
        var snapshot = await parameters.ReadConnectedAsync(token);
        token.ThrowIfCancellationRequested();
        parameterSnapshot = snapshot;
        ParameterReport = FftReportFormatter.FormatParameters(snapshot);
    });
    }

    [RelayCommand]
    private Task OpenParametersAsync()
    {
        return RunAsync(async token =>
            {
                using var file = await files.OpenAsync("Open saved notch parameter snapshot", ["*.param", "*.params", "*.csv", "*.txt"], token);
                if (file is null)
                {
                    return;
                }
                var snapshot = await parameters.ReadSavedAsync(file.Content, file.FileName, VehicleFamily, token);
                token.ThrowIfCancellationRequested();
                parameterSnapshot = snapshot;
                ParameterReport = FftReportFormatter.FormatParameters(snapshot);
            });
    }

    [RelayCommand]
    private Task ExportEvidenceAsync()
    {
        return RunAsync(async token =>
            {
                var result = Result ?? throw new InvalidOperationException("Analyze a source first.");
                var bytes = await Task.Run(() => JsonSerializer.SerializeToUtf8Bytes(new
                {
                    SchemaVersion = 1,
                    Dataset = log?.Name,
                    Result = result
                }, new JsonSerializerOptions { WriteIndented = true }), token);
                using var content = new MemoryStream(bytes);
                token.ThrowIfCancellationRequested();
                await saves.SaveAsync("fft-evidence.json", content, token);
            });
    }

    [RelayCommand]
    private void Cancel()
    {
        operation?.Cancel();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activePresentations = 0;
        generation++;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        base.Dispose();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy || lifetime is null)
        {
            return;
        }
        var currentGeneration = generation;
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operation = pending;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action(pending.Token);
        }
        catch (OperationCanceledException)
        {
            if (currentGeneration == generation)
            {
                ErrorMessage = "Operation cancelled. Previous completed evidence is retained.";
            }
        }
        catch (Exception exception)
        {
            if (currentGeneration == generation)
            {
                ErrorMessage = exception.Message;
            }
            Logger.LogWarning(exception, "Offline FFT analysis failed");
        }
        finally
        {
            if (ReferenceEquals(operation, pending))
            {
                operation = null;
            }
            if (currentGeneration == generation)
            {
                IsBusy = false;
            }
        }
    }
}
