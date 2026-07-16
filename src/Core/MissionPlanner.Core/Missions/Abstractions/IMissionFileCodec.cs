using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>
/// Serializes missions to and from the supported on-disk file formats.
/// </summary>
public interface IMissionFileCodec
{
    /// <summary>
    /// Serializes a mission (and optional home position) into the requested file format.
    /// </summary>
    /// <param name="mission">The mission to serialize.</param>
    /// <param name="home">The home position to embed, if known.</param>
    /// <param name="format">The target file format.</param>
    /// <returns>The file content.</returns>
    string Build(Mission mission, GeoPosition? home, MissionFileFormat format);

    /// <summary>
    /// Parses mission file content, detecting the format from the content itself
    /// (QGC WPL header or JSON document).
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <returns>The parsed items, home position and skip count.</returns>
    /// <exception cref="InvalidDataException">The content is neither QGC WPL nor a JSON mission.</exception>
    MissionFileContent Parse(string content);
}
