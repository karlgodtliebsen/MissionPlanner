using System.Runtime.CompilerServices;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>Provides binding cloning that includes MAUI compiled bindings.</summary>
public static class SafeBindingExtensions
{
    /// <summary>
    /// Creates an independent copy of a binding by using MAUI's own virtual clone
    /// implementation, with a public-API fallback for runtime bindings.
    /// </summary>
    /// <param name="binding">The binding to copy.</param>
    /// <returns>An unapplied binding with the same configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The installed MAUI version cannot clone the binding type.</exception>
    public static BindingBase SafeCopyAsClone(this BindingBase binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        try
        {
            return BindingBaseAccessors.Clone(binding);
        }
        catch (Exception exception) when (IsMissingCloneAccessor(exception))
        {
            return CloneKnownBinding(binding, exception);
        }
    }

    private static bool IsMissingCloneAccessor(Exception exception)
    {
        return exception is MissingMethodException or
            EntryPointNotFoundException or
            TypeLoadException or
            InvalidProgramException or
            PlatformNotSupportedException;
    }

    private static BindingBase CloneKnownBinding(BindingBase binding, Exception accessorException)
    {
        return binding switch
        {
            Binding runtimeBinding => CloneRuntimeBinding(runtimeBinding),
            MultiBinding multiBinding => CloneMultiBinding(multiBinding),
            _ => throw new NotSupportedException(
                $"Binding type '{binding.GetType().FullName}' cannot be cloned by the installed MAUI runtime.",
                accessorException)
        };
    }

    private static Binding CloneRuntimeBinding(Binding binding)
    {
        return new Binding(
            binding.Path,
            binding.Mode,
            binding.Converter,
            binding.ConverterParameter,
            binding.StringFormat,
            binding.Source)
        {
            FallbackValue = binding.FallbackValue,
            TargetNullValue = binding.TargetNullValue
        };
    }

    private static MultiBinding CloneMultiBinding(MultiBinding binding)
    {
        return new MultiBinding
        {
            Bindings = binding.Bindings.Select(SafeCopyAsClone).ToList(),
            Converter = binding.Converter,
            ConverterParameter = binding.ConverterParameter,
            FallbackValue = binding.FallbackValue,
            Mode = binding.Mode,
            StringFormat = binding.StringFormat,
            TargetNullValue = binding.TargetNullValue
        };
    }

    private static class BindingBaseAccessors
    {
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Clone")]
        internal static extern BindingBase Clone(BindingBase binding);
    }
}
