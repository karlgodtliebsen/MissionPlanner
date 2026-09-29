namespace MissionPlanner.Analysis.Frequency;

/// <summary>Window applied before transforming uniformly spaced samples.</summary>
public enum WindowFunction
{
    /// <summary>Periodic Hann window; reduces leakage with coherent-gain correction.</summary>
    Hann,
    /// <summary>No taper; useful for bin-centered periodic signals.</summary>
    Rectangular
}

/// <summary>Configuration for one complete transform, without padding or truncation.</summary>
public sealed record FftOptions
{
    /// <summary>Power-of-two sample count, at least four. Input must have exactly this length.</summary>
    public int Size { get; init; } = 1024;

    /// <summary>Window whose coherent gain normalizes the returned amplitudes.</summary>
    public WindowFunction Window { get; init; } = WindowFunction.Hann;

    /// <summary>Whether to subtract the arithmetic mean before applying the window.</summary>
    public bool RemoveDc { get; init; } = true;

    internal void Validate()
    {
        if (Size < 4 || (Size & (Size - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Size), "FFT size must be a power of two, at least four.");
        }
        if (!Enum.IsDefined(Window))
        {
            throw new ArgumentOutOfRangeException(nameof(Window));
        }
    }
}
