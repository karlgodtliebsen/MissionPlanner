namespace Domain.Library.EventHub.Events;

/// <summary>
/// Base class for all events.
/// </summary>
/// <param name="name">The name of the event.</param>
/// <param name="data">The data associated with the event.</param>
/// <param name="metadata">The metadata associated with the event.</param>
public abstract class Event(string name, object? data, MetaData metadata)
{
    /// <summary>
    /// Event Name
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// 
    /// </summary>
    public Guid EventId { get; set; } = Guid.CreateVersion7(DateTimeOffset.UtcNow);

    /// <summary>
    /// 
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Guid CausationId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string SchemaName { get; set; } = "event.schema";

    /// <summary>
    /// 
    /// </summary>
    public string SchemaVersion { get; set; } = "v1";

    /// <summary>
    /// 
    /// </summary>
    public DateTimeOffset OccuredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 
    /// </summary>
    public Aggregate Aggregate { get; set; } = new();

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// 
    /// </summary>
    public MetaData MetaData { get; internal set; } = metadata;

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// The payload associated with the event.
    /// </summary>
    public object? Payload { get; internal set; } = data;

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? TryGetMetaData<T>() where T : MetaData
    {
        return MetaData as T;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? TryGetData<T>() where T : class
    {
        return Payload as T;
    }
}
