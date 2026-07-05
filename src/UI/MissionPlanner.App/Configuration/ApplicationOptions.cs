namespace MissionPlanner.App.Configuration;

/// <summary>
/// Represents the configuration options for the application.
/// </summary>
public class ApplicationOptions
{
    /// <summary>
    /// Gets or sets the name of the configuration section for the application.
    /// </summary>
    public static string SectionName { get; set; } = "ApplicationSettings";

    /// <summary>
    /// 
    /// </summary>
    public List<string> ConnectionTypes { get; } =
    [
        "Serial",
        "TCP",
        "UDP",
        "UDP Client"
    ];

    /// <summary>
    /// 
    /// </summary>
    public List<string> Ports { get; } =
    [
        "COM1",
        "COM2",
        "COM3",
        "AUTO"
    ];

    /// <summary>
    /// 
    /// </summary>
    public List<string> BaudRates { get; } =
    [
        "4800",
        "9600",
        "19200",
        "38400",
        "57600",
        "115200",
        "230400",
        "460800",
        "921600"
    ];

    /// <summary>
    /// Gets or sets the configured port for the application.
    /// </summary>
    public string Port { get; set; } = "AUTO";

    /// <summary>
    /// Gets or sets the configured baud rate for the application.
    /// </summary>
    public string BaudRate { get; set; } = "115200";


    /// <summary>
    /// Gets or sets the configured connection type for the application.
    /// </summary>
    public string ConnectionType { get; set; } = "Serial";


    /// <summary>
    /// Gets the template for the application configuration.
    /// </summary>
    public static string Template => @"""
""Application"": {
    ""Port"": ""AUTO"",
    ""BaudRate"": ""115200"",
    ""ConnectionType"": ""Serial"",
  },
""";
}
