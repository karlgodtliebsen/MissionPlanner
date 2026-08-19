using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class FftSetupViewModel(IFftAnalysisService analysis) : OptionalHardwareBaseViewModel
{
    [ObservableProperty] public partial string SamplesText { get; set; } = string.Empty;
    [ObservableProperty] public partial double SampleRateHz { get; set; } = 1000;
    [ObservableProperty] public partial string Result { get; set; } = "Use an existing downloaded DataFlash sample export; this page does not download logs.";

    [RelayCommand]
    private void Analyze()
    {
        try
        {
            var samples = SamplesText.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var spectrum = analysis.Analyze(samples, SampleRateHz);
            Result = $"Peak: {spectrum.Peak.FrequencyHz:F2} Hz (magnitude {spectrum.Peak.Magnitude:F3})";
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
