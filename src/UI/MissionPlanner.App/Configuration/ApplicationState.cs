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
    /// Gets or sets the selected port for the application.
    /// </summary>
    public string SelectedPort { get; set; } = "AUTO";

    /// <summary>
    /// Gets or sets the selected baud rate for the application.
    /// </summary>
    public string SelectedBaudRate { get; set; } = "115200";

    /// <summary>
    /// Gets or sets the selected connection type for the application.
    /// </summary>
    public string SelectedConnectionType { get; set; } = "Serial";
}