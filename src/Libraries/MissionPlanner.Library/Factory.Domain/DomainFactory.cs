using System.Collections.Concurrent;

using Domain.Library.Factory.Domain.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Domain.Library.Factory.Domain;

/// <summary>
/// A factory for creating domain objects.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve dependencies.</param>
public sealed class DomainFactory(IServiceProvider serviceProvider) : IDomainFactory
{
    private readonly IDictionary<Type, Type> typeMap = new ConcurrentDictionary<Type, Type>();

    /// <summary>
    /// Register a Service->Implementation relation. This must be done before using the factory
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    public void Add<TService, TImplementation>() where TService : notnull where TImplementation : notnull, TService
    {
        if (!typeMap.ContainsKey(typeof(TService)))
        {
            typeMap.Add(typeof(TService), typeof(TImplementation));
        }
    }

    /// <inheritdoc/>
    public T Create<T>() where T : notnull
    {
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType);
        return res;
    }

    /// <inheritdoc/>
    public T Create<T, T1>(T1 a) where T : notnull
    {
        DomainException.ThrowIfNull(a);
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, new object[] { a });
        return res;
    }

    /// <inheritdoc/>
    public T Create<T, T1, T2>(T1 a, T2 b) where T : notnull
    {
        DomainException.ThrowIfNull(a);
        DomainException.ThrowIfNull(b);
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, new object[] { a, b });
        return res;
    }

    /// <inheritdoc/>
    public T Create<T, T1, T2, T3>(T1 a, T2 b, T3 c) where T : notnull
    {
        DomainException.ThrowIfNull(a);
        DomainException.ThrowIfNull(b);
        DomainException.ThrowIfNull(c);
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, new object[] { a, b, c });
        return res;
    }

    /// <inheritdoc/>
    public T Create<T, T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d) where T : notnull
    {
        DomainException.ThrowIfNull(a);
        DomainException.ThrowIfNull(b);
        DomainException.ThrowIfNull(c);
        DomainException.ThrowIfNull(d);
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, new object[] { a, b, c, d });
        return res;
    }

    /// <inheritdoc/>
    public T Create<T, T1, T2, T3, T4, T5>(T1 a, T2 b, T3 c, T4 d, T5 e) where T : notnull
    {
        DomainException.ThrowIfNull(a);
        DomainException.ThrowIfNull(b);
        DomainException.ThrowIfNull(c);
        DomainException.ThrowIfNull(d);
        DomainException.ThrowIfNull(e);
        var instanceType = typeMap[typeof(T)];
        var res = (T)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, new object[] { a, b, c, d, e });
        return res;
    }


    /// <inheritdoc/>
    public T1 Create<T1, T2>()
        where T1 : notnull
        where T2 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3>() where T1 : notnull where T2 : notnull where T3 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3, T4>()
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];
        var instanceType4 = typeMap[typeof(T4)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);
        var instance4 = (T4)ActivatorUtilities.CreateInstance(serviceProvider, instanceType4);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3, instance4 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3, T4, T5>()
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];
        var instanceType4 = typeMap[typeof(T4)];
        var instanceType5 = typeMap[typeof(T5)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);
        var instance4 = (T4)ActivatorUtilities.CreateInstance(serviceProvider, instanceType4);
        var instance5 = (T5)ActivatorUtilities.CreateInstance(serviceProvider, instanceType5);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3, instance4, instance5 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3, T4, T5, T6>()
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
        where T6 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];
        var instanceType4 = typeMap[typeof(T4)];
        var instanceType5 = typeMap[typeof(T5)];
        var instanceType6 = typeMap[typeof(T6)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);
        var instance4 = (T4)ActivatorUtilities.CreateInstance(serviceProvider, instanceType4);
        var instance5 = (T5)ActivatorUtilities.CreateInstance(serviceProvider, instanceType5);
        var instance6 = (T6)ActivatorUtilities.CreateInstance(serviceProvider, instanceType6);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3, instance4, instance5, instance6 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3, T4, T5, T6, T7>()
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
        where T6 : notnull
        where T7 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];
        var instanceType4 = typeMap[typeof(T4)];
        var instanceType5 = typeMap[typeof(T5)];
        var instanceType6 = typeMap[typeof(T6)];
        var instanceType7 = typeMap[typeof(T7)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);
        var instance4 = (T4)ActivatorUtilities.CreateInstance(serviceProvider, instanceType4);
        var instance5 = (T5)ActivatorUtilities.CreateInstance(serviceProvider, instanceType5);
        var instance6 = (T6)ActivatorUtilities.CreateInstance(serviceProvider, instanceType6);
        var instance7 = (T7)ActivatorUtilities.CreateInstance(serviceProvider, instanceType7);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3, instance4, instance5, instance6, instance7 });
        return res;
    }

    /// <inheritdoc/>
    public T1 Create<T1, T2, T3, T4, T5, T6, T7, T8>()
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
        where T6 : notnull
        where T7 : notnull
        where T8 : notnull
    {
        var instanceType1 = typeMap[typeof(T1)];
        var instanceType2 = typeMap[typeof(T2)];
        var instanceType3 = typeMap[typeof(T3)];
        var instanceType4 = typeMap[typeof(T4)];
        var instanceType5 = typeMap[typeof(T5)];
        var instanceType6 = typeMap[typeof(T6)];
        var instanceType7 = typeMap[typeof(T7)];
        var instanceType8 = typeMap[typeof(T8)];

        var instance2 = (T2)ActivatorUtilities.CreateInstance(serviceProvider, instanceType2);
        var instance3 = (T3)ActivatorUtilities.CreateInstance(serviceProvider, instanceType3);
        var instance4 = (T4)ActivatorUtilities.CreateInstance(serviceProvider, instanceType4);
        var instance5 = (T5)ActivatorUtilities.CreateInstance(serviceProvider, instanceType5);
        var instance6 = (T6)ActivatorUtilities.CreateInstance(serviceProvider, instanceType6);
        var instance7 = (T7)ActivatorUtilities.CreateInstance(serviceProvider, instanceType7);
        var instance8 = (T8)ActivatorUtilities.CreateInstance(serviceProvider, instanceType8);

        var res = (T1)ActivatorUtilities.CreateInstance(serviceProvider, instanceType1, new object[] { instance2, instance3, instance4, instance5, instance6, instance7, instance8 });
        return res;
    }
}
