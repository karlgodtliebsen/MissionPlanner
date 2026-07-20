namespace MissionPlanner.Library.Factory.Domain.Abstractions;

/// <summary>
/// A factory interface for creating instances of types.
/// </summary>
public interface IFactory
{
    /// <summary>
    /// Provides the public API for Create&lt;TService&gt;.
    /// </summary>
    TService Create<TService>(TService a) where TService : notnull;
    /// <summary>
    /// Provides the public API for Create&lt;TService, T&gt;.
    /// </summary>
    TService Create<TService, T>(T a1) where TService : new();
    /// <summary>
    /// Provides the public API for Create&lt;TService, T1, T2&gt;.
    /// </summary>
    TService Create<TService, T1, T2>(T1 a1, T2 a2) where TService : new();
    /// <summary>
    /// Provides the public API for Create&lt;TService, T1, T2, T3&gt;.
    /// </summary>
    TService Create<TService, T1, T2, T3>(T1 a1, T2 a2, T3 a3) where TService : new();
    /// <summary>
    /// Provides the public API for Create&lt;TService, T1, T2, T3, T4&gt;.
    /// </summary>
    TService Create<TService, T1, T2, T3, T4>(T1 a1, T2 a2, T3 a3, T4 a4) where TService : new();
    /// <summary>
    /// Provides the public API for Create&lt;TService, T1, T2, T3, T4, T5&gt;.
    /// </summary>
    TService Create<TService, T1, T2, T3, T4, T5>(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5) where TService : new();
}
