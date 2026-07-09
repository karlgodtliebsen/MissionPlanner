namespace MissionPlanner.MavLink.Client;

internal sealed record DecodedMavLinkMessage(object Message, DateTimeOffset ReceivedAt);
