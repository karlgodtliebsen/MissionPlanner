namespace Domain.Library.EventHub.Events;

/// <summary>
/// Represents a domain event.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the metadata associated with the domain event.
    /// </summary>
    /// <returns></returns>
    MetaData GetMetaData();

    /// <summary>
    /// Gets the payload of the domain event.
    /// </summary>
    object? Payload { get; }

    /// <summary>
    /// Gets the name of the domain event.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Tries to get the metadata of the domain event.
    /// </summary>
    /// <typeparam name="T">The type of metadata.</typeparam>
    /// <returns>The metadata if available; otherwise, null.</returns>
    T? TryGetMetaData<T>() where T : MetaData;

    /// <summary>
    /// Tries to get the data of the domain event.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <returns>The data if available; otherwise, null.</returns>
    T? TryGetData<T>() where T : class;
}

/// <summary>
/// 
/// </summary>
public class DomainEvent : Event, IDomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEvent"/> class.
    /// </summary>
    /// <param name="name">The name of the domain event.</param>
    public DomainEvent(string name) : base(name, null, new MetaData())
    {
    }

    // ReSharper disable once MemberCanBeProtected.Global
    /// <inheritdoc />
    public DomainEvent(string name, object? data, MetaData metadata) : base(name, data, metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEvent"/> class with the specified name and data.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="data"></param>
    public DomainEvent(string name, object? data) : base(name, data, new MetaData())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEvent"/> class with the specified name and metadata.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="md"></param>
    public DomainEvent(string name, MetaData md) : base(name, null, md)
    {
    }

    /// <inheritdoc />
    public MetaData GetMetaData()
    {
        return MetaData;
    }
}

/// <inheritdoc />
public class DomainEvent<T> : DomainEvent
{
    /// <inheritdoc />
    public DomainEvent(string name, T data) : base(name, data, new MetaData())
    {
    }

    /// <inheritdoc />
    public DomainEvent(string name, T data, MetaData md) : base(name, data, md)
    {
    }

    /// <summary>
    /// Gets the data of the domain event.
    /// </summary>
    /// <returns>The data of the domain event.</returns>
    public T GetData()
    {
        return (T)Payload!;
    }
}

/// <inheritdoc />
public class DomainEvent<T, TM>(string name, T data, TM metadata) : DomainEvent(name, data, metadata) where TM : MetaData
{
    /// <summary>
    /// Gets the data of the domain event.
    /// </summary>
    /// <returns>The data of the domain event.</returns>
    public T GetData()
    {
        return (T)Payload!;
    }
}
