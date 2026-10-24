using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Presents optional FFT setup information and manual sample analysis.</summary>
public sealed partial class FftSetupViewModel : ParameterHardwareViewModel
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
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        analysisGeneration++;
        analysisLifetime?.Cancel();
        analysisLifetime?.Dispose();
        analysisLifetime = null;
        return base.DeactivateAsync();
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
    private readonly INavigationService navigation;

    private readonly IFftAnalysisService analysis;

    /// <summary>
    /// Presents manual sample analysis through the reusable FFT adapter.
    /// </summary>
    /// <param name="vehicle">The selected connection context.</param>
    /// <param name="hardware">Loads and confirms supported logging settings.</param>
    /// <param name="analysis">Shared FFT compatibility adapter.</param>
    /// <param name="logger">Presentation logger.</param>
    /// <param name="navigation">The application navigation service.</param>
    public FftSetupViewModel(IActiveVehicleContext vehicle, IOptionalHardwareService hardware, IFftAnalysisService analysis, ILogger<FftSetupViewModel> logger, INavigationService navigation)
        : base("fft-logging", vehicle, hardware, logger, navigation)
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

