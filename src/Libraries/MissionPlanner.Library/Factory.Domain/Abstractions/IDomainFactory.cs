namespace Domain.Library.Factory.Domain.Abstractions;

/// <summary>
/// 
/// </summary>
public interface IDomainFactory
{
    void Add<TService, TImplementation>() where TService : notnull where TImplementation : notnull, TService;

    T Create<T>() where T : notnull;
    T Create<T, T1>(T1 a) where T : notnull;
    T Create<T, T1, T2>(T1 a, T2 b) where T : notnull;

    T Create<T, T1, T2, T3>(T1 a, T2 b, T3 c) where T : notnull;
    T Create<T, T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d) where T : notnull;
    T Create<T, T1, T2, T3, T4, T5>(T1 a, T2 b, T3 c, T4 d, T5 e) where T : notnull;

    T1 Create<T1, T2>() where T1 : notnull where T2 : notnull;
    T1 Create<T1, T2, T3>() where T1 : notnull where T2 : notnull where T3 : notnull;
    T1 Create<T1, T2, T3, T4>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull;
    T1 Create<T1, T2, T3, T4, T5>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull;
    T1 Create<T1, T2, T3, T4, T5, T6>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull;
    T1 Create<T1, T2, T3, T4, T5, T6, T7>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull where T7 : notnull;
    T1 Create<T1, T2, T3, T4, T5, T6, T7, T8>() where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where T5 : notnull where T6 : notnull where T7 : notnull where T8 : notnull;
}
