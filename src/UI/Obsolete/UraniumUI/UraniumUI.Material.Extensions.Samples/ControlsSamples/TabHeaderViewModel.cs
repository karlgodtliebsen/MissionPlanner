using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabHeaderViewModel : ObservableObject, IDisposable
{
    public TabHeaderModel[] TabHeaders { get; set; } =
    [
        new TabHeaderModel { Title = "Firmware", Content = "Confirm firmware, board identity, and protocol capabilities." },
        new TabHeaderModel { Title = "Frame", Content = "Choose the vehicle frame and actuator layout." },
        new TabHeaderModel { Title = "Accelerometer", Content = "Calibrate level and orientation sensors." },
        new TabHeaderModel { Title = "Compass", Content = "Calibrate compass instances and orientation." },
        new TabHeaderModel { Title = "Radio", Content = "Calibrate pilot input channels and ranges." },
        new TabHeaderModel { Title = "Flight Modes", Content = "Assign flight modes to pilot controls." },
        new TabHeaderModel { Title = "Battery", Content = "Configure voltage, current, and capacity monitoring." },
        new TabHeaderModel { Title = "ESC", Content = "Configure and calibrate electronic speed controllers." },
        new TabHeaderModel { Title = "Servo Output", Content = "Review actuator functions, limits, and reversal." },
        new TabHeaderModel { Title = "Optional Hardware", Content = "Configure supported serial, CAN, rangefinder, and other peripherals." },
        new TabHeaderModel { Title = "Safety", Content = "Review arming, failsafe, and mandatory preflight settings." },
        new TabHeaderModel { Title = "Summary", Content = "Review completion, warnings, and links to advanced configuration." }
    ];


    /// <inheritdoc />
    public void Dispose()
    {
    }
}
