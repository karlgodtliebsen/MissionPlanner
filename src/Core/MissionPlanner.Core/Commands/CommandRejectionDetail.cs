using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Correlates a rejected command with the STATUSTEXT the vehicle sent around its acknowledgement, because
/// ArduPilot explains refusals (for example failed RC calibration limits) there rather than in COMMAND_ACK.
/// </summary>
public static class CommandRejectionDetail
{
    private const int MaximumLines = 5;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets the vehicle's warning-or-worse messages received since a command was sent.</summary>
    /// <param name="messageStore">The status-text history, or <see langword="null"/> when unavailable.</param>
    /// <param name="vehicleId">The vehicle that rejected the command.</param>
    /// <param name="sentAt">When the command was transmitted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The explanation lines, or <see langword="null"/> when the vehicle said nothing.</returns>
    public static async Task<string?> CollectAsync(
        IVehicleMessageStore? messageStore,
        VehicleId vehicleId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        if (messageStore is null)
        {
            return null;
        }

        try
        {
            // The explanation can trail the acknowledgement by a few frames.
            await Task.Delay(SettleDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Report what was already received.
        }

        var lines = messageStore.GetMessages(vehicleId)
            .Where(message => message.ReceivedAt >= sentAt && message.Severity <= MavSeverity.Warning)
            .Select(message => message.Text.Trim())
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .TakeLast(MaximumLines)
            .ToArray();
        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    /// <summary>Appends vehicle-provided detail to a rejection message.</summary>
    /// <param name="message">The generic rejection message.</param>
    /// <param name="detail">The detail from <see cref="CollectAsync"/>, if any.</param>
    /// <returns>The combined message.</returns>
    public static string Append(string message, string? detail)
    {
        return string.IsNullOrEmpty(detail) ? message : $"{message}{Environment.NewLine}Vehicle reported:{Environment.NewLine}{detail}";
    }
}
