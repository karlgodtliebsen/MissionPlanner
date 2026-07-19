namespace MissionPlanner.Library.Factory.Domain.Abstractions;

/// <summary>
/// Defines a factory interface for creating domain objects and managing service registrations.
/// </summary>
public interface IDomainFactory
{
    /// <summary>
    /// Adds a service and its implementation to the domain factory.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation.</typeparam>
    void Add<TService, TImplementation>() where TService : notnull where TImplementation : class, TService;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory.
    /// </summary>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T>() where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with one constructor parameter.
    /// </summary>
    /// <param name="a">The constructor parameter.</param>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <typeparam name="T1">The type of the constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T, T1>(T1 a) where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with two constructor parameters.
    /// </summary>
    /// <param name="a">The first constructor parameter.</param>
    /// <param name="b">The second constructor parameter.</param>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <typeparam name="T1">The type of the first constructor parameter.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T, T1, T2>(T1 a, T2 b) where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with three constructor parameters.
    /// </summary>
    /// <param name="a">The first constructor parameter.</param>
    /// <param name="b">The second constructor parameter.</param>
    /// <param name="c">The third constructor parameter.</param>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <typeparam name="T1">The type of the first constructor parameter.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T, T1, T2, T3>(T1 a, T2 b, T3 c) where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with four constructor parameters.
    /// </summary>
    /// <param name="a">The first constructor parameter.</param>
    /// <param name="b">The second constructor parameter.</param>
    /// <param name="c">The third constructor parameter.</param>
    /// <param name="d">The fourth constructor parameter.</param>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <typeparam name="T1">The type of the first constructor parameter.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T, T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d) where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with five constructor parameters.
    /// </summary>
    /// <param name="a">The first constructor parameter.</param>
    /// <param name="b">The second constructor parameter.</param>
    /// <param name="c">The third constructor parameter.</param>
    /// <param name="d">The fourth constructor parameter.</param>
    /// <param name="e">The fifth constructor parameter.</param>
    /// <typeparam name="T">The type of the object to create.</typeparam>
    /// <typeparam name="T1">The type of the first constructor parameter.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T Create<T, T1, T2, T3, T4, T5>(T1 a, T2 b, T3 c, T4 d, T5 e) where T : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with two constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2>() where T1 : notnull where T2 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with three constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3>() where T1 : notnull where T2 : notnull where T3 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with four constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3, T4>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with five constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3, T4, T5>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with six constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3, T4, T5, T6>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with seven constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3, T4, T5, T6, T7>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull where T7 : notnull;

    /// <summary>
    /// Creates an instance of the specified type using the domain factory, with eight constructor parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the object to create.</typeparam>
    /// <typeparam name="T2">The type of the second constructor parameter.</typeparam>
    /// <typeparam name="T3">The type of the third constructor parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh constructor parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth constructor parameter.</typeparam>
    /// <returns>An instance of the specified type.</returns>
    T1 Create<T1, T2, T3, T4, T5, T6, T7, T8>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull where T7 : notnull where T8 : notnull;
}
