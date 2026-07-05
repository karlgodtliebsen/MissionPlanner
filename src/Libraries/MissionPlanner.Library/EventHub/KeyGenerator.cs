namespace Domain.Library.EventHub;

/// <summary>
/// A simple key generator that creates a unique key based on the event name and the type of data.
/// </summary>
public static class KeyGenerator
{
    private const string Separator = ":";

    public static string GetEventKey<T>(string eventName)
    {
        return $"{eventName}{Separator}{typeof(T).FullName}";
    }

    public static string GetEventKey<T>()
    {
        return $"data{Separator}{typeof(T).FullName}";
    }

    public static string SplitKey(string key)
    {
        return key.Contains(Separator) ? key.Split(Separator)[0] : key;
    }
}
