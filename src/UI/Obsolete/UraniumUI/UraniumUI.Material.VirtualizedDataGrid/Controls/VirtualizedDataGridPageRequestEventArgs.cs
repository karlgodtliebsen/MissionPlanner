namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Describes a page of data requested by a remotely paged
/// <see cref="VirtualizedDataGrid"/>.
/// </summary>
public sealed class VirtualizedDataGridPageRequestEventArgs : EventArgs
{
    /// <summary>Initializes a page request.</summary>
    public VirtualizedDataGridPageRequestEventArgs(int pageNumber, int pageSize)
    {
        PageNumber = Math.Max(1, pageNumber);
        PageSize = Math.Max(1, pageSize);
    }

    /// <summary>Gets the requested one-based page number.</summary>
    public int PageNumber { get; }

    /// <summary>Gets the requested maximum number of rows.</summary>
    public int PageSize { get; }

    /// <summary>Gets the remote query limit. This is an alias for <see cref="PageSize"/>.</summary>
    public int Limit => PageSize;

    /// <summary>Gets the zero-based number of remote rows to skip.</summary>
    public int Skip => checked((PageNumber - 1) * PageSize);
}
