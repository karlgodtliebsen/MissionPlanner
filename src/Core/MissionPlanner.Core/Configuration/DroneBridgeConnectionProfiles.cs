namespace MissionPlanner.Core.Configuration;

/// <summary>
/// Represents a collection of drone bridge connection profiles.
/// </summary>
public class DroneBridgeConnectionProfiles
{
    /// <summary>
    /// The name of the configuration section for drone bridge connection profiles.
    /// </summary>
    public const string SectionName = "DroneBridgeConnectionProfiles";

    /// <summary>
    /// Gets or sets the collection of drone bridge connections.
    /// </summary>
    public DroneBridgeConnection[] EndPoints { get; set; } = [];

    public static string Template => @"""
""DroneBridgeConnectionProfiles"": {
  ""endPoints"" :[
    {
      ""name"": ""DroneBridge 1"",
      ""host"": ""192.168.1.217"",
      ""port"": 5760, //14550
      ""protocol"": ""udp"",
      ""expectedSystemId"": ""optional""
    },
    {
      ""name"": ""DroneBridge 2"",
      ""host"": ""192.168.1.248"",
      ""port"": 5760, //14550
      ""protocol"": ""udp"",
      ""expectedSystemId"": ""optional""
    }
  ]
},
  """;
}