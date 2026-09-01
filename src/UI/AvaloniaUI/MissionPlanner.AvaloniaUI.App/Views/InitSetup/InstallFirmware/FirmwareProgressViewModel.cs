using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Presentation state for one firmware operation.</summary>
public sealed partial class FirmwareProgressViewModel : ObservableObject
{
    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial string? Stage { get; set; }

    partial void OnStageChanged(string? value)
    {
        HasStage = !string.IsNullOrEmpty(value);
    }

    partial void OnTechnicalDetailChanged(string? value)
    {
        HasTechnicalDetail = !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial bool HasPercentage { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial bool IsPowerCritical { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial string? TechnicalDetail { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial bool HasStage { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial bool HasTechnicalDetail { get; set; }
}

