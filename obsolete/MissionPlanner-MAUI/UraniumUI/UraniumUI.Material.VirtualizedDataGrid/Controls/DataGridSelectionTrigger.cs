namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>Specifies which user interactions change the row selection.</summary>
public enum DataGridSelectionTrigger
{
    /// <summary>Selection is changed only by a selection column.</summary>
    SelectionColumn,

    /// <summary>Selection is changed by tapping anywhere on a row.</summary>
    RowClick,

    /// <summary>Selection is changed by either a selection column or a row tap.</summary>
    SelectionColumnAndRowClick
}
