using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class FftSetupViewModel : OptionalHardwareBaseViewModel
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SamplesText))
        {
            SetMessages("Use an existing downloaded DataFlash sample export; this page does not download logs.");
            return;
        }
        Analyze();
    }

    /// <summary>Opens the shared Full Parameters workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    private readonly INavigationService navigation;

    private readonly IFftAnalysisService analysis;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="analysis"></param>
    /// <param name="logger"></param>
    /// <param name="navigation">The application navigation service.</param>
    public FftSetupViewModel(IFftAnalysisService analysis, ILogger<FftSetupViewModel> logger, INavigationService navigation) : base(logger)
    {
        this.navigation = navigation;
        this.analysis = analysis;
        SetMessages("Use an existing downloaded DataFlash sample export; this page does not download logs.", null);
    }


    [ObservableProperty] public partial string SamplesText { get; set; } = string.Empty;
    [ObservableProperty] public partial double SampleRateHz { get; set; } = 1000;

    [RelayCommand]
    private void Analyze()
    {
        try
        {
            var samples = SamplesText.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var spectrum = analysis.Analyze(samples, SampleRateHz);
            SetMessages($"Peak: {spectrum.Peak.FrequencyHz:F2} Hz (magnitude {spectrum.Peak.Magnitude:F3})", null);
        }
        catch (Exception ex)
        {
            SetMessages(ex);
        }
    }
}

