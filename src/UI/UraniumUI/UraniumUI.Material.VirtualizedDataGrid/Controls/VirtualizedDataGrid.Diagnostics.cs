namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Internal filtered/paged view and pager implementation for <see cref="VirtualizedDataGrid"/>.
/// </summary>
public partial class VirtualizedDataGrid
{
    /// <summary>
    /// Diagnostics information for the VirtualizedDataGrid.
    /// </summary>
    public VirtualizedDataGridDiagnostics Diagnostics { get; init; } = new VirtualizedDataGridDiagnostics();
}

/// <summary>
/// Diagnostics information for the <see cref="VirtualizedDataGrid"/>.
/// </summary>
public class VirtualizedDataGridDiagnostics
{
    /// <summary>
    /// Diagnostics: counts how many times the grid has retried applying the latest
    /// </summary>
    public int RowsApplyRetryCount { get; internal set; }
}
