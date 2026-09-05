using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class FftSetupViewModel : OptionalHardwareBaseViewModel
{
    private readonly IFftAnalysisService analysis;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="analysis"></param>
    /// <param name="logger"></param>
    public FftSetupViewModel(IFftAnalysisService analysis, ILogger<FftSetupViewModel> logger) : base(logger)
    {
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

