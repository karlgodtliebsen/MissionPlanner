using System.Net;

namespace MissionPlanner.Transport;

/// <summary>
/// Provides extension methods for the <see cref="TransportEndPoint"/> class.
/// </summary>
public static class TransportEndPointExtensions
{
    /// <summary>
    /// Converts a <see cref="TransportEndPoint"/> to an <see cref="IPEndPoint"/>.
    /// </summary>
    /// <param name="ipEndPoint">The IP endpoint to convert.</param>
    /// <returns>The corresponding <see cref="TransportEndPoint"/>.</returns>
    public static TransportEndPoint ToEndPoint(this IPEndPoint ipEndPoint)
    {
        return new TransportEndPoint(ipEndPoint);
    }

    /// <summary>
    /// Converts a <see cref="IPEndPoint"/> to a <see cref="TransportEndPoint"/> with the specified transport name.
    /// </summary>
    /// <param name="ipEndPoint">The IP endpoint to convert.</param>
    /// <param name="transportName">The name of the transport.</param>
    /// <returns>The corresponding <see cref="TransportEndPoint"/>.</returns>
    public static TransportEndPoint ToTransportEndPoint(this IPEndPoint ipEndPoint, string transportName)
    {
        return new TransportEndPoint(transportName, ipEndPoint);
    }


    /// <summary>
    /// Converts a string representation of an endpoint to a <see cref="TransportEndPoint"/>.
    /// </summary>
    /// <param name="endPoint">The string representation of the endpoint.</param>
    /// <returns>The corresponding <see cref="TransportEndPoint"/>.</returns>
    public static TransportEndPoint ToEndPoint(this string endPoint)
    {
        return new TransportEndPoint(endPoint);
    }

    /// <summary>
    /// Converts a string representation of an endpoint to a <see cref="TransportEndPoint"/> with the specified transport name.
    /// </summary>
    /// <param name="endPoint">The string representation of the endpoint.</param>
    /// <param name="transportName">The name of the transport.</param>
    /// <returns>The corresponding <see cref="TransportEndPoint"/>.</returns>
    public static TransportEndPoint ToEndPoint(this string endPoint, string transportName)
    {
        return new TransportEndPoint(transportName, endPoint);
    }
}
