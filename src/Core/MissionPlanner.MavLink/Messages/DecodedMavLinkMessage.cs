namespace MissionPlanner.MavLink.Messages;

internal sealed record DecodedMavLinkMessage(object Message, DateTimeOffset ReceivedAt);
