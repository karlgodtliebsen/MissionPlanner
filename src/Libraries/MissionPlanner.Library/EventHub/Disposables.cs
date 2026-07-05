namespace Domain.Library.EventHub;

/// <summary>
/// 
/// </summary>
public sealed class Disposables : List<IDisposable>, IDisposable
{
    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
        foreach (var d in this)
        {
            d.Dispose();
        }

        Clear();
    }
}
