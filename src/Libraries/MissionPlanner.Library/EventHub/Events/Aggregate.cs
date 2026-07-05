namespace Domain.Library.EventHub.Events;

public class Aggregate
{
    public string Type { get; set; } = null!;

    public string Id { get; set; } = null!;
}
