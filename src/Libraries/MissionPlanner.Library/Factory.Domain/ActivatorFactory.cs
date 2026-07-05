using Domain.Library.Factory.Domain.Abstractions;

namespace Domain.Library.Factory.Domain;

/// <summary>
/// A factory that creates instances of types using the Activator class.
/// </summary>
public class ActivatorFactory : IFactory
{
    /// <inheritdoc/>
    public TService Create<TService>(TService a) where TService : notnull
    {
        return (TService)Activator.CreateInstance(typeof(TService))!;
    }

    /// <inheritdoc/>
    public TService Create<TService, T>(T a1) where TService : new()
    {
        return (TService)Activator.CreateInstance(typeof(TService), a1)!;
    }

    /// <inheritdoc/>
    public TService Create<TService, T1, T2>(T1 a1, T2 a2) where TService : new()
    {
        return (TService)Activator.CreateInstance(typeof(TService), a1, a2)!;
    }

    /// <inheritdoc/>
    public TService Create<TService, T1, T2, T3>(T1 a1, T2 a2, T3 a3) where TService : new()
    {
        return (TService)Activator.CreateInstance(typeof(TService), a1, a2, a3)!;
    }

    /// <inheritdoc/>
    public TService Create<TService, T1, T2, T3, T4>(T1 a1, T2 a2, T3 a3, T4 a4) where TService : new()
    {
        return (TService)Activator.CreateInstance(typeof(TService), a1, a2, a3, a4)!;
    }

    /// <inheritdoc/>
    public TService Create<TService, T1, T2, T3, T4, T5>(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5) where TService : new()
    {
        return (TService)Activator.CreateInstance(typeof(TService), a1, a2, a3, a4, a5)!;
    }
}
