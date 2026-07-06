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
    public List<string> Ports { get; } =
    [
        "AUTO",
        "TCP",
        "UDP",
        "UDPCI",
        "WS"
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
    /// Gets the template for the application configuration.
    /// </summary>
    public static string Template => @"""
""Application"": {
    ""Port"": ""AUTO"",
    ""BaudRate"": ""115200"",
  },
""";
}
