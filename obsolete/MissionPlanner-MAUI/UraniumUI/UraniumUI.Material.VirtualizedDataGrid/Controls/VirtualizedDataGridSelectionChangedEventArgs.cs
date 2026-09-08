namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>Describes a change to a virtualized data grid selection.</summary>
public sealed class VirtualizedDataGridSelectionChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new selection-change description.</summary>
    public VirtualizedDataGridSelectionChangedEventArgs(
        IReadOnlyList<object> previousSelection,
        IReadOnlyList<object> currentSelection,
        object? selectedItem)
    {
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
        SelectedItem = selectedItem;
    }

    /// <summary>Gets the selected rows before the change.</summary>
    public IReadOnlyList<object> PreviousSelection { get; }

    /// <summary>Gets the selected rows after the change.</summary>
    public IReadOnlyList<object> CurrentSelection { get; }

    /// <summary>Gets the single selected row, or <see langword="null"/>.</summary>
    public object? SelectedItem { get; }
}
