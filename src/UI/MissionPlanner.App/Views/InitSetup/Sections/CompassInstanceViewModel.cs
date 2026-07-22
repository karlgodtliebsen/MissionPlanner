using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.Sections;

/// <summary>Presents one discovered compass instance and its reviewed edits.</summary>
public sealed partial class CompassInstanceViewModel : ObservableObject
{
    private readonly CompassSetupViewModel parent;

    /// <summary>Initializes a compass row.</summary>
    /// <param name="instance">The discovered compass instance.</param>
    /// <param name="options">The orientation choices available for editing.</param>
    /// <param name="parent">The owning compass workflow.</param>
    public CompassInstanceViewModel(CompassInstance instance, IReadOnlyList<CompassOrientationOption> options, CompassSetupViewModel parent)
    {
        this.parent = parent;
        Instance = instance;
        Orientations = options;
        SupportsExternal = instance.External is not null;
        IsUsed = instance.Use;
        IsExternal = instance.External ?? false;
        SelectedOrientation = options.FirstOrDefault(option => option.Value == instance.Orientation);
    }

    /// <summary>Gets the underlying discovered instance.</summary>
    public CompassInstance Instance { get; }

    /// <summary>Gets the orientation choices available for editing.</summary>
    public IReadOnlyList<CompassOrientationOption> Orientations { get; }

    /// <summary>Gets the one-based slot index.</summary>
    public int Index => Instance.Index;

    /// <summary>Gets the compass header describing slot, priority, and identity.</summary>
    public string Header => Instance.Priority > 0
        ? $"Compass {Instance.Index} · priority {Instance.Priority}{(Instance.IsPrimary ? " (primary)" : string.Empty)}"
        : $"Compass {Instance.Index} · unranked";

    /// <summary>Gets the device identity label.</summary>
    public string DeviceLabel => $"Device ID {Instance.DeviceId}";

    /// <summary>Gets the health label.</summary>
    public string HealthLabel => Instance.Healthy switch
    {
        true => "Healthy",
        false => "Unhealthy",
        null => "Health not reported"
    };

    /// <summary>Gets the offsets label.</summary>
    public string OffsetsLabel => $"Offsets X {Instance.OffsetX:F0}, Y {Instance.OffsetY:F0}, Z {Instance.OffsetZ:F0}";

    /// <summary>Gets the motor-compensation label.</summary>
    public string MotorCompensationLabel => Instance.MotorCompensationConfigured
        ? "Motor compensation configured"
        : "Motor compensation disabled";

    /// <summary>Gets whether the vehicle exposes an external flag for this compass.</summary>
    public bool SupportsExternal { get; }

    /// <summary>Gets or sets whether the compass is enabled for navigation.</summary>
    [ObservableProperty]
    public partial bool IsUsed { get; set; }

    /// <summary>Gets or sets whether the compass is marked external.</summary>
    [ObservableProperty]
    public partial bool IsExternal { get; set; }

    /// <summary>Gets or sets the reviewed orientation selection.</summary>
    [ObservableProperty]
    public partial CompassOrientationOption? SelectedOrientation { get; set; }

    /// <summary>Restores the used flag to the discovered value after a declined confirmation.</summary>
    public void RevertUse()
    {
        IsUsed = Instance.Use;
    }

    [RelayCommand]
    private Task ApplyAsync()
    {
        return parent.ApplyCompassAsync(this);
    }
}
