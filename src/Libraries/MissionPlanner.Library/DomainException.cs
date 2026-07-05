using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Domain.Library;

[Serializable]
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, string subText) : base(message + "\n" + subText)
    {
    }

    /// <summary>Throws an <see cref="DomainException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="message">Message</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull<T>([NotNull] T? argument, string? message = null, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            paramName ??= nameof(argument);
            if (message is null)
            {
                Throw(paramName);
            }

            Throw(paramName, message);
        }
    }


    /// <summary>Throws an <see cref="DomainException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="messageFunc">Lazy Fetch Message</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull<T>([NotNull] T? argument, Func<string> messageFunc, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            paramName ??= nameof(argument);
            var message = messageFunc.Invoke() ?? string.Empty;
            Throw(paramName, message);
        }
    }

    [DoesNotReturn]
    internal static void Throw(string paramName)
    {
        throw new DomainException($"Null Reference Exception for parameter named: {paramName}");
    }

    [DoesNotReturn]
    internal static void Throw(string paramName, string message)
    {
        throw new DomainException($"Null Reference Exception with message: '{message}' for parameter named:{paramName}");
    }

    [DoesNotReturn]
    internal static void Throw()
    {
        throw new DomainException("Null Reference Exception");
    }
}
