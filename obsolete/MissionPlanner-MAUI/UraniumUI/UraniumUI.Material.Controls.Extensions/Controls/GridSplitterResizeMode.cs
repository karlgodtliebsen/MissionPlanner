namespace UraniumUI.Material.Controls;

/// <summary>
/// Specifies how the GridSplitter resizes the adjacent columns or rows.
/// </summary>
public enum GridSplitterResizeMode
{
    /// <summary>Resizes only the column before the splitter.</summary>
    Previous,

    /// <summary>Resizes only the column after the splitter.</summary>
    Next,

    /// <summary>Resizes both adjacent columns while preserving their combined width.</summary>
    PreviousAndNext
}
