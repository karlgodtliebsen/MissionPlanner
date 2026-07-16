namespace MissionPlanner.App.Configuration;

/// <summary>
/// Represents the state of the application.
/// </summary>
public class ApplicationState
{
    /// <summary>
    /// Gets or sets a value indicating whether the application is connected.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the selected channel for the application.
    /// </summary>
    public string SelectedChannel { get; set; } = "AUTO";

    /// <summary>
    /// Gets or sets the selected baud rate for the application.
    /// </summary>
    public string SelectedBaudRate { get; set; } = "115200";

    /// <summary>
    /// Gets or sets the selected host for the application.
    /// </summary>
    public string SelectedHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the selected port for the application.
    /// </summary>
    public string SelectedPort { get; set; } = "14550";

    /// <summary>
    /// Gets or sets the name of the vehicle.
    /// </summary>
    public string? VehicleName { get; set; }
}
