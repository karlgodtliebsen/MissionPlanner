using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Presents optional FFT setup information and manual sample analysis.</summary>
public sealed partial class FftSetupViewModel : OptionalHardwareBaseViewModel
{
    private CancellationTokenSource? analysisLifetime;
    private int analysisGeneration;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        analysisLifetime?.Cancel();
        analysisLifetime?.Dispose();
        analysisLifetime = new CancellationTokenSource();
        analysisGeneration++;
        IsBusy = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        analysisGeneration++;
        analysisLifetime?.Cancel();
        analysisLifetime?.Dispose();
        analysisLifetime = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        analysisGeneration++;
        analysisLifetime?.Cancel();
        analysisLifetime?.Dispose();
        analysisLifetime = null;
        base.Dispose();
    }
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(SamplesText))
        {
            SetMessages("Use an existing downloaded DataFlash sample export; this page does not download logs.");
            return;
        }
        await AnalyzeAsync();
    }

    /// <summary>Opens the shared Parameters Editor workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
    }

    private readonly INavigationService navigation;

    private readonly IFftAnalysisService analysis;

    /// <summary>
    /// Presents manual sample analysis through the reusable FFT adapter.
    /// </summary>
    /// <param name="analysis">Shared FFT compatibility adapter.</param>
    /// <param name="logger">Presentation logger.</param>
    /// <param name="navigation">The application navigation service.</param>
    public FftSetupViewModel(IFftAnalysisService analysis, ILogger<FftSetupViewModel> logger, INavigationService navigation) : base(logger)
    {
        this.navigation = navigation;
        this.analysis = analysis;
        SetMessages("Use an existing downloaded DataFlash sample export; this page does not download logs.", null);
    }


    /// <summary>Manual invariant-culture source samples.</summary>
    [ObservableProperty] public partial string SamplesText { get; set; } = string.Empty;
    /// <summary>Uniform source sampling rate in hertz.</summary>
    [ObservableProperty] public partial double SampleRateHz { get; set; } = 1000;

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (IsBusy || analysisLifetime is null)
        {
            return;
        }
        var generation = analysisGeneration;
        var token = analysisLifetime.Token;
        IsBusy = true;
        try
        {
            var samples = SamplesText.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var rate = SampleRateHz;
            var spectrum = await Task.Run(() => analysis.Analyze(samples, rate), token);
            token.ThrowIfCancellationRequested();
            SetMessages($"Peak: {spectrum.Peak.FrequencyHz:F2} Hz (normalized amplitude {spectrum.Peak.Magnitude:F3}). Used {spectrum.SamplesUsed} samples; unused tail {spectrum.UnusedTailSamples}. Use FFT / Vibration Analysis for log intervals and multiple windows.", null);
        }
        catch (Exception ex)
        {
            if (generation == analysisGeneration)
            {
                SetMessages(ex);
            }
        }
        finally
        {
            if (generation == analysisGeneration)
            {
                IsBusy = false;
            }
        }
    }
}

